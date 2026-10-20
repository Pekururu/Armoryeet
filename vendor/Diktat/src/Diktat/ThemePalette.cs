using System.Globalization;
using System.Numerics;

namespace Diktat;

/// <summary>One token map shared by editing, reset, validation, and serialization.</summary>
internal static class ThemePalette
{
    internal sealed record Slot(string Key, string Label, string Group, Func<UiColors, Vector4> Get, Func<UiColors, Vector4, UiColors> Set);

    internal static readonly Slot[] Slots =
    [
        new("Background", "Window background", "Backgrounds", c => c.Background, (c, v) => c with { Background = v }),
        new("DarkBackground", "Deep surface / checkbox backing", "Backgrounds", c => c.DarkBackground, (c, v) => c with { DarkBackground = v }),
        new("DarkerBackground", "Scrollbar background", "Backgrounds", c => c.DarkerBackground, (c, v) => c with { DarkerBackground = v }),
        new("Raised", "Raised surface", "Backgrounds", c => c.Raised, (c, v) => c with { Raised = v }),
        new("Foreground", "Body text / borders", "Text", c => c.Foreground, (c, v) => c with { Foreground = v }),
        new("DarkForeground", "Secondary text", "Text", c => c.DarkForeground, (c, v) => c with { DarkForeground = v }),
        new("BrightForeground", "Title / emphasis", "Text", c => c.BrightForeground, (c, v) => c with { BrightForeground = v }),
        new("Accent", "Primary action / selection", "Accent & selection", c => c.Accent, (c, v) => c with { Accent = v }),
        new("Selection", "Selected tab surface", "Accent & selection", c => c.Selection, (c, v) => c with { Selection = v }),
        new("Muted", "Separators / inactive chrome", "Accent & selection", c => c.Muted, (c, v) => c with { Muted = v }),
        new("Danger", "Failure / blocked", "Status", c => c.Danger, (c, v) => c with { Danger = v }),
        new("Warning", "Warning / stop", "Status", c => c.Warning, (c, v) => c with { Warning = v }),
        new("Success", "Success / healthy", "Status", c => c.Success, (c, v) => c with { Success = v }),
        new("Info", "Information / protected", "Status", c => c.Info, (c, v) => c with { Info = v }),
        new("Special", "Secondary emphasis", "Status", c => c.Special, (c, v) => c with { Special = v }),
        new("Orange", "Reserved warm accent", "Status", c => c.Orange, (c, v) => c with { Orange = v }),
    ];

    internal static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);

    internal static bool TryParse(string? text, out Vector4 color)
    {
        color = default;
        if (text == null || text.Length != 7 || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)) return false;
        color = Rgb(rgb);
        return true;
    }

    internal static string Hex(Vector4 color) => $"#{(int)MathF.Round(color.X * 255):X2}{(int)MathF.Round(color.Y * 255):X2}{(int)MathF.Round(color.Z * 255):X2}";

    internal static Vector4 Validate(Vector4 color)
    {
        if (!float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) ||
            color.X < 0 || color.X > 1 || color.Y < 0 || color.Y > 1 || color.Z < 0 || color.Z > 1)
            throw new ArgumentOutOfRangeException(nameof(color), "RGB components must be finite and between 0 and 1.");
        // Preferences use RGB bytes; quantize now so saved and previewed colors match exactly.
        return new Vector4(MathF.Round(color.X * 255) / 255, MathF.Round(color.Y * 255) / 255, MathF.Round(color.Z * 255) / 255, 1);
    }

    internal static IReadOnlyList<string> Warnings(UiColors colors)
    {
        var warnings = new List<string>();
        foreach (var (name, text) in new[]
        {
            ("Body text", colors.Foreground), ("Secondary text", colors.DarkForeground), ("Title", colors.BrightForeground),
            ("Accent", colors.Accent), ("Failure", colors.Danger), ("Warning", colors.Warning), ("Success", colors.Success), ("Info", colors.Info),
        })
        {
            var ratio = Math.Min(Contrast(text, colors.Background), Contrast(text, colors.Raised));
            if (ratio < 4.5f) warnings.Add($"{name}: contrast {ratio:0.0}:1 on window/raised surfaces (aim for 4.5:1).");
        }
        if (Contrast(colors.Accent, colors.DarkBackground) < 3 || Contrast(colors.Foreground, colors.DarkBackground) < 3)
            warnings.Add("Checkbox outlines may blend into their backing (aim for 3:1).");
        var states = new[] { ("Failure", colors.Danger), ("Warning", colors.Warning), ("Success", colors.Success), ("Info", colors.Info), ("Active", colors.Accent) };
        for (var i = 0; i < states.Length; i++)
        for (var j = i + 1; j < states.Length; j++)
            if (Vector4.Distance(states[i].Item2, states[j].Item2) < .15f)
                warnings.Add($"{states[i].Item1} and {states[j].Item1} colors are very similar; keep text and icons distinct.");
        return warnings;
    }

    internal static float Contrast(Vector4 a, Vector4 b)
    {
        static float Linear(float channel) => channel <= .04045f ? channel / 12.92f : MathF.Pow((channel + .055f) / 1.055f, 2.4f);
        static float Luminance(Vector4 c) => .2126f * Linear(c.X) + .7152f * Linear(c.Y) + .0722f * Linear(c.Z);
        var x = Luminance(a);
        var y = Luminance(b);
        return (Math.Max(x, y) + .05f) / (Math.Min(x, y) + .05f);
    }
}
