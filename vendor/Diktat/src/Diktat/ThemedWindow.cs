using Dalamud.Interface.Windowing;

namespace Diktat;

/// <summary>A WindowSystem window whose styling is confined to PreDraw/PostDraw.</summary>
public abstract class ThemedWindow : Window
{
    private UiStyleScope? scope;
    private UiTheme? pendingTheme;

    protected ThemedWindow(string name, UiTheme? theme = null) : base(name)
    {
        this.Theme = theme ?? TokyoNight.Theme;
        this.BgAlpha = 1;
        this.AllowBackgroundBlur = false;
        this.DisableFadeInFadeOut = true;
    }

    public UiTheme Theme { get; private set; }

    /// <summary>Queues a theme for the next PreDraw. Call on the drawing thread; the current draw stays consistent.</summary>
    public void SetTheme(UiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Colors);
        ArgumentNullException.ThrowIfNull(theme.Metrics);
        this.pendingTheme = theme;
    }

    public sealed override void PreDraw()
    {
        if (this.scope != null) throw new InvalidOperationException("The previous Diktat window style scope was not closed.");
        this.Theme = this.pendingTheme ?? this.Theme;
        this.pendingTheme = null;
        this.scope = new UiStyleScope(this.Theme);
    }

    // WindowSystem calls PostDraw after End, including collapsed windows and exceptions from Draw.
    // Popping in Draw's catch/finally would incorrectly pop WindowSystem's own intervening styles.
    public sealed override void PostDraw()
    {
        try { this.scope?.Dispose(); }
        finally { this.scope = null; }
    }

    public sealed override void Draw() => this.DrawContent();
    protected abstract void DrawContent();
}
