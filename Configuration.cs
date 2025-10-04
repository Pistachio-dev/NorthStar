using Dalamud.Configuration;

namespace NorthStar;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string ApiKey { get; set; } = string.Empty;
    public HashSet<uint> BannedTerritories { get; set; } = [];
    public bool DisableTrials = true;
    public bool DisableDeepDungeon = true;
    public bool DisableInCutscene = true;
    public bool DisableInGpose = true;
    public bool RemoveGlow = true;
    public bool AutoViewer;
    public bool AutoViewerClose = true;
    public bool LockViewer;
    public bool ClickThroughViewer;
    public bool HideTitlebar;
    public bool ShowEmotes = true;
    public float EmoteAlpha = 25.0f;
    public float SignAlpha = 100.0f;
    public float SignRed = 100.0f;
    public float SignGreen = 100.0f;
    public float SignBlue = 100.0f;
    public float ViewerOpacity = 100.0f;
    public int DefaultGlyph = 3;

    public float PillarOfLightMinDistance = 100f;
    public float StarMinDistance = 40f;
    public float StarHeightOffset = -35f;
    public bool Enabled = true;
}