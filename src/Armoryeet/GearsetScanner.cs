using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Armoryeet;

public sealed unsafe class GearsetScanner
{
    private const int MaxGearsetSlots = 14;

    public bool TryBuildGearsetItemIds(out HashSet<uint> ids)
    {
        ids = new HashSet<uint>();
        var gearsets = RaptureGearsetModule.Instance();
        if (gearsets == null)
            return false;

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
            return false;

        var equipped = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
        if (!InventoryItemReader.IsReady(equipped))
            return false;

        for (var slot = 0; slot < equipped->Size; slot++)
        {
            var item = equipped->GetInventorySlot(slot);
            var snapshot = InventoryItemReader.Read(item);
            if (snapshot.State == InventorySlotState.Unavailable) return false;
            if (snapshot.State == InventorySlotState.Occupied)
                ids.Add(snapshot.BaseItemId);
        }

        return true;
    }
}
