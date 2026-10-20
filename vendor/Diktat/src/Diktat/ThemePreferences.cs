namespace Diktat;

/// <summary>Serializer-friendly color preferences. The consuming plugin owns storage.
/// Null CustomColors means the built-in preset; otherwise entries are opaque #RRGGBB values keyed by UiColors property name.</summary>
public sealed class ThemePreferences
{
    public string PresetId { get; set; } = ThemePresets.DefaultId;
    public Dictionary<string, string>? CustomColors { get; set; }
}
