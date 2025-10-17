using Dalamud.Configuration;

namespace NorthStar;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool RemoveGlow = true;
    public float SignAlpha = 100.0f;
    public float SignRed = 100.0f;
    public float SignGreen = 100.0f;
    public float SignBlue = 100.0f;
    public float PillarOfLightMinDistance = 100f;
    public float StarMinDistance = 40f;
    public float StarHeightOffset = -35f;
    public int RefreshInterval = 30;
    public bool Enabled = true;
}