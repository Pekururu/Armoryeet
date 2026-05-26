using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Armoryeet;

public sealed unsafe class ArmouryScanner
{
    public static readonly InventoryType[] ArmouryTypes =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
    ];

    public static readonly InventoryType[] WeaponTypes =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
    ];

    public static readonly InventoryType[] ArmorTypes =
    [
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
    ];

    public static readonly InventoryType[] AccessoryTypes =
    [
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
    ];

    public List<ArmouryItem> FindOrphans(IReadOnlySet<uint> gearsetItemIds, Configuration configuration)
    {
        return this.FindOrphans(gearsetItemIds, GetEnabledArmouryTypes(configuration));
    }

    public List<ArmouryItem> FindOrphans(IReadOnlySet<uint> gearsetItemIds, IEnumerable<InventoryType> inventoryTypes)
    {
        var orphans = new List<ArmouryItem>();
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return orphans;

        foreach (var inventoryType in inventoryTypes)
        {
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (container == null)
                continue;

            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (!InventoryItemReader.TryGetBaseItemId(item, out var baseItemId))
                    continue;

                if (!gearsetItemIds.Contains(baseItemId))
                    orphans.Add(new ArmouryItem(inventoryType, slot, baseItemId));
            }
        }

        return orphans;
    }

    public static IEnumerable<InventoryType> GetEnabledArmouryTypes(Configuration configuration)
    {
        if (configuration.IncludeMainHand)
            yield return InventoryType.ArmoryMainHand;
        if (configuration.IncludeOffHand)
            yield return InventoryType.ArmoryOffHand;
        if (configuration.IncludeHead)
            yield return InventoryType.ArmoryHead;
        if (configuration.IncludeBody)
            yield return InventoryType.ArmoryBody;
        if (configuration.IncludeHands)
            yield return InventoryType.ArmoryHands;
        if (configuration.IncludeWaist)
            yield return InventoryType.ArmoryWaist;
        if (configuration.IncludeLegs)
            yield return InventoryType.ArmoryLegs;
        if (configuration.IncludeFeet)
            yield return InventoryType.ArmoryFeets;
        if (configuration.IncludeEars)
            yield return InventoryType.ArmoryEar;
        if (configuration.IncludeNeck)
            yield return InventoryType.ArmoryNeck;
        if (configuration.IncludeWrist)
            yield return InventoryType.ArmoryWrist;
        if (configuration.IncludeRings)
            yield return InventoryType.ArmoryRings;
    }
}
