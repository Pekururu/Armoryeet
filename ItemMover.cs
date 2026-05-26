using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed unsafe class ItemMover : IDisposable
{
    private const int MoveAttemptDelayMs = 250;
    private const int MaxMoveAttempts = 10;

    private static readonly InventoryType[] InventoryBags =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    private static readonly ConditionFlag[] MoveBlockers =
    [
        ConditionFlag.InCombat,
        ConditionFlag.BoundByDuty,
        ConditionFlag.BoundByDuty56,
        ConditionFlag.BoundByDuty95,
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.Occupied,
        ConditionFlag.Occupied30,
        ConditionFlag.OccupiedInEvent,
        ConditionFlag.OccupiedInQuestEvent,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.TradeOpen,
        ConditionFlag.Crafting,
        ConditionFlag.ExecutingCraftingAction,
        ConditionFlag.PreparingToCraft,
        ConditionFlag.Gathering,
        ConditionFlag.ExecutingGatheringAction,
        ConditionFlag.Fishing,
        ConditionFlag.MeldingMateria,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.LoggingOut,
    ];

    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly GearsetScanner gearsetScanner;
    private readonly Configuration configuration;
    private readonly Queue<ArmouryItem> queue = new();
    private DateTimeOffset lastMove = DateTimeOffset.MinValue;
    private DateTimeOffset lastItemCompleted = DateTimeOffset.MinValue;
    private int moveAttempts;

    public ItemMover(IFramework framework, IClientState clientState, ICondition condition, IPluginLog log, GearsetScanner gearsetScanner, Configuration configuration)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
        this.log = log;
        this.gearsetScanner = gearsetScanner;
        this.configuration = configuration;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    public int MovedCount { get; private set; }

    public int TotalCount { get; private set; }

    public int SkippedCount { get; private set; }

    public string Status { get; private set; } = "Idle";

    public bool IsMoving => this.queue.Count > 0;

    public void Enqueue(IEnumerable<ArmouryItem> items)
    {
        this.queue.Clear();
        foreach (var item in items)
            this.queue.Enqueue(item);

        this.MovedCount = 0;
        this.SkippedCount = 0;
        this.TotalCount = this.queue.Count;
        this.moveAttempts = 0;
        this.lastItemCompleted = DateTimeOffset.MinValue;
        this.Status = this.queue.Count == 0 ? "Idle" : $"Moving 0/{this.queue.Count}";
    }

    public void Clear()
    {
        this.queue.Clear();
        this.TotalCount = 0;
        this.moveAttempts = 0;
        this.lastItemCompleted = DateTimeOffset.MinValue;
        this.Status = "Idle";
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        this.queue.Clear();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (this.queue.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        if (this.moveAttempts == 0
            && this.lastItemCompleted != DateTimeOffset.MinValue
            && (now - this.lastItemCompleted).TotalMilliseconds < Math.Clamp(this.configuration.DelayBetweenItemsMs, 0, 2000))
            return;

        if ((now - this.lastMove).TotalMilliseconds < MoveAttemptDelayMs)
            return;

        if (!this.CanMove(out var reason))
        {
            this.Status = reason;
            return;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            this.Status = "Inventory unavailable";
            return;
        }

        var next = this.queue.Peek();
        if (next.InventoryType == InventoryType.ArmorySoulCrystal)
        {
            this.CompleteCurrentItem(moved: false);
            return;
        }

        var source = inventoryManager->GetInventorySlot(next.InventoryType, next.Slot);
        if (!InventoryItemReader.TryGetBaseItemId(source, out var sourceItemId))
        {
            this.CompleteCurrentItem(moved: true);
            return;
        }

        if (sourceItemId != next.ItemId)
        {
            this.CompleteCurrentItem(moved: false);
            return;
        }

        if (this.gearsetScanner.BuildGearsetItemIds().Contains(next.ItemId))
        {
            this.CompleteCurrentItem(moved: false);
            return;
        }

        if (this.moveAttempts > MaxMoveAttempts)
        {
            this.log.Warning("Too many move attempts for {InventoryType}:{Slot}", next.InventoryType, next.Slot);
            this.CompleteCurrentItem(moved: false);
            return;
        }

        if (inventoryManager->GetEmptySlotsInBag() == 0)
        {
            this.Status = "No free inventory slots";
            this.queue.Clear();
            return;
        }

        if (!this.TryFindEmptyInventorySlot(inventoryManager, out var destinationType, out var destinationSlot))
        {
            this.Status = "No free inventory slots";
            this.queue.Clear();
            return;
        }

        var result = inventoryManager->MoveItemSlot(
            next.InventoryType,
            (ushort)next.Slot,
            destinationType,
            destinationSlot,
            true);

        this.lastMove = DateTimeOffset.UtcNow;
        this.moveAttempts++;
        if (result != 0)
            this.log.Warning("MoveItemSlot returned {Result} for {InventoryType}:{Slot}", result, next.InventoryType, next.Slot);

        this.Status = $"Moving {this.MovedCount + this.SkippedCount}/{this.TotalCount}";
    }

    private bool CanMove(out string reason)
    {
        if (!this.clientState.IsLoggedIn)
        {
            reason = "Player unavailable";
            return false;
        }

        foreach (var blocker in MoveBlockers)
        {
            if (!this.condition[blocker])
                continue;

            reason = $"Paused: {blocker}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool TryFindEmptyInventorySlot(InventoryManager* inventoryManager, out InventoryType inventoryType, out ushort slot)
    {
        foreach (var bagType in InventoryBags)
        {
            var bag = inventoryManager->GetInventoryContainer(bagType);
            if (bag == null)
                continue;

            for (var index = 0; index < bag->Size; index++)
            {
                var item = bag->GetInventorySlot(index);
                if (item == null || !item->IsEmpty())
                    continue;

                inventoryType = bagType;
                slot = (ushort)index;
                return true;
            }
        }

        inventoryType = default;
        slot = 0;
        return false;
    }

    private string FormatDoneStatus()
    {
        return this.SkippedCount == 0
            ? $"Moved {this.MovedCount} item(s)"
            : $"Moved {this.MovedCount} item(s), skipped {this.SkippedCount}";
    }

    private void CompleteCurrentItem(bool moved)
    {
        this.queue.Dequeue();
        this.lastMove = DateTimeOffset.UtcNow;
        this.lastItemCompleted = DateTimeOffset.UtcNow;
        this.moveAttempts = 0;

        if (moved)
            this.MovedCount++;
        else
            this.SkippedCount++;

        this.Status = this.queue.Count == 0
            ? this.FormatDoneStatus()
            : $"Moving {this.MovedCount + this.SkippedCount}/{this.TotalCount}";
    }
}
