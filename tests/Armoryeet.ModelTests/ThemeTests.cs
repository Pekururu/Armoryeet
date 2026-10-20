using Armoryeet;
using Newtonsoft.Json;
using QuickUI;

internal static class ThemeTests
{
    public static void Run()
    {
        foreach (var preset in ThemePresets.All)
            Check(new ThemeEditor(new ThemePreferences { PresetId = preset.Id }).Warnings.Count == 0,
                $"Bundled preset {preset.Name} must pass readability checks.");
        var configuration = JsonConvert.DeserializeObject<Configuration>("""{"Version":1,"IncludeHead":false,"DelayBetweenItemsMs":700,"SkipMoveConfirmation":true}""")!;
        Check(configuration.Theme.PresetId == ThemePresets.DefaultId, "Existing configurations must default to Tokyo Night.");
        var editor = new ThemeEditor(configuration.Theme);
        editor.SelectPreset("ffxiv-menu");
        Configuration? saved = null;
        editor.Apply(preferences => configuration.SaveTheme(preferences, next => saved = next));
        Check(saved != null && !ReferenceEquals(saved, configuration), "Theme saves must persist a detached configuration.");
        var loaded = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(saved))!;
        Check(!loaded.IncludeHead && loaded.DelayBetweenItemsMs == 700 && loaded.SkipMoveConfirmation,
            "Applying a theme must preserve existing behavior settings.");
        Check(loaded.Theme.PresetId == "ffxiv-menu" && configuration.Theme.PresetId == "ffxiv-menu", "Applied theme must persist and update current consumers.");

        editor.SelectPreset("dalamud-default");
        try
        {
            editor.Apply(preferences => configuration.SaveTheme(preferences, _ => throw new IOException("Simulated save failure")));
            throw new InvalidOperationException("Save failures must propagate to the editor.");
        }
        catch (IOException) { }
        Check(configuration.Theme.PresetId == "ffxiv-menu" && editor.IsDirty, "Failed save must retain the applied theme and editable draft.");
        editor.Cancel();
        Check(editor.PresetId == "ffxiv-menu" && !editor.IsDirty, "Cancel after failed save must restore the applied theme.");

        editor.Customize();
        editor.SetColor(nameof(UiColors.Accent), new System.Numerics.Vector4(1, 0, 0, 1));
        editor.Apply(preferences => configuration.SaveTheme(preferences, next => saved = next));
        loaded = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(saved))!;
        Check(loaded.Theme.CustomColors![nameof(UiColors.Accent)] == "#FF0000", "Custom colors must round-trip through Dalamud's JSON serializer.");
        editor.SelectPreset("tokyo-night");
        using var settings = new ConfigUI(configuration, () => { }, editor, _ => throw new InvalidOperationException("Closing must not save."));
        settings.OnClose();
        Check(editor.PresetId == "ffxiv-menu" && editor.IsCustom && !editor.IsDirty, "Closing Settings must discard the draft and retain applied custom colors.");
        settings.SetAppearanceActive(true);
        editor.SelectPreset("tokyo-night");
        settings.SetAppearanceActive(false);
        Check(editor.PresetId == "ffxiv-menu" && editor.IsCustom && !editor.IsDirty,
            "Leaving Appearance must discard unapplied changes.");
        settings.SetAppearanceActive(true);
        editor.SelectPreset("dalamud-default");
        editor.Apply(preferences => configuration.SaveTheme(preferences, next => saved = next));
        settings.SetAppearanceActive(false);
        Check(editor.PresetId == "dalamud-default" && configuration.Theme.PresetId == "dalamud-default",
            "Leaving Appearance must preserve an applied theme.");
        var nullTheme = JsonConvert.DeserializeObject<Configuration>("""{"Theme":null}""")!;
        Check(new ThemeEditor(nullTheme.Theme).PresetId == ThemePresets.DefaultId, "Null preferences must load safely.");
        Console.WriteLine("PASS: theme compatibility, persistence, failed save, and close cancellation");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
