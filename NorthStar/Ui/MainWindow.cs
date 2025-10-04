using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NorthStar.Ui;

internal class MainWindow : Window, IDisposable
{
    private Plugin Plugin { get; }

    internal bool Visible;
    internal uint ExtraMessages;

    internal MainWindow(Plugin plugin) : base("NorthStar")
    {
        Plugin = plugin;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("This plugin draws a very easy to spot VFX on the position of the last <flag> or <pos> in chat");

        var enabled = Plugin.Config.Enabled;
        if (ImGui.Checkbox("Enable plugin", ref enabled))
        {
            Plugin.Config.Enabled = enabled;
        }
        var minDistancePillar = Plugin.Config.PillarOfLightMinDistance;
        if (ImGui.SliderFloat("Change to star VFX when at distance or closer:", ref minDistancePillar, 0, 1000))
        {
            Plugin.Config.PillarOfLightMinDistance = minDistancePillar;
        }
        var minDistanceStar = Plugin.Config.StarMinDistance;
        if (ImGui.SliderFloat("Change to no VFX when at distance or closer:", ref minDistanceStar, 0, 1000))
        {
            Plugin.Config.StarMinDistance = minDistanceStar;
        }

        var starOffset = Plugin.Config.StarHeightOffset;
        if (ImGui.SliderFloat("Star height offset:", ref starOffset, -200, 200))
        {
            Plugin.Config.StarHeightOffset = starOffset;
            Plugin.VfxSpawner.SpawnBeaconOnLastCoords();
        }
    }

    public void Dispose()
    {
    }
}