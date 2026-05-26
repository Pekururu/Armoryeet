using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using SaveConfigurationDelegate = System.Action;

namespace Armoryeet;

public sealed class ConfigUI : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly SaveConfigurationDelegate saveConfiguration;

    public ConfigUI(Configuration configuration, SaveConfigurationDelegate saveConfiguration)
        : base("Armoryeet Settings###ArmoryeetSettings")
    {
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var scanWhenOpened = this.configuration.ScanWhenOpened;
        if (ImGui.Checkbox("Scan when opened", ref scanWhenOpened))
        {
            this.configuration.ScanWhenOpened = scanWhenOpened;
            this.saveConfiguration();
        }

        var scanOnLogin = this.configuration.ScanOnLogin;
        if (ImGui.Checkbox("Scan on login", ref scanOnLogin))
        {
            this.configuration.ScanOnLogin = scanOnLogin;
            this.saveConfiguration();
        }

        var closeAfterMove = this.configuration.CloseAfterMove;
        if (ImGui.Checkbox("Close after move", ref closeAfterMove))
        {
            this.configuration.CloseAfterMove = closeAfterMove;
            this.saveConfiguration();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Containers");

        this.DrawContainerCheckbox("Main hand", "/yeet mainhand", this.configuration.IncludeMainHand, value => this.configuration.IncludeMainHand = value);
        this.DrawContainerCheckbox("Off hand", "/yeet offhand", this.configuration.IncludeOffHand, value => this.configuration.IncludeOffHand = value);
        this.DrawContainerCheckbox("Head", "/yeet head", this.configuration.IncludeHead, value => this.configuration.IncludeHead = value);
        this.DrawContainerCheckbox("Body", "/yeet body", this.configuration.IncludeBody, value => this.configuration.IncludeBody = value);
        this.DrawContainerCheckbox("Hands", "/yeet hands", this.configuration.IncludeHands, value => this.configuration.IncludeHands = value);
        this.DrawContainerCheckbox("Waist", "/yeet waist", this.configuration.IncludeWaist, value => this.configuration.IncludeWaist = value);
        this.DrawContainerCheckbox("Legs", "/yeet legs", this.configuration.IncludeLegs, value => this.configuration.IncludeLegs = value);
        this.DrawContainerCheckbox("Feet", "/yeet feet", this.configuration.IncludeFeet, value => this.configuration.IncludeFeet = value);
        this.DrawContainerCheckbox("Ears", "/yeet ears", this.configuration.IncludeEars, value => this.configuration.IncludeEars = value);
        this.DrawContainerCheckbox("Neck", "/yeet neck", this.configuration.IncludeNeck, value => this.configuration.IncludeNeck = value);
        this.DrawContainerCheckbox("Wrist", "/yeet wrist", this.configuration.IncludeWrist, value => this.configuration.IncludeWrist = value);
        this.DrawContainerCheckbox("Rings", "/yeet rings", this.configuration.IncludeRings, value => this.configuration.IncludeRings = value);

        ImGui.Separator();
        ImGui.TextDisabled("Movement");

        var delayBetweenItemsMs = this.configuration.DelayBetweenItemsMs;
        if (ImGui.SliderInt("Delay between items", ref delayBetweenItemsMs, 0, 2000, "%d ms"))
        {
            this.configuration.DelayBetweenItemsMs = delayBetweenItemsMs;
            this.saveConfiguration();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Soul crystals are always protected.");
    }

    private void DrawContainerCheckbox(string label, string command, bool currentValue, Action<bool> setter)
    {
        var value = currentValue;
        if (ImGui.Checkbox(label, ref value))
        {
            setter(value);
            this.saveConfiguration();
        }

        ImGui.SameLine(180);
        ImGui.TextDisabled(command);
    }
}
