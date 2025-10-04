using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using NorthStar.Ui.MainWindowTabs;
using System.Numerics;

namespace NorthStar.Ui;

internal class MainWindow : Window, IDisposable
{
    private Plugin Plugin { get; }
    private List<ITab> Tabs { get; }

    internal bool Visible;
    internal uint ExtraMessages;

    internal MainWindow(Plugin plugin):base("NorthStar")
    {
        Plugin = plugin;
        Tabs = [
            new Write(Plugin),
            new MessageList(Plugin),
            new Settings(Plugin),
        ];
    }

    public override void Draw()
    {
        if (ImGui.Button("Spawn on player position"))
        {
            Plugin.VfxSpawner.SpawnLightOnPlayerPosition();
        }
        if (ImGui.Button("Remove VFX"))
        {
            Plugin.VfxSpawner.DespawnAllVFX();
        }
        if (ImGui.Button("Print last coords"))
        {
            var coords = Plugin.ChatCoordsReader.LastCoords;
            if (coords == null)
            {
                Plugin.Log.Info("Coords are null");
                return;
            }

            Plugin.Log.Info($"X: {coords.RawX} Y: {coords.RawY} " +
                $"Map: {coords.PlaceName} " +
                $"Region: {coords.PlaceNameRegion} " +
                $"Terr: {coords.TerritoryType.RowId}");

        }

        if (ImGui.Button("Print player coords"))
        {
            Plugin.Log.Info(Plugin.ClientState.LocalPlayer.Position.ToString());
        }
    }


    public void Dispose()
    {
    }
}