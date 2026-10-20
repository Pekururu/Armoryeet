using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Diktat;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Armoryeet;

public sealed class ConfigUI : ThemedWindow, IDisposable
{
    private readonly Configuration configuration;
    private readonly Action save;
    private readonly ThemeEditor themeEditor;
    private readonly Action<ThemePreferences> saveTheme;
    private bool appearanceActive;

    public ConfigUI(Configuration configuration, Action save, ThemeEditor themeEditor, Action<ThemePreferences> saveTheme) : base("Armoryeet Settings###ArmoryeetSettings")
    {
        this.configuration = configuration;
        this.save = save;
        this.themeEditor = themeEditor;
        this.saveTheme = saveTheme;
        this.Size = new Vector2(540, 580);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(470, 420), MaximumSize = new Vector2(900, 900) };
    }

    public void Dispose() => this.themeEditor.Cancel();

    public override void OnClose()
    {
        this.themeEditor.Cancel();
        this.appearanceActive = false;
    }

    internal void SetAppearanceActive(bool active)
    {
        if (this.appearanceActive && !active) this.themeEditor.Cancel();
        this.appearanceActive = active;
    }

    protected override void DrawContent()
    {
        if (!ImGui.BeginTabBar("ArmoryeetSettingsTabs")) return;
        if (ImGui.BeginTabItem("Behavior")) { this.SetAppearanceActive(false); this.DrawBehavior(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Scan scope")) { this.SetAppearanceActive(false); this.DrawScanScope(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Movement")) { this.SetAppearanceActive(false); this.DrawMovement(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Appearance"))
        {
            this.SetAppearanceActive(true);
            try
            {
                // Keep the editor readable while the other windows preview arbitrary draft colors.
                using var style = new UiStyleScope(TokyoNight.Theme);
                using var child = ImRaii.Child("appearance-editor", Vector2.Zero, false);
                if (child) this.themeEditor.Draw(TokyoNight.Theme, "appearance", this.saveTheme);
            }
            finally { ImGui.EndTabItem(); }
        }
        ImGui.EndTabBar();
    }

    private void DrawBehavior()
    {
        Section("Automatic scanning", "Choose when results refresh.");
        this.Option("Scan on open", "Refresh results when opening Armoryeet.", () => this.configuration.ScanWhenOpened, value => this.configuration.ScanWhenOpened = value);
        this.Option("Scan after login", "Prepare results when your character is ready.", () => this.configuration.ScanOnLogin, value => this.configuration.ScanOnLogin = value);
        this.Option("Rescan after moving", "Refresh results when the queue finishes.", () => this.configuration.RescanAfterMove, value => this.configuration.RescanAfterMove = value);
        ImGui.Spacing();
        Section("After moving", "");
        this.Option("Close after completion", "Close once completed moves are reconciled.", () => this.configuration.CloseAfterMove, value => this.configuration.CloseAfterMove = value);
    }

    private void DrawScanScope()
    {
        UiWidgets.HelpText(this.Theme, "Choose containers for Scan and /yeet chest. Category commands keep their explicit scope.");
        ImGui.Spacing();
        this.Group("Weapons", [
            ("Main hand", () => this.configuration.IncludeMainHand, value => this.configuration.IncludeMainHand = value),
            ("Off hand", () => this.configuration.IncludeOffHand, value => this.configuration.IncludeOffHand = value)]);
        this.Group("Armour", [
            ("Head", () => this.configuration.IncludeHead, value => this.configuration.IncludeHead = value),
            ("Body", () => this.configuration.IncludeBody, value => this.configuration.IncludeBody = value),
            ("Hands", () => this.configuration.IncludeHands, value => this.configuration.IncludeHands = value),
            ("Waist", () => this.configuration.IncludeWaist, value => this.configuration.IncludeWaist = value),
            ("Legs", () => this.configuration.IncludeLegs, value => this.configuration.IncludeLegs = value),
            ("Feet", () => this.configuration.IncludeFeet, value => this.configuration.IncludeFeet = value)]);
        this.Group("Accessories", [
            ("Ears", () => this.configuration.IncludeEars, value => this.configuration.IncludeEars = value),
            ("Neck", () => this.configuration.IncludeNeck, value => this.configuration.IncludeNeck = value),
            ("Wrists", () => this.configuration.IncludeWrist, value => this.configuration.IncludeWrist = value),
            ("Rings", () => this.configuration.IncludeRings, value => this.configuration.IncludeRings = value)]);
        ImGui.Spacing(); ImGui.TextDisabled("Soul crystals are always protected and are never part of the scan scope.");
    }

    private void DrawMovement()
    {
        Section("Move timing", "Allow inventory updates between items.");
        var delay = this.configuration.DelayBetweenItemsMs;
        ImGui.SetNextItemWidth(Math.Min(240 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
        if (ImGui.SliderInt("Delay between items", ref delay, 0, 2000, "%d ms")) { this.configuration.DelayBetweenItemsMs = delay; this.save(); }
        UiWidgets.SecondaryText(this.Theme, "Recommended: 250 ms. Retry timing is separate.");
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        UiWidgets.Status(this.Theme, "Advanced", UiTone.Warning);
        ImGui.TextWrapped("Skipping confirmation starts moves immediately. Capacity and selection checks still apply.");
        this.Option("Skip move confirmation", "Expert slash commands already start immediately.", () => this.configuration.SkipMoveConfirmation, value => this.configuration.SkipMoveConfirmation = value);
    }

    private void Section(string title, string description)
    {
        UiWidgets.Title(this.Theme, title, description);
    }

    private void Group(string title, (string Label, Func<bool> Get, Action<bool> Set)[] options)
    {
        if (!UiWidgets.Section(this.Theme, title, title)) return;
        if (UiWidgets.Button(this.Theme, $"{title}-all", "Select All", UiTone.Neutral)) this.SetAll(options, true);
        ImGui.SameLine(); if (UiWidgets.Button(this.Theme, $"{title}-none", "Clear All", UiTone.Neutral)) this.SetAll(options, false);
        ImGui.Indent(8 * ImGuiHelpers.GlobalScale);
        foreach (var option in options) this.Option(option.Label, null, option.Get, option.Set);
        ImGui.Unindent(8 * ImGuiHelpers.GlobalScale); ImGui.Spacing();
    }

    private void SetAll((string Label, Func<bool> Get, Action<bool> Set)[] options, bool value)
    {
        foreach (var option in options) option.Set(value);
        this.save();
    }

    private void Option(string label, string? description, Func<bool> getter, Action<bool> setter)
    {
        var value = getter();
        if (UiWidgets.CompactCheckbox(this.Theme, label, ref value)) { setter(value); this.save(); }
        if (description == null) return;
        var indent = ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemInnerSpacing.X;
        ImGui.Indent(indent);
        UiWidgets.SecondaryText(this.Theme, description);
        ImGui.Unindent(indent);
    }
}
