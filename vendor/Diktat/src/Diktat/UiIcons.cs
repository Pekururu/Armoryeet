using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Diktat;

public enum UiIcon { Scan, Move, Protected, Stop, Settings, Appearance }

/// <summary>
/// Small action glyphs drawn from theme colors, never from baked ones.
/// Strokes use <see cref="UiColors.Foreground"/> and the single accent shape uses <see cref="UiColors.Accent"/>;
/// disabled icons use <see cref="UiColors.DarkForeground"/> for both. Shapes are straight-edged and drawn on a 256 grid,
/// so they follow UI scale. Contrast is covered by the Foreground/Accent readability warnings on window and raised surfaces.
/// An icon never carries state alone: pair it with a label, tooltip or marker.
/// </summary>
public static class UiIcons
{
    /// <summary>Draws the icon at the cursor and advances layout by a square. A <paramref name="size"/> of 0 uses the line height.</summary>
    public static void Draw(UiTheme theme, UiIcon icon, float size = 0, bool enabled = true)
    {
        if (!Enum.IsDefined(icon)) throw new ArgumentOutOfRangeException(nameof(icon));
        if (!float.IsFinite(size) || size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (size == 0) size = ImGui.GetTextLineHeight();
        var origin = ImGui.GetCursorScreenPos();
        var unit = size / 256f;
        var line = ImGui.GetColorU32(enabled ? theme.Colors.Foreground : theme.Colors.DarkForeground);
        var accent = ImGui.GetColorU32(enabled ? theme.Colors.Accent : theme.Colors.DarkForeground);
        var thickness = Math.Max(1, 12 * unit);
        var draw = ImGui.GetWindowDrawList();
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * unit;
        void Frame(float x0, float y0, float x1, float y1) => draw.AddRect(P(x0, y0), P(x1, y1), line, 0, ImDrawFlags.None, thickness);
        void Fill(float x0, float y0, float x1, float y1, uint color) => draw.AddRectFilled(P(x0, y0), P(x1, y1), color);
        // Straight strokes, extended by half the thickness so corners stay square.
        void Stroke(float x0, float y0, float x1, float y1)
        {
            var half = thickness / unit / 2;
            var dx = Math.Sign(x1 - x0) * half;
            var dy = Math.Sign(y1 - y0) * half;
            draw.AddLine(P(x0 - dx, y0 - dy), P(x1 + dx, y1 + dy), line, thickness);
        }

        switch (icon)
        {
            case UiIcon.Scan:
                foreach (var (cx, cy, sx, sy) in new[] { (28f, 28f, 1f, 1f), (228f, 28f, -1f, 1f), (228f, 228f, -1f, -1f), (28f, 228f, 1f, -1f) })
                {
                    Stroke(cx, cy, cx + 56 * sx, cy);
                    Stroke(cx, cy, cx, cy + 56 * sy);
                }
                Fill(68, 118, 188, 138, accent);
                break;
            case UiIcon.Move:
                Frame(36, 120, 220, 228);
                draw.AddTriangleFilled(P(128, 16), P(176, 72), P(80, 72), accent);
                Fill(110, 72, 146, 100, accent);
                break;
            case UiIcon.Protected:
                var shield = new[] { P(128, 20), P(216, 52), P(216, 128), P(128, 236), P(40, 128), P(40, 52) };
                for (var i = 0; i < shield.Length; i++) draw.AddLine(shield[i], shield[(i + 1) % shield.Length], line, thickness);
                draw.AddQuadFilled(P(128, 92), P(164, 128), P(128, 164), P(92, 128), accent);
                break;
            case UiIcon.Stop:
                Frame(44, 44, 212, 212);
                Fill(88, 88, 168, 168, accent);
                break;
            case UiIcon.Settings:
                foreach (var y in new[] { 64f, 128f, 192f }) Stroke(28, y, 228, y);
                Fill(66, 42, 94, 86, accent);
                Fill(146, 106, 174, 150, accent);
                Fill(96, 170, 124, 214, accent);
                break;
            case UiIcon.Appearance:
                Frame(36, 36, 220, 220);
                Fill(36, 36, 128, 220, line);
                Fill(156, 76, 196, 180, accent);
                break;
        }
        ImGui.Dummy(new Vector2(size, size));
    }
}
