using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public static unsafe class InventoryItemReader
{
    private static readonly InventoryType[] InventoryBags =
        [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];

    public static bool TryGetBaseItemId(InventoryItem* item, out uint itemId)
    {
        var snapshot = Read(item);
        itemId = snapshot.BaseItemId;
        return snapshot.State == InventorySlotState.Occupied;
    }

    internal static InventorySlotSnapshot ReadSlot(InventoryLocation location)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return default;
        var container = manager->GetInventoryContainer(location.Type);
        if (!IsReady(container) || location.Slot < 0 || location.Slot >= container->Size) return default;
        return Read(container->GetInventorySlot(location.Slot));
    }

    internal static bool IsReady(InventoryContainer* container) =>
        container != null && container->IsLoaded && container->Items != null && container->Size > 0;

    internal static InventorySlotSnapshot Read(InventoryItem* item)
    {
        if (item == null) return default;
        if (item->IsEmpty()) return new(InventorySlotState.Empty);

        var resolved = item->GetLinkedItem();
        if (resolved == null || resolved->IsSymbolic || resolved->IsEmpty()) return default;
        var itemId = resolved->GetItemId();
        var baseItemId = ItemIdNormalizer.Normalize(resolved->GetBaseItemId());
        var quantity = resolved->GetQuantity();
        return itemId == 0 || baseItemId == 0 || quantity == 0 ? default : new(InventorySlotState.Occupied, itemId, quantity, baseItemId);
    }

    internal static bool TryGetCapacity(out int freeSlots, out InventoryLocation firstEmpty)
    {
        freeSlots = 0;
        firstEmpty = default;
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        var count = 0;
        foreach (var type in InventoryBags)
        {
            var bag = manager->GetInventoryContainer(type);
            if (!IsReady(bag)) return false;
            for (var slot = 0; slot < bag->Size; slot++)
            {
                var item = bag->GetInventorySlot(slot);
                if (item == null) return false;
                if (!item->IsEmpty()) continue;
                if (count++ == 0) firstEmpty = new(type, slot);
            }
        }

        freeSlots = count;
        return true;
    }
}
