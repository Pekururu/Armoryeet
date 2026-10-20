using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

internal sealed unsafe class ItemMoveGame(
    IClientState clientState, IPlayerState playerState, ICondition condition,
    IPluginLog log, GearsetScanner gearsetScanner) : IItemMoveGame
{
    private static readonly ConditionFlag[] MoveBlockers =
    [
        ConditionFlag.InCombat, ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95,
        ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51, ConditionFlag.Occupied, ConditionFlag.Occupied30,
        ConditionFlag.OccupiedInEvent, ConditionFlag.OccupiedInQuestEvent, ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.TradeOpen, ConditionFlag.Crafting, ConditionFlag.ExecutingCraftingAction, ConditionFlag.PreparingToCraft,
        ConditionFlag.Gathering, ConditionFlag.ExecutingGatheringAction, ConditionFlag.Fishing, ConditionFlag.MeldingMateria,
        ConditionFlag.WatchingCutscene, ConditionFlag.WatchingCutscene78, ConditionFlag.LoggingOut,
    ];

    public ulong CharacterId => clientState.IsLoggedIn && playerState.IsLoaded ? playerState.ContentId : 0;

    public string? MoveBlockReason
    {
        get
        {
            foreach (var blocker in MoveBlockers)
                if (condition[blocker]) return blocker.ToString();
            return null;
        }
    }

    public bool TryGetProtectedItems(out HashSet<uint> items) => gearsetScanner.TryBuildGearsetItemIds(out items);
    public InventorySlotSnapshot ReadSlot(InventoryLocation location) => InventoryItemReader.ReadSlot(location);
    public bool TryGetCapacity(out int freeSlots, out InventoryLocation firstEmpty) =>
        InventoryItemReader.TryGetCapacity(out freeSlots, out firstEmpty);

    public bool Move(InventoryLocation source, InventoryLocation destination)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        var result = manager->MoveItemSlot(source.Type, (ushort)source.Slot, destination.Type, (ushort)destination.Slot, true);
        if (result != 0) log.Warning("MoveItemSlot returned {Result} for {InventoryType}:{Slot}", result, source.Type, source.Slot);
        return result == 0;
    }
}
