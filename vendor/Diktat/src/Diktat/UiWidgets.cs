using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Diktat;

/// <summary>Presentation only. Call within a UiStyleScope and keep selection/actions in the caller.</summary>
public static class UiWidgets
{
    public static void Title(UiTheme theme, string title, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        using (ImRaii.PushColor(ImGuiCol.Text, theme.Colors.BrightForeground)) ImGui.TextWrapped(title);
        if (!string.IsNullOrWhiteSpace(description)) HelpText(theme, description);
    }

    /// <summary>Readable, wrapped guidance. Important restrictions should also appear inline, not only in tooltips.</summary>
    public static void HelpText(UiTheme theme, string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, theme.Colors.Foreground);
        ImGui.TextWrapped(text);
    }

    public static void Metrics(UiTheme theme, string id, params UiMetric[] metrics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(metrics);
        if (metrics.Length == 0) return;
        foreach (var metric in metrics)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metric.Label);
            ArgumentException.ThrowIfNullOrWhiteSpace(metric.Value);
        }
        var widest = metrics.Max(metric => Math.Max(ImGui.CalcTextSize(metric.Label.ToUpperInvariant()).X, ImGui.CalcTextSize(metric.Value).X));
        var cellWidth = Math.Max(1, widest + theme.Metrics.CellPadding.X * 2 * ImGuiHelpers.GlobalScale);
        var columns = Math.Clamp((int)(ImGui.GetContentRegionAvail().X / cellWidth), 1, metrics.Length);
        using var table = ImRaii.Table($"metrics##{id}", columns, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings);
        if (!table) return;
        foreach (var metric in metrics)
        {
            ImGui.TableNextColumn();
            using var group = ImRaii.Group();
            using (ImRaii.PushColor(ImGuiCol.Text, metric.Tone == UiTone.Neutral ? theme.Colors.BrightForeground : theme.Color(metric.Tone)))
                ImGui.TextWrapped(metric.Value);
            HelpText(theme, metric.Label.ToUpperInvariant());
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{metric.Label}: {metric.Value}");
        }
    }

    /// <summary>Scrolls only the body. Measure footerHeight in pixels using the active font and available width.
    /// The caller must provide enough window height for the footer plus a usable body.</summary>
    public static void BodyWithFooter(UiTheme theme, string id, float footerHeight, Action body, Action footer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(footer);
        if (!float.IsFinite(footerHeight) || footerHeight < 0) throw new ArgumentOutOfRangeException(nameof(footerHeight));
        var bodyHeight = Math.Max(1, ImGui.GetContentRegionAvail().Y - footerHeight - theme.Metrics.ItemSpacing.Y * ImGuiHelpers.GlobalScale);
        using (var child = ImRaii.Child($"body##{id}", new Vector2(0, bodyHeight), false))
        {
            if (child) body();
        }
        footer();
    }

    public static bool Section(UiTheme theme, string id, string label, string? count = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ImGui.Dummy(new Vector2(0, Math.Max(0, theme.Metrics.SectionGap * ImGuiHelpers.GlobalScale - ImGui.GetStyle().ItemSpacing.Y)));
        using var colors = ImRaii.PushColor(ImGuiCol.Text, theme.Colors.DarkForeground)
            .Push(ImGuiCol.Header, Vector4.Zero)
            .Push(ImGuiCol.HeaderHovered, UiStyleScope.Alpha(theme.Colors.Foreground, .08f));
        using var table = ImRaii.Table($"section##{id}", count == null ? 1 : 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings);
        if (!table) return false;
        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthStretch);
        if (count != null) ImGui.TableSetupColumn("Count", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(count).X);
        ImGui.TableNextColumn();
        var expanded = ImGui.CollapsingHeader($"{label.ToUpperInvariant()}###{id}", ImGuiTreeNodeFlags.DefaultOpen);
        if (count != null) { ImGui.TableNextColumn(); ImGui.TextUnformatted(count); }
        return expanded;
    }

    /// <summary>Auto-height, full-width rectangular block. The callback may return early or throw.</summary>
    public static void Surface(UiTheme theme, string id, Action content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(content);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, theme.Metrics.FramePadding * ImGuiHelpers.GlobalScale);
        using var border = ImRaii.PushColor(ImGuiCol.TableBorderStrong, UiStyleScope.Alpha(theme.Colors.Foreground, .4f));
        using var table = ImRaii.Table($"surface##{id}", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings);
        if (!table) return;
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(UiStyleScope.Opaque(theme.Colors.Raised)));
        ImGui.TableNextColumn();
        using var group = ImRaii.Group();
        content();
    }

    /// <summary>Full-width selectable row. Returns a click; the caller decides whether to change selection.</summary>
    public static bool SelectableRow(UiTheme theme, string id, string label, bool selected, string? metadata = null, bool enabled = true, string? disabledReason = null, IDalamudTextureWrap? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        using var disabled = ImRaii.Disabled(!enabled);
        using var colors = ImRaii.PushColor(ImGuiCol.Header, UiStyleScope.Alpha(theme.Colors.Foreground, .08f))
            .Push(ImGuiCol.HeaderHovered, UiStyleScope.Alpha(theme.Colors.Foreground, .12f))
            .Push(ImGuiCol.HeaderActive, UiStyleScope.Alpha(theme.Colors.Foreground, .22f));
        var height = Math.Max(theme.Metrics.RowHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());
        var contentStart = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Selectable($"##{id}", selected, ImGuiSelectableFlags.None, new Vector2(0, height));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var padding = theme.Metrics.CellPadding.X * ImGuiHelpers.GlobalScale;
        var textHeight = ImGui.GetTextLineHeight();
        var textY = (min.Y + max.Y - textHeight) / 2;
        var draw = ImGui.GetWindowDrawList();
        var markerSize = textHeight;
        // Selectable expands its highlight into item spacing. Anchor the marker to the
        // content instead, and give it a solid backing so hover cannot bleed through.
        var markerMin = new Vector2(contentStart.X + theme.Metrics.FramePadding.X * ImGuiHelpers.GlobalScale, (min.Y + max.Y - markerSize) / 2);
        var markerColor = ImGui.GetColorU32(selected ? theme.Colors.Accent : theme.Colors.Foreground);
        draw.AddRectFilled(markerMin, markerMin + new Vector2(markerSize), ImGui.GetColorU32(UiStyleScope.Opaque(theme.Colors.DarkBackground)));
        draw.AddRect(markerMin, markerMin + new Vector2(markerSize), markerColor, 0, ImDrawFlags.None, ImGuiHelpers.GlobalScale);
        if (selected)
        {
            var a = markerMin + new Vector2(.25f, .5f) * markerSize;
            var b = markerMin + new Vector2(.43f, .7f) * markerSize;
            var c = markerMin + new Vector2(.75f, .28f) * markerSize;
            draw.AddLine(a, b, markerColor, 1.5f * ImGuiHelpers.GlobalScale);
            draw.AddLine(b, c, markerColor, 1.5f * ImGuiHelpers.GlobalScale);
        }
        var gap = theme.Metrics.ItemInnerSpacing.X * ImGuiHelpers.GlobalScale;
        var iconMin = new Vector2(markerMin.X + markerSize + gap, textY);
        if (icon != null) draw.AddImage(icon.Handle, iconMin, iconMin + new Vector2(textHeight), Vector2.Zero, Vector2.One, ImGui.GetColorU32(Vector4.One));
        var labelLeft = iconMin.X + textHeight + gap;
        var labelRight = max.X - padding;
        if (!string.IsNullOrEmpty(metadata))
        {
            var width = Math.Min(ImGui.CalcTextSize(metadata).X, (max.X - min.X) * .45f);
            var left = max.X - padding - width;
            draw.PushClipRect(new Vector2(left, min.Y), max, true);
            try { draw.AddText(new Vector2(left, textY), ImGui.GetColorU32(theme.Colors.DarkForeground), metadata); }
            finally { draw.PopClipRect(); }
            labelRight = left - theme.Metrics.ItemInnerSpacing.X * ImGuiHelpers.GlobalScale;
        }

        draw.PushClipRect(new Vector2(Math.Min(labelLeft, max.X), min.Y), new Vector2(Math.Max(Math.Min(labelLeft, max.X), labelRight), max.Y), true);
        try { draw.AddText(new Vector2(labelLeft, textY), ImGui.GetColorU32(selected ? theme.Colors.Accent : theme.Colors.Foreground), label); }
        finally { draw.PopClipRect(); }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = string.IsNullOrEmpty(metadata) ? label : $"{label}\n{metadata}";
            if (!enabled && !string.IsNullOrWhiteSpace(disabledReason)) tooltip += $"\n{disabledReason}";
            ImGui.SetTooltip(tooltip);
        }
        return clicked;
    }

    public static bool Button(UiTheme theme, string id, string label, UiTone tone = UiTone.Primary, FontAwesomeIcon? icon = null, bool enabled = true, string? disabledReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        using var colors = ButtonColors(theme, tone);
        using var disabled = ImRaii.Disabled(!enabled);
        var height = Math.Max(theme.Metrics.ControlHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());
        var clicked = icon.HasValue
            ? ImGuiComponents.IconButtonWithText(icon.Value, $"{label}###{id}", new Vector2(0, height / ImGuiHelpers.GlobalScale))
            : ImGui.Button($"{label}###{id}", new Vector2(0, height));
        if (!enabled && !string.IsNullOrWhiteSpace(disabledReason) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(disabledReason);
        return clicked;
    }

    public static bool IconButton(UiTheme theme, string id, FontAwesomeIcon icon, string tooltip, UiTone tone = UiTone.Neutral, bool enabled = true, string? disabledReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(tooltip);
        using var colors = ButtonColors(theme, tone);
        using var disabled = ImRaii.Disabled(!enabled);
        var side = Math.Max(theme.Metrics.ControlHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());
        var clicked = ImGuiComponents.IconButton(id, icon, new Vector2(side, side) / ImGuiHelpers.GlobalScale);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(!enabled && !string.IsNullOrWhiteSpace(disabledReason) ? $"{tooltip}\n{disabledReason}" : tooltip);
        return clicked;
    }

    public static void Status(UiTheme theme, string label, UiTone tone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var color = theme.Color(tone);
        var cursor = ImGui.GetCursorScreenPos();
        var size = new Vector2(3 * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeight());
        ImGui.GetWindowDrawList().AddRectFilled(cursor, cursor + size, ImGui.GetColorU32(color));
        ImGui.Dummy(size);
        ImGui.SameLine();
        ImGui.TextColored(color, label);
    }

    public static void Progress(UiTheme theme, int completed, int total, UiTone tone = UiTone.Primary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        var fraction = ProgressFraction(completed, total);
        ImGui.TextUnformatted($"{Math.Clamp(completed, 0, total)}/{total}");
        var start = ImGui.GetCursorScreenPos();
        var size = new Vector2(Math.Max(1, ImGui.GetContentRegionAvail().X), 4 * ImGuiHelpers.GlobalScale);
        var color = theme.Color(tone);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + size, ImGui.GetColorU32(UiStyleScope.Alpha(color, .18f)));
        if (fraction > 0) draw.AddRectFilled(start, start + new Vector2(size.X * fraction, size.Y), ImGui.GetColorU32(color));
        ImGui.Dummy(size);
    }

    /// <summary>Caller controls opening, confirmation, and cancellation; this helper only balances popup styling.</summary>
    public static void Modal(UiTheme theme, string id, string title, ref bool open, Action content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(content);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, theme.Metrics.PopupPadding * ImGuiHelpers.GlobalScale);
        if (!ImGui.BeginPopupModal($"{title}###{id}", ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;
        try { content(); }
        finally { ImGui.EndPopup(); }
    }

    /// <summary>Uses Dalamud's shared icon cache. Borrowed wraps remain owned by Dalamud.</summary>
    public static void GameIcon(ITextureProvider textures, uint iconId)
    {
        ArgumentNullException.ThrowIfNull(textures);
        var size = new Vector2(ImGui.GetTextLineHeight());
        var wrap = iconId == 0 ? null : textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        if (wrap == null) ImGui.Dummy(size);
        else ImGui.Image(wrap.Handle, size);
    }

    public static float ButtonWidth(string label) => ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2;

    public static float ButtonHeight(UiTheme theme) => Math.Max(theme.Metrics.ControlHeight * ImGuiHelpers.GlobalScale, ImGui.GetFrameHeight());

    /// <summary>Solid accent-filled button for the single main action. Disabled buttons use the raised surface.</summary>
    public static bool PrimaryButton(UiTheme theme, string id, string label, bool enabled = true, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        using var disabled = ImRaii.Disabled(!enabled);
        using var colors = ImRaii.PushColor(ImGuiCol.Text, enabled ? theme.Colors.Background : theme.Colors.DarkForeground)
            .Push(ImGuiCol.Button, enabled ? theme.Colors.Accent : theme.Colors.Raised)
            .Push(ImGuiCol.ButtonHovered, Vector4.Lerp(theme.Colors.Accent, Vector4.One, .12f))
            .Push(ImGuiCol.ButtonActive, Vector4.Lerp(theme.Colors.Accent, Vector4.One, .22f));
        var clicked = ImGui.Button($"{label}###{id}", new Vector2(0, ButtonHeight(theme)));
        if (!enabled && reason != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reason);
        return clicked;
    }

    /// <summary>Zero-padding checkbox. <paramref name="mixed"/> draws an accent bar for partial selection.</summary>
    public static bool CompactCheckbox(UiTheme theme, string label, ref bool selected, bool mixed = false)
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

    /// <summary>Slightly enlarged, bright, wrapped headline text.</summary>
    public static void Emphasis(UiTheme theme, string text)
    {
        const float scale = 1.15f;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var size = ImGui.CalcTextSize(text, false, width / scale) * scale;
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize() * scale,
            ImGui.GetCursorScreenPos(), ImGui.GetColorU32(theme.Colors.BrightForeground), text, width);
        ImGui.Dummy(new Vector2(Math.Min(width, size.X), size.Y));
    }

    /// <summary>Wrapped secondary text in the dimmer foreground.</summary>
    public static void SecondaryText(UiTheme theme, string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, theme.Colors.DarkForeground);
        ImGui.TextWrapped(text);
    }

    internal static float ProgressFraction(int completed, int total) => total <= 0 ? 0 : Math.Clamp((float)completed / total, 0, 1);

    private static ImRaii.ColorDisposable ButtonColors(UiTheme theme, UiTone tone)
    {
        var color = tone == UiTone.Neutral ? theme.Colors.Foreground : theme.Color(tone);
        return ImRaii.PushColor(ImGuiCol.Text, color)
            .Push(ImGuiCol.Button, UiStyleScope.Alpha(color, .04f))
            .Push(ImGuiCol.ButtonHovered, UiStyleScope.Alpha(color, .08f))
            .Push(ImGuiCol.ButtonActive, UiStyleScope.Alpha(color, .22f))
            .Push(ImGuiCol.Border, UiStyleScope.Alpha(color, .4f));
    }
}
