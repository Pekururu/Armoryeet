using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using QuickUI;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OpenSettingsDelegate = System.Action;

namespace Armoryeet;

public sealed class PluginUI : ThemedWindow, IDisposable
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
    private ulong confirmationCharacterId;
    private bool openConfirmation;
    private bool staleConfirmation;
    private bool closeConfirmation;
    private string? scanError;
    private string filter = string.Empty;
    private bool showCompletionDetails;
    private bool showCompletion;
    private ItemMoverSnapshot lastProcessedSnapshot;

    public PluginUI(Configuration configuration, GearsetScanner gearsetScanner, ArmouryScanner armouryScanner, ItemMover itemMover, ITextureProvider textureProvider, OpenSettingsDelegate openSettings)
        : base("Armoryeet###ArmoryeetMain")
    {
        this.configuration = configuration; this.gearsetScanner = gearsetScanner; this.armouryScanner = armouryScanner;
        this.itemMover = itemMover; this.textureProvider = textureProvider; this.openSettings = openSettings;
        this.lastProcessedSnapshot = itemMover.Snapshot;
        this.itemMover.StateChanged += this.ProcessTerminalState;
        this.SetTheme(this.Theme with { Metrics = this.Theme.Metrics with { SectionGap = 6, ItemSpacing = new Vector2(8, 4) } });
        this.Size = new Vector2(660, 480);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(560, 400), MaximumSize = new Vector2(900, 720) };
        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.QuestionCircle, ShowTooltip = () => CommandReferenceUI.DrawCompactTooltip(this.Theme), Click = _ => { }, Priority = 1 });
        this.TitleBarButtons.Add(new TitleBarButton { Icon = FontAwesomeIcon.Cog, ShowTooltip = () => ImGui.SetTooltip("Settings"), Click = _ => this.openSettings(), Priority = 0 });
    }

    internal bool HasReviewItems => this.session.Items.Count > 0;
    internal bool ShowMoveActions => this.HasReviewItems || this.itemMover.Snapshot.IsActive;
    internal bool ShowCompletion => this.showCompletion;

    public ScanSession Session => this.session;
    public IReadOnlyList<ArmouryItem> PreviewItems => this.session.Items;
    public int ProtectedItemCount => this.session.ProtectedCount;
    public void Dispose() => this.itemMover.StateChanged -= this.ProcessTerminalState;
    public override void OnOpen() { if (this.configuration.ScanWhenOpened) this.Scan(); }

    public void Scan() => this.Scan(ArmouryScanner.GetEnabledArmouryTypes(this.configuration));

    public bool Scan(IEnumerable<InventoryType> inventoryTypes)
    {
        if (this.itemMover.IsMoving) return false;
        var characterId = this.itemMover.CurrentCharacterId;
        if (characterId == 0 || !this.gearsetScanner.TryBuildGearsetItemIds(out var protectedIds)
            || !this.armouryScanner.TryScan(protectedIds, inventoryTypes, out var result)
            || characterId != this.itemMover.CurrentCharacterId)
        {
            this.ResetForCharacter();
            this.scanError = "Character, gearset, or inventory data unavailable. Scan again when loaded.";
            return false;
        }

        this.scanError = null;
        this.showCompletion = false;
        this.session.Replace(result.MovableItems, result.ProtectedItemCount, ArmouryScanner.GetFreeInventorySlots(), characterId);
        return true;
    }

    public void ResetForCharacter()
    {
        this.session.Reset();
        this.showCompletion = false;
        this.confirmationItems = null;
        this.confirmationCharacterId = 0;
        this.openConfirmation = this.staleConfirmation = false;
        this.closeConfirmation = true;
        this.scanError = null;
    }

    protected override void DrawContent()
    {
        this.session.RefreshCapacity(ArmouryScanner.GetFreeInventorySlots());
        if (this.ShowMoveActions)
        {
            var reason = this.GetDisabledReason();
            var gap = ImGui.GetStyle().ItemSpacing;
            var width = ImGui.GetContentRegionAvail().X;
            var actionsWidth = this.ActionWidth();
            var inline = width - actionsWidth - gap.X >= ImGui.CalcTextSize("104 free slots").X;
            var guidanceWidth = inline ? width - actionsWidth - gap.X : width;
            var capacityWarning = $"Not enough inventory space: {this.session.Items.Count} required, {this.session.FreeInventorySlots} available.";
            var textHeight = Math.Max(ImGui.CalcTextSize(capacityWarning, false, Math.Max(1, guidanceWidth)).Y,
                ImGui.CalcTextSize(reason ?? this.CapacityGuidance(), false, Math.Max(1, guidanceWidth)).Y);
            var height = inline ? Math.Max(ArmoryeetUi.ButtonHeight(this.Theme), textHeight)
                : ArmoryeetUi.ButtonHeight(this.Theme) + textHeight + gap.Y;
            UiWidgets.BodyWithFooter(this.Theme, "review", height + gap.Y * 2 + 1, this.DrawReview, this.DrawActions);
        }
        else this.DrawReview();
        // Open and draw the modal outside the body's child ID scope.
        this.DrawConfirmation();
    }

    private void DrawReview()
    {
        if (this.showCompletion || this.itemMover.Snapshot.IsActive) this.DrawProgress();
        if (!this.HasReviewItems)
        {
            if (this.scanError != null) UiWidgets.HelpText(this.Theme, this.scanError);
            else if (!this.showCompletion && !this.itemMover.Snapshot.IsActive)
            {
                UiWidgets.Title(this.Theme, this.session.HasScanned ? "No unused gear found" : "Review your Armoury Chest",
                    this.session.HasScanned ? "Equipped gear and saved gearsets remain protected." : "Scan for gear that is not equipped or used by a gearset.");
            }
            if (ArmoryeetUi.PrimaryButton(this.Theme, "scan", this.session.HasScanned ? "Scan again" : "Scan Armoury Chest",
                !this.itemMover.IsMoving, "Stop the active move before scanning.")) this.Scan();
            return;
        }

        this.DrawSummary();
        var enabled = !this.itemMover.IsMoving;
        const string disabledReason = "Stop the active move before changing the review.";
        if (UiWidgets.Button(this.Theme, "scan", "Rescan", UiTone.Neutral, enabled: enabled, disabledReason: disabledReason)) this.Scan();
        ImGui.SameLine();
        if (UiWidgets.Button(this.Theme, "select-all", "Select All", UiTone.Neutral, enabled: enabled, disabledReason: disabledReason)) this.session.SelectAll();
        ImGui.SameLine();
        if (UiWidgets.Button(this.Theme, "clear-all", "Clear All", UiTone.Neutral, enabled: enabled, disabledReason: disabledReason)) this.session.ClearAll();
        ImGui.TextUnformatted("Filter items");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##item-filter", "Filter by item name...", ref this.filter, 100);
        var visibleCount = 0;
        foreach (var group in this.session.Groups)
        {
            var visible = this.Filter(group.Items);
            visibleCount += visible.Count;
            if (visible.Count != 0) this.DrawGroup(group.Type, visible, group.Items.Count);
        }
        if (visibleCount == 0) UiWidgets.HelpText(this.Theme, "No items match the filter.");
        if (!string.IsNullOrWhiteSpace(this.filter)) ArmoryeetUi.SecondaryText(this.Theme,
            $"Showing {visibleCount} of {this.session.Items.Count} items. Selection includes hidden items.");
    }

    private void DrawSummary()
    {
        UiWidgets.Surface(this.Theme, "summary", () =>
        {
            ArmoryeetUi.Emphasis(this.Theme, $"{this.session.SelectedCount} selected / {this.session.Items.Count} available");
            var tight = this.session.SelectedCount > this.session.FreeInventorySlots;
            UiWidgets.Status(this.Theme, $"{this.session.FreeInventorySlots} free slots", tight ? UiTone.Warning : UiTone.Neutral);
            ImGui.SameLine();
            ArmoryeetUi.SecondaryText(this.Theme, $"·  {this.session.ProtectedCount} protected");
        });
    }

    private void DrawGroup(InventoryType type, IReadOnlyList<ArmouryItem> items, int totalCount)
    {
        var selectedCount = 0;
        foreach (var item in this.session.Items)
            if (item.InventoryType == type && this.session.IsSelected(item)) selectedCount++;
        var allSelected = selectedCount == totalCount;
        var mixed = selectedCount > 0 && !allSelected;
        var expanded = false;
        using (var table = ImRaii.Table($"category-{type}", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            if (!table) return;
            ImGui.TableSetupColumn("Selection", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeight());
            ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize($"{totalCount}/{totalCount}").X);
            ImGui.TableNextColumn();
            using (var disabled = ImRaii.Disabled(this.itemMover.Snapshot.IsActive))
            {
                if (ArmoryeetUi.CompactCheckbox(this.Theme, $"##group-{type}", ref allSelected, mixed)) this.session.ToggleGroup(type);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(this.itemMover.Snapshot.IsActive
                ? "Stop the active move before changing selection."
                : $"{FormatInventoryType(type)}: {selectedCount} of {totalCount} selected. Click to {(selectedCount == totalCount ? "clear" : "select")} the entire category, including hidden items.");
            ImGui.TableNextColumn();
            using (var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 0)))
                expanded = ImGui.CollapsingHeader($"{FormatInventoryType(type).ToUpperInvariant()}###header-{type}", ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{selectedCount}/{totalCount}");
        }
        if (!expanded) return;

        foreach (var item in items)
        {
            var icon = item.IconId == 0 ? null : this.textureProvider.GetFromGameIcon(new GameIconLookup(item.IconId)).GetWrapOrDefault();
            var enabled = !this.itemMover.Snapshot.IsActive;
            if (UiWidgets.SelectableRow(this.Theme, $"item-{type}-{item.Slot}", item.DisplayName,
                this.session.IsSelected(item), enabled: enabled,
                disabledReason: "Stop the active move before changing selection.", icon: icon)) this.session.Toggle(item);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip($"{item.DisplayName}\nItem ID: {item.ItemId}\nArmoury slot: {item.Slot + 1}\nContainer: {FormatInventoryType(type)}"
                    + (enabled ? string.Empty : "\nStop the active move before changing selection."));
        }
    }

    private void DrawProgress()
    {
        var snapshot = this.itemMover.Snapshot;
        UiWidgets.Surface(this.Theme, "movement", () =>
        {
            var tone = snapshot.State switch
            {
                ItemMoverState.Completed when snapshot.SkippedCount == 0 => UiTone.Success,
                ItemMoverState.Completed or ItemMoverState.Stopped or ItemMoverState.Paused => UiTone.Warning,
                ItemMoverState.Failed => UiTone.Danger,
                _ => UiTone.Info,
            };
            UiWidgets.Status(this.Theme, snapshot.State.ToString(), tone);
            if (snapshot.IsActive)
            {
                UiWidgets.Progress(this.Theme, snapshot.ProcessedCount, snapshot.TotalCount);
                UiWidgets.HelpText(this.Theme, this.itemMover.Status);
            }
            else
            {
                ArmoryeetUi.Emphasis(this.Theme, $"Moved {snapshot.MovedCount} items · {this.session.FreeInventorySlots} free slots");
                if (snapshot.SkippedCount > 0) UiWidgets.HelpText(this.Theme, $"{snapshot.SkippedCount} skipped items remain for review.");
                if (snapshot.State != ItemMoverState.Completed) UiWidgets.HelpText(this.Theme, this.itemMover.Status);
            }
            if (snapshot.SkipDetails.Count > 0)
            {
                if (UiWidgets.Button(this.Theme, "skip-details", this.showCompletionDetails ? "Hide skipped details" : "Show skipped details", UiTone.Neutral)) this.showCompletionDetails = !this.showCompletionDetails;
                if (this.showCompletionDetails)
                    foreach (var skip in snapshot.SkipDetails) ImGui.BulletText($"{skip.Item.DisplayName}: {FormatSkipReason(skip.Reason)}");
            }
        });
    }

    private string MoveLabel => $"Move {this.session.SelectedCount} items";
    private float ActionWidth() => (this.HasReviewItems ? ArmoryeetUi.ButtonWidth(this.MoveLabel) : 0)
        + (this.itemMover.Snapshot.IsActive ? ArmoryeetUi.ButtonWidth("Stop") + (this.HasReviewItems ? ImGui.GetStyle().ItemSpacing.X : 0) : 0);
    private string CapacityGuidance() => $"{this.session.SelectedCount} slots needed · {this.session.FreeInventorySlots} available";

    private void DrawActions()
    {
        if (!this.ShowMoveActions) return;
        ImGui.Separator();
        var reason = this.GetDisabledReason();
        var width = ImGui.GetContentRegionAvail().X;
        var actionWidth = this.ActionWidth();
        var gap = ImGui.GetStyle().ItemSpacing;
        var inline = width - actionWidth - gap.X >= ImGui.CalcTextSize("104 free slots").X;
        var origin = ImGui.GetCursorPos();
        var guidanceWidth = inline ? width - actionWidth - gap.X : width;
        ImGui.PushTextWrapPos(origin.X + Math.Max(1, guidanceWidth));
        try { ImGui.TextUnformatted(reason ?? this.CapacityGuidance()); }
        finally { ImGui.PopTextWrapPos(); }
        if (inline) ImGui.SetCursorPos(new Vector2(origin.X + width - actionWidth, origin.Y));
        else ImGui.SetCursorPosX(origin.X + Math.Max(0, width - actionWidth));
        if (this.itemMover.Snapshot.IsActive)
        {
            if (UiWidgets.Button(this.Theme, "stop", "Stop", UiTone.Danger)) this.itemMover.Stop();
            if (this.HasReviewItems) ImGui.SameLine();
        }
        if (this.HasReviewItems && ArmoryeetUi.PrimaryButton(this.Theme, "move-selected", this.MoveLabel, reason == null, reason))
        {
            if (this.configuration.SkipMoveConfirmation) this.StartReviewedMove(); else this.OpenConfirmation();
        }
    }

    private string? GetDisabledReason()
    {
        if (this.session.CharacterId == 0 || this.session.CharacterId != this.itemMover.CurrentCharacterId) return "Scan again for the current character.";
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
        this.confirmationCharacterId = this.session.CharacterId;
        this.closeConfirmation = false;
    }

    private void StartReviewedMove()
    {
        this.session.RefreshCapacity(ArmouryScanner.GetFreeInventorySlots());
        var items = this.session.SelectedSnapshot();
        this.itemMover.TryEnqueueReviewed(items, this.session.CharacterId);
    }

    private void DrawConfirmation()
    {
        if (this.openConfirmation) { ImGui.OpenPopup("Confirm move###ArmoryeetConfirm"); this.openConfirmation = false; }
        ImGui.SetNextWindowSizeConstraints(new Vector2(380 * ImGuiHelpers.GlobalScale, 0), new Vector2(560 * ImGuiHelpers.GlobalScale, float.MaxValue));
        var open = true;
        UiWidgets.Modal(this.Theme, "ArmoryeetConfirm", "Confirm move", ref open, this.DrawConfirmationContent);
        if (!ImGui.IsPopupOpen("Confirm move###ArmoryeetConfirm")) this.confirmationItems = null;
    }

    private void DrawConfirmationContent()
    {
        if (this.closeConfirmation || this.confirmationCharacterId == 0 || this.confirmationCharacterId != this.itemMover.CurrentCharacterId)
        {
            this.confirmationItems = null;
            this.closeConfirmation = false;
            ImGui.CloseCurrentPopup();
            return;
        }

        var items = this.confirmationItems ?? Array.Empty<ArmouryItem>();
        ArmoryeetUi.Emphasis(this.Theme, $"Move {items.Count} items to inventory?");
        ArmoryeetUi.SecondaryText(this.Theme, $"{items.Count} slots needed · {this.confirmationCapacity} available at review");
        ImGui.Spacing();
        ImGui.Separator();
        if (this.staleConfirmation) UiWidgets.Status(this.Theme, "Selection or capacity changed. Cancel and review again.", UiTone.Warning);
        var moveLabel = $"Move {items.Count} items";
        var buttonsWidth = ArmoryeetUi.ButtonWidth("Cancel") + ImGui.GetStyle().ItemSpacing.X + ArmoryeetUi.ButtonWidth(moveLabel);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - buttonsWidth));
        if (UiWidgets.Button(this.Theme, "cancel-move", "Cancel", UiTone.Neutral))
        {
            this.confirmationItems = null;
            ImGui.CloseCurrentPopup();
            return;
        }
        ImGui.SameLine();
        if (ArmoryeetUi.PrimaryButton(this.Theme, "confirm-move", moveLabel))
        {
            var current = this.session.SelectedSnapshot();
            var capacity = ArmouryScanner.GetFreeInventorySlots(); this.session.RefreshCapacity(capacity);
            if (SameItems(items, current) && this.session.CharacterId == this.confirmationCharacterId
                && capacity == this.confirmationCapacity && capacity >= items.Count
                && this.itemMover.TryEnqueueReviewed(items, this.confirmationCharacterId))
            { this.confirmationItems = null; ImGui.CloseCurrentPopup(); }
            else this.staleConfirmation = true;
        }
    }

    private void ProcessTerminalState(ItemMoverSnapshot snapshot)
    {
        if (ReferenceEquals(snapshot, this.lastProcessedSnapshot)) return;
        this.lastProcessedSnapshot = snapshot;
        if (snapshot.State is not (ItemMoverState.Completed or ItemMoverState.Stopped or ItemMoverState.Failed)) return;
        if (this.session.CharacterId != this.itemMover.CurrentCharacterId) { this.ResetForCharacter(); return; }
        this.session.Reconcile(snapshot);
        if (snapshot.State == ItemMoverState.Completed && this.configuration.RescanAfterMove) this.Scan();
        this.showCompletion = true;
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
        ItemSkipReason.MoveUnconfirmed => "the transfer could not be confirmed; rescan before retrying",
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
