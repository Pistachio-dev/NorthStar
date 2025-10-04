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
    }


    public void Dispose()
    {
    }
}