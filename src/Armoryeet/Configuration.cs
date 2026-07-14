using System;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace Armoryeet;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool ScanOnLogin { get; set; }

    public bool ScanWhenOpened { get; set; } = true;

    public bool CloseAfterMove { get; set; }

    public bool RescanAfterMove { get; set; }

    public bool SkipMoveConfirmation { get; set; }

    public bool ShowDetailsByDefault { get; set; }

    public bool IncludeMainHand { get; set; } = true;

    public bool IncludeOffHand { get; set; } = true;

    public bool IncludeHead { get; set; } = true;

    public bool IncludeBody { get; set; } = true;

    public bool IncludeHands { get; set; } = true;

    public bool IncludeWaist { get; set; } = true;

    public bool IncludeLegs { get; set; } = true;

    public bool IncludeFeet { get; set; } = true;

    public bool IncludeEars { get; set; } = true;

    public bool IncludeNeck { get; set; } = true;

    public bool IncludeWrist { get; set; } = true;

    public bool IncludeRings { get; set; } = true;

    public int DelayBetweenItemsMs { get; set; } = 250;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
