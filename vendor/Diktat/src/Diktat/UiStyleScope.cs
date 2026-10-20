using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Diktat;

/// <summary>Use on the ImGui draw thread and dispose in reverse nesting order, within the same frame.</summary>
public sealed class UiStyleScope : IDisposable
{
    private readonly ImRaii.ColorDisposable colors = new();
    private readonly ImRaii.StyleDisposable styles = new();

    public UiStyleScope(UiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Colors);
        ArgumentNullException.ThrowIfNull(theme.Metrics);
        var c = theme.Colors;
        var m = theme.Metrics;
        var scale = ImGuiHelpers.GlobalScale;
        try
        {
            this.colors
                .Push(ImGuiCol.Text, c.Foreground).Push(ImGuiCol.TextDisabled, c.DarkForeground)
                .Push(ImGuiCol.WindowBg, Opaque(c.Background)).Push(ImGuiCol.ChildBg, Opaque(c.Background))
                .Push(ImGuiCol.PopupBg, Opaque(c.Background)).Push(ImGuiCol.Border, Alpha(c.Foreground, .4f))
                .Push(ImGuiCol.BorderShadow, Vector4.Zero)
                .Push(ImGuiCol.FrameBg, Alpha(c.Foreground, .04f)).Push(ImGuiCol.FrameBgHovered, Alpha(c.Foreground, .08f))
                .Push(ImGuiCol.FrameBgActive, Alpha(c.Foreground, .22f))
                .Push(ImGuiCol.TitleBg, Opaque(c.DarkBackground)).Push(ImGuiCol.TitleBgActive, Opaque(c.DarkBackground))
                .Push(ImGuiCol.TitleBgCollapsed, Opaque(c.DarkBackground)).Push(ImGuiCol.MenuBarBg, Opaque(c.DarkBackground))
                .Push(ImGuiCol.ScrollbarBg, Opaque(c.DarkerBackground)).Push(ImGuiCol.ScrollbarGrab, c.Muted)
                .Push(ImGuiCol.ScrollbarGrabHovered, c.DarkForeground).Push(ImGuiCol.ScrollbarGrabActive, c.Accent)
                .Push(ImGuiCol.CheckMark, c.Accent).Push(ImGuiCol.SliderGrab, c.Accent).Push(ImGuiCol.SliderGrabActive, c.BrightForeground)
                .Push(ImGuiCol.Button, Alpha(c.Foreground, .04f)).Push(ImGuiCol.ButtonHovered, Alpha(c.Foreground, .08f))
                .Push(ImGuiCol.ButtonActive, Alpha(c.Foreground, .22f))
                .Push(ImGuiCol.Header, Alpha(c.Foreground, .08f)).Push(ImGuiCol.HeaderHovered, Alpha(c.Foreground, .12f))
                .Push(ImGuiCol.HeaderActive, Alpha(c.Foreground, .22f))
                .Push(ImGuiCol.Separator, c.Muted).Push(ImGuiCol.SeparatorHovered, c.Accent).Push(ImGuiCol.SeparatorActive, c.Accent)
                .Push(ImGuiCol.ResizeGrip, Alpha(c.Foreground, .04f)).Push(ImGuiCol.ResizeGripHovered, Alpha(c.Accent, .25f))
                .Push(ImGuiCol.ResizeGripActive, c.Accent)
                .Push(ImGuiCol.Tab, Opaque(c.DarkBackground)).Push(ImGuiCol.TabHovered, Alpha(c.Accent, .18f))
                .Push(ImGuiCol.TabActive, Opaque(c.Selection)).Push(ImGuiCol.TabUnfocused, Opaque(c.DarkBackground))
                .Push(ImGuiCol.TabUnfocusedActive, Opaque(c.Raised))
                .Push(ImGuiCol.DockingPreview, Alpha(c.Accent, .35f)).Push(ImGuiCol.DockingEmptyBg, Opaque(c.DarkerBackground))
                .Push(ImGuiCol.PlotLines, c.Info).Push(ImGuiCol.PlotLinesHovered, c.Accent)
                .Push(ImGuiCol.PlotHistogram, c.Info).Push(ImGuiCol.PlotHistogramHovered, c.Accent)
                .Push(ImGuiCol.TableHeaderBg, Opaque(c.Raised)).Push(ImGuiCol.TableBorderStrong, Alpha(c.Foreground, .4f))
                .Push(ImGuiCol.TableBorderLight, c.Muted).Push(ImGuiCol.TableRowBg, Vector4.Zero)
                .Push(ImGuiCol.TableRowBgAlt, Alpha(c.Foreground, .04f)).Push(ImGuiCol.TextSelectedBg, Alpha(c.Accent, .35f))
                .Push(ImGuiCol.DragDropTarget, c.Accent).Push(ImGuiCol.NavHighlight, c.Accent)
                .Push(ImGuiCol.NavWindowingHighlight, c.Accent).Push(ImGuiCol.NavWindowingDimBg, Alpha(c.DarkerBackground, .65f))
                .Push(ImGuiCol.ModalWindowDimBg, Alpha(c.DarkerBackground, .65f));

            this.styles
                .Push(ImGuiStyleVar.WindowRounding, 0).Push(ImGuiStyleVar.ChildRounding, 0)
                .Push(ImGuiStyleVar.FrameRounding, 0).Push(ImGuiStyleVar.PopupRounding, 0)
                .Push(ImGuiStyleVar.GrabRounding, 0).Push(ImGuiStyleVar.TabRounding, 0).Push(ImGuiStyleVar.ScrollbarRounding, 0)
                .Push(ImGuiStyleVar.WindowBorderSize, scale).Push(ImGuiStyleVar.ChildBorderSize, scale)
                .Push(ImGuiStyleVar.PopupBorderSize, scale).Push(ImGuiStyleVar.FrameBorderSize, scale)
                .Push(ImGuiStyleVar.WindowPadding, m.WindowPadding * scale).Push(ImGuiStyleVar.FramePadding, m.FramePadding * scale)
                .Push(ImGuiStyleVar.ItemSpacing, m.ItemSpacing * scale).Push(ImGuiStyleVar.ItemInnerSpacing, m.ItemInnerSpacing * scale)
                .Push(ImGuiStyleVar.CellPadding, m.CellPadding * scale).Push(ImGuiStyleVar.DisabledAlpha, .65f)
                .Push(ImGuiStyleVar.ScrollbarSize, m.ScrollbarSize * scale);
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        this.styles.Dispose();
        this.colors.Dispose();
    }

    internal static Vector4 Alpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);
    internal static Vector4 Opaque(Vector4 color) => Alpha(color, 1);
}
