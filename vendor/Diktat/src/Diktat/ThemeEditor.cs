using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Diktat;

/// <summary>Per-plugin theme draft and reusable editor. No filesystem access or global state.
/// Apply calls the consumer's save callback before accepting the draft; Cancel restores the last accepted snapshot.</summary>
public sealed class ThemeEditor
{
    private sealed record Selection(ThemePreset Preset, UiColors? CustomColors);
    private Selection saved;
    private Selection draft;
    private bool saveFailed;

    public ThemeEditor(ThemePreferences? preferences = null)
    {
        var preset = ThemePresets.Find(preferences?.PresetId);
        var recovered = preferences != null && preset.Id != preferences.PresetId;
        UiColors? custom = null;
        if (preferences?.CustomColors is { } values)
        {
            custom = preset.Theme.Colors;
            foreach (var slot in ThemePalette.Slots)
            {
                if (values.TryGetValue(slot.Key, out var text) && ThemePalette.TryParse(text, out var color)) custom = slot.Set(custom, color);
                else recovered = true;
            }
        }
        this.saved = this.draft = new Selection(preset, custom);
        this.LoadNotice = recovered ? "Some saved theme values were missing or invalid. Safe preset values were used; Apply saves the repaired palette." : null;
        this.PreviewTheme = preset.Theme;
        this.Refresh();
    }

    public string PresetId => this.draft.Preset.Id;
    public string PresetName => this.draft.Preset.Name;
    public bool IsCustom => this.draft.CustomColors != null;
    public bool IsDirty => this.draft != this.saved;
    public UiTheme PreviewTheme { get; private set; }
    public IReadOnlyList<string> Warnings { get; private set; } = Array.Empty<string>();
    public string? LoadNotice { get; private set; }

    public void SelectPreset(string id)
    {
        var preset = ThemePresets.All.FirstOrDefault(preset => preset.Id == id)
            ?? throw new ArgumentException("Unknown theme preset.", nameof(id));
        this.draft = new Selection(preset, null);
        this.Refresh();
    }

    public void Customize()
    {
        this.draft = this.draft with { CustomColors = this.PreviewTheme.Colors };
        this.Refresh();
    }

    public void SetColor(string key, Vector4 color)
    {
        if (!this.IsCustom) throw new InvalidOperationException("Customize the preset before editing colors.");
        var slot = FindSlot(key);
        this.draft = this.draft with { CustomColors = slot.Set(this.draft.CustomColors!, ThemePalette.Validate(color)) };
        this.Refresh();
    }

    public void ResetColor(string key) => this.SetColor(key, FindSlot(key).Get(this.draft.Preset.Theme.Colors));

    public void ResetToPreset() => this.SelectPreset(this.PresetId);

    public void Cancel()
    {
        this.draft = this.saved;
        this.Refresh();
    }

    /// <summary>Returns a detached snapshot containing no layout settings or alpha channels.</summary>
    public ThemePreferences CreatePreferences() => new()
    {
        PresetId = this.PresetId,
        CustomColors = this.IsCustom ? ThemePalette.Slots.ToDictionary(slot => slot.Key, slot => ThemePalette.Hex(slot.Get(this.PreviewTheme.Colors))) : null,
    };

    /// <summary>Save failures propagate and leave the last accepted snapshot unchanged.</summary>
    public void Apply(Action<ThemePreferences> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var accepted = this.draft;
        save(this.CreatePreferences());
        this.saved = accepted;
        this.LoadNotice = null;
        this.saveFailed = false;
    }

    /// <summary>Fills the available region with a scrollable editor and fixed actions.
    /// Use a readable, fixed editor theme; preview PreviewTheme in the consuming windows.
    /// Save exceptions show a retry message; the consumer's callback should log diagnostic details.</summary>
    public void Draw(UiTheme editorTheme, string id, Action<ThemePreferences> save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(save);
        using var scope = ImRaii.PushId(id);
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var statusHeight = new[] { "Save failed. See plugin log; retry or Cancel.", "Unsaved preview. Apply to keep it.", "Saved. Preview is up to date." }
            .Max(text => ImGui.CalcTextSize(text, false, width).Y);
        var footerHeight = Math.Max(editorTheme.Metrics.ControlHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight())
            + ImGui.GetStyle().ItemSpacing.Y + statusHeight;
        UiWidgets.BodyWithFooter(editorTheme, "editor", footerHeight, () => this.DrawColors(editorTheme), () =>
        {
            if (UiWidgets.Button(editorTheme, "apply", "Apply", enabled: this.IsDirty || this.LoadNotice != null,
                disabledReason: "No changes to save."))
            {
                try { this.Apply(save); }
                catch (Exception) { this.saveFailed = true; }
            }
            ImGui.SameLine();
            if (UiWidgets.Button(editorTheme, "cancel", "Cancel", UiTone.Neutral)) this.Cancel();
            UiWidgets.HelpText(editorTheme, this.saveFailed ? "Save failed. See plugin log; retry or Cancel."
                : this.IsDirty ? "Unsaved preview. Apply to keep it." : "Saved. Preview is up to date.");
        });
    }

    private void DrawColors(UiTheme theme)
    {
        UiWidgets.Title(theme, "Appearance", "Preview colors in your plugin window. This editor keeps a readable palette; Cancel restores the last applied theme.");
        if (this.LoadNotice != null) UiWidgets.HelpText(theme, this.LoadNotice);
        ImGui.TextUnformatted("Preset");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##preset", this.IsCustom ? $"Custom / {this.PresetName}" : this.PresetName))
        {
            try
            {
                foreach (var preset in ThemePresets.All)
                    if (ImGui.Selectable(preset.Name, !this.IsCustom && this.PresetId == preset.Id)) this.SelectPreset(preset.Id);
            }
            finally { ImGui.EndCombo(); }
        }
        UiWidgets.HelpText(theme, "Selecting a preset replaces the preview, not your saved theme. Colors only; geometry and opacity stay fixed.");
        if (!this.IsCustom)
        {
            if (UiWidgets.Button(theme, "customize", "Customize", UiTone.Neutral)) this.Customize();
        }
        else if (UiWidgets.Button(theme, "reset-preset", "Reset to preset", UiTone.Neutral)) this.ResetToPreset();
        if (this.Warnings.Count > 0) UiWidgets.HelpText(theme, $"Readability: {this.Warnings.Count} advisory warnings. Details below the colors.");

        foreach (var group in ThemePalette.Slots.GroupBy(slot => slot.Group))
        {
            if (!UiWidgets.Section(theme, group.Key, group.Key)) continue;
            foreach (var slot in group)
            {
                using var slotId = ImRaii.PushId(slot.Key);
                ImGui.TextUnformatted(slot.Label);
                var color = slot.Get(this.PreviewTheme.Colors);
                var rgb = new Vector3(color.X, color.Y, color.Z);
                var resetWidth = ImGui.CalcTextSize("Reset").X + ImGui.GetStyle().FramePadding.X * 2;
                using (ImRaii.Disabled(!this.IsCustom))
                {
                    ImGui.SetNextItemWidth(Math.Max(1, ImGui.GetContentRegionAvail().X - resetWidth - ImGui.GetStyle().ItemSpacing.X));
                    if (ImGui.ColorEdit3("##color", ref rgb, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.Uint8 | ImGuiColorEditFlags.NoOptions))
                        this.SetColor(slot.Key, new Vector4(rgb, 1));
                }
                if (!this.IsCustom && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Choose Customize to edit this preset.");
                ImGui.SameLine();
                if (UiWidgets.Button(theme, "reset", "Reset", UiTone.Neutral, enabled: this.IsCustom,
                    disabledReason: "Choose Customize to enable per-color reset.")) this.ResetColor(slot.Key);
            }
        }
        if (UiWidgets.Section(theme, "readability", "Readability", this.Warnings.Count.ToString()))
        {
            UiWidgets.HelpText(theme, "Warnings are advisory, not a full accessibility or color-vision audit. State text and checkmarks are retained; avoid relying on color alone.");
            if (this.Warnings.Count == 0) UiWidgets.HelpText(theme, "No issues found by the basic color checks.");
            foreach (var warning in this.Warnings) UiWidgets.HelpText(theme, warning);
        }
    }

    private static ThemePalette.Slot FindSlot(string key) => ThemePalette.Slots.FirstOrDefault(slot => slot.Key == key)
        ?? throw new ArgumentException("Unknown color token.", nameof(key));

    private void Refresh()
    {
        this.PreviewTheme = this.draft.Preset.Theme with { Colors = this.draft.CustomColors ?? this.draft.Preset.Theme.Colors };
        this.Warnings = ThemePalette.Warnings(this.PreviewTheme.Colors);
        this.saveFailed = false;
    }
}
