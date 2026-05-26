using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Armoryeet;

public sealed unsafe class GearsetScanner
{
    private const int MaxGearsetSlots = 14;

    public HashSet<uint> BuildGearsetItemIds()
    {
        var ids = new HashSet<uint>();
        var gearsets = RaptureGearsetModule.Instance();
        if (gearsets == null)
            return ids;

        foreach (ref var gearset in gearsets->Entries)
        {
            if (!gearset.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            for (var slot = 0; slot < MaxGearsetSlots; slot++)
            {
                var item = gearset.Items[slot];
                if (item.ItemId != 0)
                    ids.Add(ItemIdNormalizer.Normalize(item.ItemId));
            }
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return ids;

        var equipped = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null)
            return ids;

        for (var slot = 0; slot < equipped->Size; slot++)
        {
            var item = equipped->GetInventorySlot(slot);
            if (InventoryItemReader.TryGetBaseItemId(item, out var itemId))
                ids.Add(itemId);
        }

        return ids;
    }
}
