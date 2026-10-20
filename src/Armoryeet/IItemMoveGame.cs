using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

internal enum InventorySlotState { Unavailable, Empty, Occupied }
internal readonly record struct InventorySlotSnapshot(InventorySlotState State, uint ItemId = 0, uint Quantity = 0, uint BaseItemId = 0);
internal readonly record struct InventoryLocation(InventoryType Type, int Slot);

// The native boundary is kept separate so queue safety can be tested without game memory.
internal interface IItemMoveGame
{
    ulong CharacterId { get; }
    string? MoveBlockReason { get; }
    bool TryGetProtectedItems(out HashSet<uint> items);
    InventorySlotSnapshot ReadSlot(InventoryLocation location);
    bool TryGetCapacity(out int freeSlots, out InventoryLocation firstEmpty);
    bool Move(InventoryLocation source, InventoryLocation destination);
}
