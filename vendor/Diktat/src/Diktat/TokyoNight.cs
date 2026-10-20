using System.Numerics;

namespace Diktat;

public static class TokyoNight
{
    public static UiTheme Theme { get; } = new(new UiColors(
        Background: Rgb(0x1A1B26), DarkBackground: Rgb(0x13141C), DarkerBackground: Rgb(0x0E0E14),
        Raised: Rgb(0x24283B), Foreground: Rgb(0xA9B1D6), DarkForeground: Rgb(0x8C92AE),
        BrightForeground: Rgb(0xC0CAF5), Accent: Rgb(0x7AA2F7), Selection: Rgb(0x292E42), Muted: Rgb(0x414868),
        Danger: Rgb(0xF7768E), Warning: Rgb(0xE0AF68), Success: Rgb(0x9ECE6A), Info: Rgb(0x449DAB),
        Special: Rgb(0xAD8EE6), Orange: Rgb(0xEB927B)), new UiMetrics());

    private static Vector4 Rgb(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
}
