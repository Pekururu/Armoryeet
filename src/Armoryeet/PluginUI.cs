using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using SaveConfigurationDelegate = System.Action;
using OpenSettingsDelegate = System.Action;

namespace Armoryeet;

public sealed class PluginUI : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly GearsetScanner gearsetScanner;
    private readonly ArmouryScanner armouryScanner;
    private readonly ItemMover itemMover;
    private readonly IDataManager dataManager;
    private readonly SaveConfigurationDelegate saveConfiguration;
    private readonly OpenSettingsDelegate openSettings;
    private List<ArmouryItem> previewItems = [];
    private bool hasScanned;
    private int protectedItemCount;
    private bool showDetails;
    private bool wasMoving;

    public PluginUI(
        Configuration configuration,
        GearsetScanner gearsetScanner,
        ArmouryScanner armouryScanner,
        ItemMover itemMover,
        IDataManager dataManager,
        SaveConfigurationDelegate saveConfiguration,
        OpenSettingsDelegate openSettings)
        : base("Armoryeet###ArmoryeetMain")
    {
        this.configuration = configuration;
        this.gearsetScanner = gearsetScanner;
        this.armouryScanner = armouryScanner;
        this.itemMover = itemMover;
        this.dataManager = dataManager;
        this.saveConfiguration = saveConfiguration;
        this.openSettings = openSettings;
        this.showDetails = configuration.ShowDetailsByDefault;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
            Click = _ => this.openSettings(),
            Priority = 0,
        });
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        this.HandleMoveCompletion();

        if (ImGui.Button("Scan", new Vector2(116, 0)))
            this.Scan();

        ImGui.SameLine();
        var canMove = this.previewItems.Count > 0 && !this.itemMover.IsMoving;
        if (!canMove)
            ImGui.BeginDisabled();

        if (ImGui.Button("Move All to Inventory", new Vector2(176, 0)))
            this.itemMover.Enqueue(this.previewItems);

        if (!canMove)
            ImGui.EndDisabled();

        if (this.itemMover.IsMoving)
        {
            ImGui.SameLine();
            if (ImGui.Button("Stop", new Vector2(96, 0)))
                this.itemMover.Clear();
        }

        ImGui.Separator();

        this.DrawSummary();
        this.DrawMoveProgress();

        if (ImGui.Checkbox("Details", ref this.showDetails))
        {
            this.configuration.ShowDetailsByDefault = this.showDetails;
            this.saveConfiguration();
        }
        ImGui.Separator();

        if (this.previewItems.Count == 0)
        {
            ImGui.TextUnformatted("No unregistered armoury items found.");
            return;
        }

        foreach (var group in this.previewItems.GroupBy(item => item.InventoryType).OrderBy(group => Array.IndexOf(ArmouryScanner.ArmouryTypes, group.Key)))
        {
            var groupItems = group.OrderBy(item => item.Slot).ToArray();
            this.DrawContainerGroup(group.Key, groupItems);
        }
    }

    public override void OnOpen()
    {
        if (this.configuration.ScanWhenOpened)
            this.Scan();
    }

    public void Scan()
    {
        var gearsetItemIds = this.gearsetScanner.BuildGearsetItemIds();
        this.SetScanResults(this.armouryScanner.FindOrphans(gearsetItemIds, this.configuration), gearsetItemIds.Count);
    }

    public IReadOnlyList<ArmouryItem> PreviewItems => this.previewItems;

    public int ProtectedItemCount => this.protectedItemCount;

    public void SetScanResults(IEnumerable<ArmouryItem> items, int protectedItemCount)
    {
        this.previewItems = items.ToList();
        this.protectedItemCount = protectedItemCount;
        this.hasScanned = true;
    }

    private string GetItemName(uint itemId)
    {
        try
        {
            var row = this.dataManager.GetExcelSheet<Item>().GetRow(itemId);
            var name = row.Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? $"Item {itemId}" : name;
        }
        catch
        {
            return $"Item {itemId}";
        }
    }

    private string FormatInventoryType(InventoryType inventoryType)
    {
        var name = inventoryType.ToString();
        return name.StartsWith("Armory", StringComparison.Ordinal) ? name["Armory".Length..] : name;
    }

    private void DrawContainerGroup(InventoryType inventoryType, IReadOnlyList<ArmouryItem> items)
    {
        var label = $"{this.FormatInventoryType(inventoryType)} ({items.Count})";
        if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var moveLabel = $"Move to Inventory###Move{inventoryType}";
        var canMove = items.Count > 0 && !this.itemMover.IsMoving;
        if (!canMove)
            ImGui.BeginDisabled();

        if (ImGui.SmallButton(moveLabel))
            this.itemMover.Enqueue(items);

        if (!canMove)
            ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled($"{items.Count} item(s)");

        if (ImGui.BeginChild($"ArmoryeetItems{inventoryType}", new Vector2(0, Math.Min(items.Count * ImGui.GetTextLineHeightWithSpacing() + 8, 180)), true))
        {
            foreach (var item in items)
            {
                ImGui.TextUnformatted(this.GetItemName(item.ItemId));
                if (!this.showDetails)
                    continue;

                ImGui.SameLine();
                ImGui.TextDisabled($"#{item.ItemId} · slot {item.Slot + 1}");
            }
        }

        ImGui.EndChild();
    }

    private void DrawSummary()
    {
        if (!this.hasScanned)
        {
            ImGui.TextUnformatted("Scan armoury chest for gear not used by gearsets.");
            return;
        }

        var groupCount = this.previewItems.Select(item => item.InventoryType).Distinct().Count();
        ImGui.TextUnformatted($"{this.previewItems.Count} item(s) found · {groupCount} group(s) · {this.protectedItemCount} protected");
    }

    private void DrawMoveProgress()
    {
        if (this.itemMover.IsMoving && this.itemMover.TotalCount > 0)
        {
            var completed = this.itemMover.MovedCount + this.itemMover.SkippedCount;
            var progress = Math.Clamp((float)completed / this.itemMover.TotalCount, 0, 1);
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"Moving {completed}/{this.itemMover.TotalCount}");
            return;
        }

        if (this.itemMover.Status != "Idle")
            ImGui.TextUnformatted(this.itemMover.Status);
    }

    private void HandleMoveCompletion()
    {
        if (this.wasMoving && !this.itemMover.IsMoving && this.configuration.CloseAfterMove)
            this.IsOpen = false;

        this.wasMoving = this.itemMover.IsMoving;
    }
}
