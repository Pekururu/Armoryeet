using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using QuickUI;

namespace Armoryeet;

internal static class ArmoryeetUi
{
    internal static float ButtonWidth(string label) => ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2;
    internal static float ButtonHeight(UiTheme theme) => Math.Max(theme.Metrics.ControlHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());

    internal static bool PrimaryButton(UiTheme theme, string id, string label, bool enabled = true, string? reason = null)
    {
        using var disabled = ImRaii.Disabled(!enabled);
        using var colors = ImRaii.PushColor(ImGuiCol.Text, enabled ? theme.Colors.Background : theme.Colors.DarkForeground)
            .Push(ImGuiCol.Button, enabled ? theme.Colors.Accent : theme.Colors.Raised)
            .Push(ImGuiCol.ButtonHovered, Vector4.Lerp(theme.Colors.Accent, Vector4.One, .12f))
            .Push(ImGuiCol.ButtonActive, Vector4.Lerp(theme.Colors.Accent, Vector4.One, .22f));
        var clicked = ImGui.Button($"{label}###{id}", new Vector2(0, ButtonHeight(theme)));
        if (!enabled && reason != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reason);
        return clicked;
    }

    internal static bool CompactCheckbox(UiTheme theme, string label, ref bool selected, bool mixed = false)
    {
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
        var clicked = ImGui.Checkbox(label, ref selected);
        if (mixed && !clicked)
        {
            var start = ImGui.GetItemRectMin();
            var side = ImGui.GetTextLineHeight();
            ImGui.GetWindowDrawList().AddRectFilled(start + new Vector2(side * .22f, side * .43f),
                start + new Vector2(side * .78f, side * .57f), ImGui.GetColorU32(theme.Colors.Accent));
        }
        return clicked;
    }

    internal static void Emphasis(UiTheme theme, string text)
    {
        const float scale = 1.15f;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var size = ImGui.CalcTextSize(text, false, width / scale) * scale;
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale,
            ImGui.GetCursorScreenPos(), ImGui.GetColorU32(theme.Colors.BrightForeground), text, width);
        ImGui.Dummy(new Vector2(Math.Min(width, size.X), size.Y));
    }

    internal static void SecondaryText(UiTheme theme, string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, theme.Colors.DarkForeground);
        ImGui.TextWrapped(text);
    }
}
