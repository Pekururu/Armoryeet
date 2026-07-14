using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OpenSettingsDelegate = System.Action;

namespace Armoryeet;

public sealed class PluginUI : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly GearsetScanner gearsetScanner;
    private readonly ArmouryScanner armouryScanner;
    private readonly ItemMover itemMover;
    private readonly ITextureProvider textureProvider;
    private readonly OpenSettingsDelegate openSettings;
    private readonly ScanSession session = new();
    private IReadOnlyList<ArmouryItem>? confirmationItems;
    private int confirmationCapacity;
    private bool openConfirmation;
    private bool staleConfirmation;
    private string filter = string.Empty;
    private bool showCompletionDetails;
    private ItemMoverSnapshot lastProcessedSnapshot;

    public PluginUI(Configuration configuration, GearsetScanner gearsetScanner, ArmouryScanner armouryScanner, ItemMover itemMover, ITextureProvider textureProvider, OpenSettingsDelegate openSettings)
        : base("Armoryeet###ArmoryeetMain")
    {
        this.configuration = configuration; this.gearsetScanner = gearsetScanner; this.armouryScanner = armouryScanner;
        this.itemMover = itemMover; this.textureProvider = textureProvider; this.openSettings = openSettings;
        this.lastProcessedSnapshot = itemMover.Snapshot;
        this.Size = new Vector2(620, 400);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(560, 280), MaximumSize = new Vector2(900, 720) };
        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.QuestionCircle, ShowTooltip = CommandReferenceUI.DrawCompactTooltip, Click = _ => { }, Priority = 1 });
        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.Cog, ShowTooltip = () => ImGui.SetTooltip("Settings"), Click = _ => this.openSettings(), Priority = 0 });
    }

    public ScanSession Session => this.session;
    public IReadOnlyList<ArmouryItem> PreviewItems => this.session.Items;
    public int ProtectedItemCount => this.session.ProtectedCount;
    public void Dispose() { }
    public override void OnOpen() { if (this.configuration.ScanWhenOpened) this.Scan(); }

    public void Scan()
    {
        var protectedIds = this.gearsetScanner.BuildGearsetItemIds();
        var result = this.armouryScanner.Scan(protectedIds, ArmouryScanner.GetEnabledArmouryTypes(this.configuration));
        this.SetScanResults(result.MovableItems, result.ProtectedItemCount);
    }

    public void SetScanResults(IEnumerable<ArmouryItem> items, int protectedCount) =>
        this.session.Replace(items, protectedCount, ArmouryScanner.GetFreeInventorySlots());

    public override void Draw()
    {
        this.ProcessTerminalState();
        this.session.RefreshCapacity(ArmouryScanner.GetFreeInventorySlots());
        this.DrawSummary();

        if (ImGui.Button(this.session.HasScanned ? "↻  Rescan" : "⌕  Scan", new Vector2(110, 0))) this.Scan();
        ImGui.SameLine();
        if (ImGui.Button("✓  Select All")) this.session.SelectAll();
        ImGui.SameLine();
        if (ImGui.Button("×  Clear All")) this.session.ClearAll();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##item-filter", "Filter by item name...", ref this.filter, 100);
        ImGui.Separator();

        if (!this.session.HasScanned) ImGui.TextUnformatted("Scan armoury chest for gear not used by gearsets.");
        else if (this.session.Items.Count == 0) ImGui.TextUnformatted("No unregistered armoury items found.");
        else
        {
            var visibleCount = 0;
            foreach (var group in this.session.Groups)
            {
                var visible = this.Filter(group.Items);
                visibleCount += visible.Count;
                if (visible.Count != 0) this.DrawGroup(group.Type, visible, group.Items.Count);
            }
            if (visibleCount == 0) ImGui.TextDisabled("No items match the filter.");
            else if (!string.IsNullOrWhiteSpace(this.filter)) ImGui.TextDisabled($"Showing {visibleCount} of {this.session.Items.Count} items. Selection totals include hidden items.");
        }

        ImGui.Separator();
        this.DrawProgress();
        this.DrawActions();
        this.DrawConfirmation();
    }


    private void DrawSummary()
    {
        ImGui.TextUnformatted($"Selected {this.session.SelectedCount} of {this.session.Items.Count}   Protected: {this.session.ProtectedCount}   Free slots: {this.session.FreeInventorySlots}");
        if (this.session.SelectedCount > this.session.FreeInventorySlots)
            ImGui.TextColored(new Vector4(1, .65f, .2f, 1), $"Free {this.session.SelectedCount - this.session.FreeInventorySlots} more inventory slot(s) to move this selection.");
    }

    private void DrawGroup(InventoryType type, IReadOnlyList<ArmouryItem> items, int totalCount)
    {
        ImGui.Spacing();

        var selectedCount = 0;
        foreach (var item in this.session.Items)
            if (item.InventoryType == type && this.session.IsSelected(item)) selectedCount++;

        var expanded = false;
        if (ImGui.BeginTable($"group-header-{type}", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("000/000").X);
            ImGui.TableNextColumn();
            expanded = ImGui.CollapsingHeader($"{FormatInventoryType(type)}###header-{type}", ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.TableNextColumn();
            var count = $"{selectedCount}/{totalCount}";
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(count).X));
            ImGui.TextDisabled(count);
            ImGui.EndTable();
        }
        if (!expanded) return;

        var allSelected = true;
        var anySelected = false;
        foreach (var item in this.session.Items)
        {
            if (item.InventoryType != type) continue;
            allSelected &= this.session.IsSelected(item);
            anySelected |= this.session.IsSelected(item);
        }
        var groupSelected = allSelected;
        if (this.itemMover.Snapshot.IsActive) ImGui.BeginDisabled();
        if (anySelected && !allSelected)
        {
            if (ImGui.SmallButton($"-  Partially selected##group-{type}")) this.session.ToggleGroup(type);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Some items are selected. Click to select the whole group.");
        }
        else if (ImGui.Checkbox($"Select all in {FormatInventoryType(type)}##group-{type}", ref groupSelected)) this.session.ToggleGroup(type);
        if (this.itemMover.Snapshot.IsActive) ImGui.EndDisabled();

        ImGui.Indent(12);
        if (ImGui.BeginTable($"items-{type}", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Selected", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            foreach (var item in items)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var selected = this.session.IsSelected(item);
                if (this.itemMover.Snapshot.IsActive) ImGui.BeginDisabled();
                if (ImGui.Checkbox($"##item-{type}-{item.Slot}", ref selected)) this.session.Toggle(item);
                ImGui.TableNextColumn(); this.DrawIcon(item.IconId);
                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{item.DisplayName}###select-{type}-{item.Slot}", selected)) this.session.Toggle(item);
                if (this.itemMover.Snapshot.IsActive) ImGui.EndDisabled();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Item ID: {item.ItemId}\nArmoury slot: {item.Slot + 1}\nContainer: {FormatInventoryType(type)}");
            }
            ImGui.EndTable();
        }

        ImGui.Unindent(12);
        ImGui.Spacing();
    }

    private void DrawIcon(uint iconId)
    {
        if (iconId != 0)
        {
            var texture = this.textureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
            if (texture != null) { ImGui.Image(texture.Handle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight())); return; }
        }
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
    }

    private void DrawProgress()
    {
        var snapshot = this.itemMover.Snapshot;
        if (snapshot.IsActive && snapshot.TotalCount > 0) ImGui.ProgressBar(snapshot.Progress, new Vector2(-1, 0), $"{snapshot.State} {snapshot.ProcessedCount}/{snapshot.TotalCount}");
        if (snapshot.State != ItemMoverState.Idle)
        {
            var color = snapshot.State switch
            {
                ItemMoverState.Completed when snapshot.SkippedCount == 0 => new Vector4(.35f, .9f, .45f, 1),
                ItemMoverState.Completed or ItemMoverState.Stopped => new Vector4(1, .75f, .25f, 1),
                ItemMoverState.Failed => new Vector4(1, .35f, .3f, 1),
                _ => new Vector4(.75f, .85f, 1, 1),
            };
            ImGui.TextColored(color, this.itemMover.Status);
        }
        if (snapshot.SkipDetails.Count > 0)
        {
            if (ImGui.SmallButton(this.showCompletionDetails ? "Hide skipped details" : "Show skipped details")) this.showCompletionDetails = !this.showCompletionDetails;
            if (this.showCompletionDetails)
                foreach (var skip in snapshot.SkipDetails) ImGui.BulletText($"{skip.Item.DisplayName}: {FormatSkipReason(skip.Reason)}");
        }
        if (snapshot.State is ItemMoverState.Completed or ItemMoverState.Stopped or ItemMoverState.Failed)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Rescan now")) this.Scan();
        }
    }

    private void DrawActions()
    {
        var active = this.itemMover.Snapshot.IsActive;
        if (active)
        {
            if (ImGui.Button("■  Stop", new Vector2(100, 0))) this.itemMover.Stop();
            ImGui.SameLine();
        }

        var reason = this.GetDisabledReason();
        if (reason != null) ImGui.BeginDisabled();
        if (ImGui.Button($"→  Move {this.session.SelectedCount} selected items to Inventory", new Vector2(-1, 0)))
        {
            if (this.configuration.SkipMoveConfirmation) this.StartReviewedMove(); else this.OpenConfirmation();
        }
        if (reason != null)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reason);
        }
    }

    private string? GetDisabledReason()
    {
        if (this.session.SelectedCount == 0) return "Select at least one item.";
        if (this.itemMover.Snapshot.IsActive) return "A move is already active.";
        if (this.confirmationItems != null || this.openConfirmation) return "Confirm or cancel the pending move first.";
        if (this.session.SelectedCount > this.session.FreeInventorySlots) return $"Not enough inventory space: {this.session.SelectedCount} required, {this.session.FreeInventorySlots} available.";
        return null;
    }

    private void OpenConfirmation()
    {
        this.session.RefreshCapacity(ArmouryScanner.GetFreeInventorySlots());
        if (this.session.SelectedCount == 0 || this.session.SelectedCount > this.session.FreeInventorySlots) return;
        this.confirmationItems = this.session.SelectedSnapshot(); this.confirmationCapacity = this.session.FreeInventorySlots; this.staleConfirmation = false; this.openConfirmation = true;
    }

    private void StartReviewedMove()
    {
        this.session.RefreshCapacity(ArmouryScanner.GetFreeInventorySlots());
        var items = this.session.SelectedSnapshot();
        this.itemMover.TryEnqueueReviewed(items, this.session.FreeInventorySlots);
    }

    private void DrawConfirmation()
    {
        if (this.openConfirmation) { ImGui.OpenPopup("Confirm move###ArmoryeetConfirm"); this.openConfirmation = false; }
        var open = true;
        if (!ImGui.BeginPopupModal("Confirm move###ArmoryeetConfirm", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!open) this.confirmationItems = null;
            return;
        }

        var items = this.confirmationItems ?? Array.Empty<ArmouryItem>();
        ImGui.TextUnformatted($"Move {items.Count} selected item(s) to Inventory?");
        ImGui.TextUnformatted($"Required slots: {items.Count}   Available when opened: {this.confirmationCapacity}");
        if (this.staleConfirmation) ImGui.TextColored(new Vector4(1, .4f, .3f, 1), "Selection or capacity changed. Cancel and review again.");
        if (ImGui.Button("Move", new Vector2(120, 0)))
        {
            var current = this.session.SelectedSnapshot();
            var capacity = ArmouryScanner.GetFreeInventorySlots(); this.session.RefreshCapacity(capacity);
            if (SameItems(items, current) && capacity == this.confirmationCapacity && capacity >= items.Count && this.itemMover.TryEnqueueReviewed(items, capacity))
            { this.confirmationItems = null; ImGui.CloseCurrentPopup(); }
            else this.staleConfirmation = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120, 0))) { this.confirmationItems = null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }

    private void ProcessTerminalState()
    {
        var snapshot = this.itemMover.Snapshot;
        if (ReferenceEquals(snapshot, this.lastProcessedSnapshot)) return;
        this.lastProcessedSnapshot = snapshot;
        if (snapshot.State is not (ItemMoverState.Completed or ItemMoverState.Stopped or ItemMoverState.Failed)) return;
        this.session.Reconcile(snapshot);
        if (snapshot.State == ItemMoverState.Completed && this.configuration.RescanAfterMove) this.Scan();
        if (snapshot.State == ItemMoverState.Completed && this.configuration.CloseAfterMove) this.IsOpen = false;
    }

    private static bool SameItems(IReadOnlyList<ArmouryItem> left, IReadOnlyList<ArmouryItem> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++) if (left[i] != right[i]) return false;
        return true;
    }

    private IReadOnlyList<ArmouryItem> Filter(IReadOnlyList<ArmouryItem> items)
    {
        if (string.IsNullOrWhiteSpace(this.filter)) return items;
        var matches = new List<ArmouryItem>();
        foreach (var item in items) if (item.DisplayName.Contains(this.filter, StringComparison.CurrentCultureIgnoreCase)) matches.Add(item);
        return matches;
    }

    private static string FormatSkipReason(ItemSkipReason reason) => reason switch
    {
        ItemSkipReason.SoulCrystalProtected => "soul crystals are protected",
        ItemSkipReason.ArmourySlotEmpty => "the scanned Armoury slot is now empty",
        ItemSkipReason.ArmourySlotChanged => "the item in the Armoury slot changed",
        ItemSkipReason.GearsetProtected => "the item is now used by a gearset or equipped",
        ItemSkipReason.RetryLimitReached => "the move did not complete after repeated attempts",
        _ => reason.ToString(),
    };

    private static string FormatInventoryType(InventoryType type) => type switch
    {
        InventoryType.ArmoryMainHand => "Main Hand", InventoryType.ArmoryOffHand => "Off Hand", InventoryType.ArmoryHead => "Head",
        InventoryType.ArmoryBody => "Body", InventoryType.ArmoryHands => "Hands", InventoryType.ArmoryWaist => "Waist",
        InventoryType.ArmoryLegs => "Legs", InventoryType.ArmoryFeets => "Feet", InventoryType.ArmoryEar => "Ears",
        InventoryType.ArmoryNeck => "Neck", InventoryType.ArmoryWrist => "Wrists", InventoryType.ArmoryRings => "Rings", _ => type.ToString(),
    };
}
