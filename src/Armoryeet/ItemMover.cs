using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed unsafe class ItemMover : IDisposable
{
    private const int MoveAttemptDelayMs = 250;
    private const int MaxMoveAttempts = 10;
    private static readonly InventoryType[] InventoryBags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
    private static readonly ConditionFlag[] MoveBlockers =
    [
        ConditionFlag.InCombat, ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95,
        ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51, ConditionFlag.Occupied, ConditionFlag.Occupied30,
        ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent, ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.TradeOpen, ConditionFlag.Crafting, ConditionFlag.ExecutingCraftingAction, ConditionFlag.PreparingToCraft,
        ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction, ConditionFlag.Fishing, ConditionFlag.MeldingMateria,
        ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78, ConditionFlag.LoggingOut,
    ];

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly GearsetScanner gearsetScanner;
    private readonly Configuration configuration;
    private readonly Queue<ArmouryItem> queue = new();
    private readonly List<ArmouryItem> completed = [];
    private readonly List<ArmouryItem> skipped = [];
    private readonly List<ItemSkip> skipDetails = [];
    private DateTimeOffset lastMove = DateTimeOffset.MinValue;
    private DateTimeOffset lastItemCompleted = DateTimeOffset.MinValue;
    private int moveAttempts;
    private ItemMoverSnapshot snapshot = new(ItemMoverState.Idle, string.Empty, 0, 0, 0, Array.Empty<ArmouryItem>(), Array.Empty<ArmouryItem>(), Array.Empty<ItemSkip>());

    public ItemMover(IFramework framework, IClientState clientState, ICondition condition, IPluginLog log, GearsetScanner gearsetScanner, Configuration configuration)
    {
        this.framework = framework; this.clientState = clientState; this.condition = condition; this.log = log;
        this.gearsetScanner = gearsetScanner; this.configuration = configuration;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    public event Action<ItemMoverSnapshot>? StateChanged;
    public ItemMoverSnapshot Snapshot => this.snapshot;
    public bool IsMoving => this.snapshot.IsActive;
    public int MovedCount => this.snapshot.MovedCount;
    public int SkippedCount => this.snapshot.SkippedCount;
    public int TotalCount => this.snapshot.TotalCount;
    public string Status => this.FormatStatus();

    public bool TryEnqueueReviewed(IReadOnlyList<ArmouryItem> items, int availableSlots)
    {
        if (items.Count == 0 || items.Count > availableSlots || this.IsMoving) return false;
        this.Enqueue(items);
        return true;
    }

    public void Enqueue(IEnumerable<ArmouryItem> items)
    {
        var immutable = items.ToArray();
        this.queue.Clear();
        foreach (var item in immutable) this.queue.Enqueue(item);
        this.completed.Clear(); this.skipped.Clear(); this.skipDetails.Clear(); this.moveAttempts = 0; this.lastItemCompleted = DateTimeOffset.MinValue;
        this.Publish(immutable.Length == 0 ? ItemMoverState.Idle : ItemMoverState.Moving, string.Empty, immutable.Length);
    }

    public void Stop()
    {
        if (!this.IsMoving) return;
        this.queue.Clear(); this.moveAttempts = 0; this.lastItemCompleted = DateTimeOffset.MinValue;
        this.Publish(ItemMoverState.Stopped, string.Empty, this.snapshot.TotalCount);
    }

    public void Clear() => this.Stop();
    public void Dispose() { this.framework.Update -= this.OnFrameworkUpdate; this.queue.Clear(); }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (this.queue.Count == 0) return;
        var now = DateTimeOffset.UtcNow;
        if (this.moveAttempts == 0 && this.lastItemCompleted != DateTimeOffset.MinValue && (now - this.lastItemCompleted).TotalMilliseconds < Math.Clamp(this.configuration.DelayBetweenItemsMs, 0, 2000)) return;
        if ((now - this.lastMove).TotalMilliseconds < MoveAttemptDelayMs) return;
        if (!this.CanMove(out var reason)) { this.Publish(ItemMoverState.Paused, reason, this.snapshot.TotalCount); return; }
        if (this.snapshot.State == ItemMoverState.Paused) this.Publish(ItemMoverState.Moving, string.Empty, this.snapshot.TotalCount);

        var manager = InventoryManager.Instance();
        if (manager == null) { this.Publish(ItemMoverState.Paused, "Inventory unavailable", this.snapshot.TotalCount); return; }
        var next = this.queue.Peek();
        if (next.InventoryType == InventoryType.ArmorySoulCrystal) { this.SkipCurrent(ItemSkipReason.SoulCrystalProtected); return; }
        var source = manager->GetInventorySlot(next.InventoryType, next.Slot);
        if (!InventoryItemReader.TryGetBaseItemId(source, out var sourceId))
        {
            if (this.moveAttempts > 0) this.CompleteCurrent(); else this.SkipCurrent(ItemSkipReason.ArmourySlotEmpty);
            return;
        }
        if (sourceId != next.ItemId) { this.SkipCurrent(ItemSkipReason.ArmourySlotChanged); return; }
        if (this.gearsetScanner.BuildGearsetItemIds().Contains(next.ItemId)) { this.SkipCurrent(ItemSkipReason.GearsetProtected); return; }
        if (this.moveAttempts >= MaxMoveAttempts) { this.log.Warning("Too many move attempts for {InventoryType}:{Slot}", next.InventoryType, next.Slot); this.SkipCurrent(ItemSkipReason.RetryLimitReached); return; }
        if (!this.TryFindEmptyInventorySlot(manager, out var destinationType, out var destinationSlot))
        {
            this.queue.Clear(); this.Publish(ItemMoverState.Failed, "No free inventory slots", this.snapshot.TotalCount); return;
        }

        var result = manager->MoveItemSlot(next.InventoryType, (ushort)next.Slot, destinationType, destinationSlot, true);
        this.lastMove = DateTimeOffset.UtcNow; this.moveAttempts++;
        if (result != 0) this.log.Warning("MoveItemSlot returned {Result} for {InventoryType}:{Slot}", result, next.InventoryType, next.Slot);
    }

    private bool CanMove(out string reason)
    {
        if (!this.clientState.IsLoggedIn) { reason = "Player unavailable"; return false; }
        foreach (var blocker in MoveBlockers) if (this.condition[blocker]) { reason = blocker.ToString(); return false; }
        reason = string.Empty; return true;
    }

    private bool TryFindEmptyInventorySlot(InventoryManager* manager, out InventoryType type, out ushort slot)
    {
        foreach (var bagType in InventoryBags)
        {
            var bag = manager->GetInventoryContainer(bagType); if (bag == null) continue;
            for (var index = 0; index < bag->Size; index++)
            {
                var item = bag->GetInventorySlot(index);
                if (item != null && !item->IsEmpty()) continue;
                type = bagType; slot = (ushort)index; return true;
            }
        }
        type = default; slot = 0; return false;
    }

    private void CompleteCurrent()
    {
        var item = this.queue.Dequeue();
        this.completed.Add(item);
        this.FinishCurrent();
    }

    private void SkipCurrent(ItemSkipReason reason)
    {
        var item = this.queue.Dequeue();
        this.skipped.Add(item); this.skipDetails.Add(new ItemSkip(item, reason));
        this.FinishCurrent();
    }

    private void FinishCurrent()
    {
        this.lastMove = this.lastItemCompleted = DateTimeOffset.UtcNow; this.moveAttempts = 0;
        this.Publish(this.queue.Count == 0 ? ItemMoverState.Completed : ItemMoverState.Moving, string.Empty, this.snapshot.TotalCount);
    }

    private void Publish(ItemMoverState state, string reason, int total)
    {
        this.snapshot = new ItemMoverSnapshot(state, reason, this.completed.Count, this.skipped.Count, total,
            Array.AsReadOnly(this.completed.ToArray()), Array.AsReadOnly(this.skipped.ToArray()), Array.AsReadOnly(this.skipDetails.ToArray()));
        this.StateChanged?.Invoke(this.snapshot);
    }

    private string FormatStatus() => this.snapshot.State switch
    {
        ItemMoverState.Idle => "Idle",
        ItemMoverState.Moving => $"Moving {this.snapshot.ProcessedCount}/{this.snapshot.TotalCount}",
        ItemMoverState.Paused => $"Paused: {this.snapshot.PausedReason}",
        ItemMoverState.Completed => $"Moved {this.snapshot.MovedCount} item(s)" + (this.snapshot.SkippedCount == 0 ? string.Empty : $", skipped {this.snapshot.SkippedCount}"),
        ItemMoverState.Stopped => $"Stopped after {this.snapshot.ProcessedCount}/{this.snapshot.TotalCount}; moved {this.snapshot.MovedCount}, skipped {this.snapshot.SkippedCount}",
        ItemMoverState.Failed => $"Failed: {this.snapshot.PausedReason}; moved {this.snapshot.MovedCount}, skipped {this.snapshot.SkippedCount}",
        _ => "Idle",
    };
}
