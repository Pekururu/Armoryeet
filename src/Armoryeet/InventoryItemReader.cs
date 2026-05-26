using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public static unsafe class InventoryItemReader
{
    public static bool TryGetBaseItemId(InventoryItem* item, out uint itemId)
    {
        itemId = 0;
        if (item == null || item->IsEmpty())
            return false;

        var resolved = item->GetLinkedItem();
        if (resolved == null || resolved->IsEmpty())
            return false;

        itemId = ItemIdNormalizer.Normalize(resolved->GetBaseItemId());
        return itemId != 0;
    }
}
