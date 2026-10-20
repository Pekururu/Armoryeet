using System.Numerics;

namespace Diktat;

/// <summary>Immutable, plugin-independent colors and layout metrics.</summary>
public sealed record UiTheme(UiColors Colors, UiMetrics Metrics)
{
    public Vector4 Color(UiTone tone) => tone switch
    {
        UiTone.Neutral => this.Colors.DarkForeground,
        UiTone.Primary => this.Colors.Accent,
        UiTone.Warning => this.Colors.Warning,
        UiTone.Danger => this.Colors.Danger,
        UiTone.Success => this.Colors.Success,
        UiTone.Info => this.Colors.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(tone)),
    };
}

public enum UiTone { Neutral, Primary, Warning, Danger, Success, Info }

/// <summary>A display value; the consuming plugin owns its calculation.</summary>
public readonly record struct UiMetric(string Label, string Value, UiTone Tone = UiTone.Neutral);

public sealed record UiColors(
    Vector4 Background, Vector4 DarkBackground, Vector4 DarkerBackground,
    Vector4 Raised, Vector4 Foreground, Vector4 DarkForeground,
    Vector4 BrightForeground, Vector4 Accent, Vector4 Selection, Vector4 Muted,
    Vector4 Danger, Vector4 Warning, Vector4 Success, Vector4 Info,
    Vector4 Special, Vector4 Orange);

/// <summary>Unscaled sizes; controls grow when the active Dalamud font needs more room.</summary>
public sealed record UiMetrics
{
    public Vector2 WindowPadding { get; init; } = new(18, 18);
    public Vector2 PopupPadding { get; init; } = new(14, 14);
    public Vector2 FramePadding { get; init; } = new(10, 6);
    public Vector2 ItemSpacing { get; init; } = new(8, 6);
    public Vector2 ItemInnerSpacing { get; init; } = new(6, 4);
    public Vector2 CellPadding { get; init; } = new(4, 4);
    public float ControlHeight { get; init; } = 28;
    public float RowHeight { get; init; } = 28;
    public float SectionGap { get; init; } = 12;
}
