using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed class ItemMover : IDisposable
{
    private const int MoveAttemptDelayMs = 250;
    private const int MaxMoveAttempts = 10;
    private readonly IFramework? framework;
    private readonly IItemMoveGame game;
    private readonly Configuration configuration;
    private readonly Queue<ArmouryItem> queue = new();
    private readonly List<ArmouryItem> completed = [];
    private readonly List<ArmouryItem> skipped = [];
    private readonly List<ItemSkip> skipDetails = [];
    private DateTimeOffset lastMove = DateTimeOffset.MinValue;
    private DateTimeOffset lastItemCompleted = DateTimeOffset.MinValue;
    private int moveAttempts;
    private int confirmationChecks;
    private ulong queueCharacterId;
    private InventoryLocation? pendingDestination;
    private InventorySlotSnapshot pendingItem;
    private ItemMoverSnapshot snapshot = new(ItemMoverState.Idle, string.Empty, 0, 0, 0, Array.Empty<ArmouryItem>(), Array.Empty<ArmouryItem>(), Array.Empty<ItemSkip>());

    public ItemMover(IFramework framework, IClientState clientState, IPlayerState playerState, ICondition condition, IPluginLog log, GearsetScanner gearsetScanner, Configuration configuration)
        : this(new ItemMoveGame(clientState, playerState, condition, log, gearsetScanner), configuration)
    {
        this.framework = framework;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    internal ItemMover(IItemMoveGame game, Configuration configuration)
    {
        this.game = game;
        this.configuration = configuration;
    }

    public event Action<ItemMoverSnapshot>? StateChanged;
    public ItemMoverSnapshot Snapshot => this.snapshot;
    public bool IsMoving => this.snapshot.IsActive;
    public int MovedCount => this.snapshot.MovedCount;
    public int SkippedCount => this.snapshot.SkippedCount;
    public int TotalCount => this.snapshot.TotalCount;
    public string Status => this.FormatStatus();
    public ulong CurrentCharacterId => this.game.CharacterId;

    public bool TryEnqueueReviewed(IReadOnlyList<ArmouryItem> items, ulong characterId)
    {
        if (!this.game.TryGetCapacity(out var availableSlots, out _) || items.Count > availableSlots) return false;
        return this.TryEnqueue(items, characterId);
    }

    public bool TryEnqueue(IEnumerable<ArmouryItem> items, ulong characterId)
    {
        if (this.IsMoving || characterId == 0 || characterId != this.CurrentCharacterId) return false;
        var immutable = items.ToArray();
        if (immutable.Length == 0) return false;
        this.queue.Clear();
        foreach (var item in immutable) this.queue.Enqueue(item);
        this.completed.Clear(); this.skipped.Clear(); this.skipDetails.Clear();
        this.ResetAttempt();
        this.lastMove = this.lastItemCompleted = DateTimeOffset.MinValue;
        this.queueCharacterId = characterId;
        this.Publish(ItemMoverState.Moving, string.Empty, immutable.Length);
        return true;
    }

    public void Stop()
    {
        if (!this.IsMoving) return;
        this.queue.Clear(); this.ResetAttempt(); this.lastItemCompleted = DateTimeOffset.MinValue;
        this.Publish(ItemMoverState.Stopped, string.Empty, this.snapshot.TotalCount);
    }

    public void Clear() => this.Stop();
    public void Dispose()
    {
        if (this.framework != null) this.framework.Update -= this.OnFrameworkUpdate;
        this.queue.Clear();
        this.ResetAttempt();
    }

    private void OnFrameworkUpdate(IFramework _) => this.Tick(DateTimeOffset.UtcNow);

    internal void Tick(DateTimeOffset now)
    {
        if (this.queue.Count == 0) return;
        // Identity is checked even while delayed or blocked, before touching inventory.
        if (this.CurrentCharacterId == 0 || this.CurrentCharacterId != this.queueCharacterId) { this.Stop(); return; }
        if (this.moveAttempts == 0 && this.lastItemCompleted != DateTimeOffset.MinValue && (now - this.lastItemCompleted).TotalMilliseconds < Math.Clamp(this.configuration.DelayBetweenItemsMs, 0, 2000)) return;
        if ((now - this.lastMove).TotalMilliseconds < MoveAttemptDelayMs) return;
        this.lastMove = now;
        if (this.game.MoveBlockReason is { } reason) { this.Pause(reason); return; }

        var next = this.queue.Peek();
        if (next.InventoryType == InventoryType.ArmorySoulCrystal) { this.SkipCurrent(ItemSkipReason.SoulCrystalProtected, now); return; }
        var sourceLocation = new InventoryLocation(next.InventoryType, next.Slot);
        var source = this.game.ReadSlot(sourceLocation);
        if (source.State == InventorySlotState.Unavailable) { this.Pause("Source inventory unavailable"); return; }

        if (this.pendingDestination is { } destination)
        {
            var target = this.game.ReadSlot(destination);
            if (target.State == InventorySlotState.Unavailable) { this.Pause("Destination inventory unavailable"); return; }
            if (source.State == InventorySlotState.Empty && target == this.pendingItem)
                this.CompleteCurrent(now);
            else if ((source.State == InventorySlotState.Occupied && source != this.pendingItem)
                || (target.State == InventorySlotState.Occupied && target != this.pendingItem)
                || ++this.confirmationChecks >= MaxMoveAttempts)
                this.SkipCurrent(ItemSkipReason.MoveUnconfirmed, now);
            else
                this.Publish(ItemMoverState.Moving, string.Empty, this.snapshot.TotalCount);
            return;
        }

        if (source.State == InventorySlotState.Empty) { this.SkipCurrent(ItemSkipReason.ArmourySlotEmpty, now); return; }
        if (source.BaseItemId != next.ItemId) { this.SkipCurrent(ItemSkipReason.ArmourySlotChanged, now); return; }
        if (!this.game.TryGetProtectedItems(out var protectedIds)) { this.Pause("Gearset or equipped inventory unavailable"); return; }
        if (protectedIds.Contains(next.ItemId)) { this.SkipCurrent(ItemSkipReason.GearsetProtected, now); return; }
        if (this.moveAttempts >= MaxMoveAttempts) { this.SkipCurrent(ItemSkipReason.RetryLimitReached, now); return; }
        if (!this.game.TryGetCapacity(out var freeSlots, out var firstEmpty)) { this.Pause("Inventory unavailable"); return; }
        if (freeSlots == 0)
        {
            this.queue.Clear(); this.Publish(ItemMoverState.Failed, "No free inventory slots", this.snapshot.TotalCount); return;
        }

        if (this.CurrentCharacterId != this.queueCharacterId) { this.Stop(); return; }
        this.moveAttempts++;
        if (this.game.Move(sourceLocation, firstEmpty))
        {
            this.pendingDestination = firstEmpty;
            this.pendingItem = source;
        }
        this.Publish(ItemMoverState.Moving, string.Empty, this.snapshot.TotalCount);
    }

    private void Pause(string reason)
    {
        if (this.snapshot.State != ItemMoverState.Paused || this.snapshot.PausedReason != reason)
            this.Publish(ItemMoverState.Paused, reason, this.snapshot.TotalCount);
    }

    private void ResetAttempt()
    {
        this.moveAttempts = this.confirmationChecks = 0;
        this.pendingDestination = null;
        this.pendingItem = default;
    }

    private void CompleteCurrent(DateTimeOffset now)
    {
        var item = this.queue.Dequeue();
        this.completed.Add(item);
        this.FinishCurrent(now);
    }

    private void SkipCurrent(ItemSkipReason reason, DateTimeOffset now)
    {
        var item = this.queue.Dequeue();
        this.skipped.Add(item); this.skipDetails.Add(new ItemSkip(item, reason));
        this.FinishCurrent(now);
    }

    private void FinishCurrent(DateTimeOffset now)
    {
        this.lastMove = this.lastItemCompleted = now;
        this.ResetAttempt();
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
