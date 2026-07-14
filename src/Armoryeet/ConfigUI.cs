using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Armoryeet;

public sealed class ConfigUI : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Action save;

    public ConfigUI(Configuration configuration, Action save) : base("Armoryeet Settings###ArmoryeetSettings")
    {
        this.configuration = configuration; this.save = save;
        this.Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(470, 0), MaximumSize = new Vector2(760, 700) };
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextUnformatted("Configure when Armoryeet scans, what it includes, and how reviewed moves behave.");
        ImGui.Spacing();
        if (!ImGui.BeginTabBar("ArmoryeetSettingsTabs")) return;
        if (ImGui.BeginTabItem("Behavior")) { this.DrawBehavior(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Scan scope")) { this.DrawScanScope(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Movement")) { this.DrawMovement(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private void DrawBehavior()
    {
        Section("Opening and scanning", "Choose when scan results should refresh automatically.");
        this.Option("Scan when the main window opens", "Keeps reviewed results current whenever you open Armoryeet.", () => this.configuration.ScanWhenOpened, value => this.configuration.ScanWhenOpened = value);
        this.Option("Scan after login", "Prepares results as soon as your character becomes available.", () => this.configuration.ScanOnLogin, value => this.configuration.ScanOnLogin = value);
        this.Option("Rescan after a completed move", "Refreshes every Armoury group after the final item is processed.", () => this.configuration.RescanAfterMove, value => this.configuration.RescanAfterMove = value);
        ImGui.Spacing();
        Section("Window behavior", "Control what happens after a reviewed move finishes.");
        this.Option("Close after a completed move", "Closes only after completion has been reconciled safely.", () => this.configuration.CloseAfterMove, value => this.configuration.CloseAfterMove = value);
    }

    private void DrawScanScope()
    {
        ImGui.TextWrapped("Only enabled Armoury Chest containers are included by the main Scan button and /yeet chest. Container-specific commands keep their existing explicit behavior.");
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
        Section("Timing", "A short delay makes inventory updates easier for the game client to process.");
        var delay = this.configuration.DelayBetweenItemsMs;
        ImGui.SetNextItemWidth(Math.Min(300, ImGui.GetContentRegionAvail().X));
        if (ImGui.SliderInt("Delay between items", ref delay, 0, 2000, "%d ms")) { this.configuration.DelayBetweenItemsMs = delay; this.save(); }
        ImGui.TextDisabled("250 ms is the recommended default. Move retries still use their own safety interval.");
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        ImGui.TextColored(new Vector4(1, .65f, .2f, 1), "Advanced");
        ImGui.TextWrapped("Confirmation is an important final review step. Capacity and selection are still checked when confirmation is bypassed.");
        this.Option("Move reviewed selections without confirmation", "The main Move button begins immediately. Expert slash commands are already immediate.", () => this.configuration.SkipMoveConfirmation, value => this.configuration.SkipMoveConfirmation = value);
    }

    private static void Section(string title, string description)
    {
        ImGui.TextUnformatted(title); ImGui.TextDisabled(description); ImGui.Spacing();
    }

    private void Group(string title, (string Label, Func<bool> Get, Action<bool> Set)[] options)
    {
        ImGui.Separator(); ImGui.TextUnformatted(title); ImGui.Spacing();
        if (ImGui.SmallButton($"Select All###{title}-all")) this.SetAll(options, true);
        ImGui.SameLine(); if (ImGui.SmallButton($"Clear All###{title}-none")) this.SetAll(options, false);
        ImGui.Indent(8);
        foreach (var option in options) this.Option(option.Label, null, option.Get, option.Set);
        ImGui.Unindent(8); ImGui.Spacing();
    }

    private void SetAll((string Label, Func<bool> Get, Action<bool> Set)[] options, bool value)
    {
        foreach (var option in options) option.Set(value);
        this.save();
    }

    private void Option(string label, string? description, Func<bool> getter, Action<bool> setter)
    {
        var value = getter();
        if (ImGui.Checkbox(label, ref value)) { setter(value); this.save(); }
        if (description == null) return;
        ImGui.Indent(26); ImGui.TextDisabled(description); ImGui.Unindent(26); ImGui.Spacing();
    }
}
