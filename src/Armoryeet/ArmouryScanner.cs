using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Armoryeet;

public sealed unsafe class ArmouryScanner
{
    private readonly IDataManager dataManager;

    public ArmouryScanner(IDataManager dataManager) => this.dataManager = dataManager;
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

    public bool TryScan(IReadOnlySet<uint> gearsetItemIds, IEnumerable<InventoryType> inventoryTypes, out ArmouryScanResult result)
    {
        result = new ArmouryScanResult([], 0);
        var orphans = new List<ArmouryItem>();
        var protectedCount = 0;
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return false;

        foreach (var inventoryType in inventoryTypes)
        {
            // The retired waist container is not loaded by current clients.
            if (inventoryType == InventoryType.ArmoryWaist || inventoryType == InventoryType.ArmorySoulCrystal) continue;
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (!InventoryItemReader.IsReady(container)) return false;

            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                var snapshot = InventoryItemReader.Read(item);
                if (snapshot.State == InventorySlotState.Unavailable) return false;
                if (snapshot.State == InventorySlotState.Empty) continue;
                var baseItemId = snapshot.BaseItemId;

                if (gearsetItemIds.Contains(baseItemId))
                {
                    protectedCount++;
                }
                else
                {
                    var name = $"Item {baseItemId}";
                    uint iconId = 0;
                    try
                    {
                        var row = this.dataManager.GetExcelSheet<Item>().GetRow(baseItemId);
                        var localized = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(localized)) name = localized;
                        iconId = row.Icon;
                    }
                    catch { }

                    orphans.Add(new ArmouryItem(inventoryType, slot, baseItemId, name, iconId));
                }
            }
        }

        result = new ArmouryScanResult(orphans, protectedCount);
        return true;
    }

    public static int GetFreeInventorySlots()
    {
        return InventoryItemReader.TryGetCapacity(out var slots, out _) ? slots : 0;
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

public sealed record ArmouryScanResult(IReadOnlyList<ArmouryItem> MovableItems, int ProtectedItemCount);
