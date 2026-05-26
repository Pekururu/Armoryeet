using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public readonly record struct ArmouryItem(
    InventoryType InventoryType,
    int Slot,
    uint ItemId);
