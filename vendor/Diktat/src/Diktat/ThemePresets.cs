using System.Numerics;
using Dalamud.Interface.Style;

namespace Diktat;

public sealed record ThemePreset(string Id, string Name, UiTheme Theme);

/// <summary>Immutable color presets. Geometry and behavior are identical across presets.</summary>
public static class ThemePresets
{
    public const string DefaultId = "tokyo-night";

    public static IReadOnlyList<ThemePreset> All { get; } = Array.AsReadOnly<ThemePreset>(
    [
        new(DefaultId, "Tokyo Night", TokyoNight.Theme),
        new("dalamud-default", "Dalamud Default", CreateDalamudDefault()),
        // The reference Actions & Traits menu supplies charcoal surfaces, warm text,
        // and gold emphasis. Semantic states are complementary, not sampled game UI constants.
        new("ffxiv-menu", "FFXIV Menu", Create(
            0x313131, 0x262626, 0x202020, 0x494949, 0xDED5BD, 0xBFBCB3, 0xEEE4CB,
            0xD6B56C, 0x585345, 0x777166, 0xF0AEC0, 0xF0BD8A, 0xA9C493, 0x99C2CF, 0xB5A0C7, 0xD99B68)),
    ]);

    /// <summary>Unknown or missing saved IDs safely fall back to Tokyo Night.</summary>
    public static ThemePreset Find(string? id) => All.FirstOrDefault(preset => preset.Id == id) ?? All[0];

    private static UiTheme CreateDalamudDefault()
    {
        // Read built-in data only: never capture a plugin's pushed style, call Apply,
        // or read the user's configured host theme. Keep Diktat geometry/opacity.
        var standard = StyleModelV1.DalamudStandard;
        var states = standard.BuiltInColors;
        Vector4 Color(string name) => ThemePalette.Validate(standard.Colors[name]);
        Vector4 State(Vector4? value, uint fallback) => ThemePalette.Validate(value ?? ThemePalette.Rgb(fallback));
        // Curated text overrides retain the standard palette's character while meeting
        // 4.5:1 on its opaque raised surface. Do not adjust user-created palettes.
        return new UiTheme(new UiColors(
            Background: Color("WindowBg"), DarkBackground: Color("TitleBgActive"), DarkerBackground: Color("TitleBg"),
            Raised: Color("FrameBg"), Foreground: Color("Text"), DarkForeground: ThemePalette.Rgb(0xBDBDBD),
            BrightForeground: Color("Text"), Accent: Color("CheckMark"), Selection: Color("TabActive"), Muted: Color("ScrollbarGrab"),
            Danger: ThemePalette.Rgb(0xFFA5A5), Warning: State(states?.WarningForeground, 0xFFB500),
            Success: ThemePalette.Rgb(0x3BD855), Info: ThemePalette.Rgb(0x70C6FF),
            Special: State(states?.DalamudViolet, 0xC4B3F6), Orange: State(states?.DalamudOrange, 0xFFB500)), TokyoNight.Theme.Metrics);
    }

    private static UiTheme Create(uint background, uint darkBackground, uint darkerBackground, uint raised,
        uint foreground, uint darkForeground, uint brightForeground, uint accent, uint selection, uint muted,
        uint danger, uint warning, uint success, uint info, uint special, uint orange) => new(new UiColors(
            ThemePalette.Rgb(background), ThemePalette.Rgb(darkBackground), ThemePalette.Rgb(darkerBackground), ThemePalette.Rgb(raised),
            ThemePalette.Rgb(foreground), ThemePalette.Rgb(darkForeground), ThemePalette.Rgb(brightForeground), ThemePalette.Rgb(accent),
            ThemePalette.Rgb(selection), ThemePalette.Rgb(muted), ThemePalette.Rgb(danger), ThemePalette.Rgb(warning),
            ThemePalette.Rgb(success), ThemePalette.Rgb(info), ThemePalette.Rgb(special), ThemePalette.Rgb(orange)), TokyoNight.Theme.Metrics);
}
