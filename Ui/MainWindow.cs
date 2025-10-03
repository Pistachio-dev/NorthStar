using Dalamud.Bindings.ImGui;
using NorthStar.Ui.MainWindowTabs;
using System.Numerics;

namespace NorthStar.Ui;

internal class MainWindow
{
    private Plugin Plugin { get; }
    private List<ITab> Tabs { get; }

    internal bool Visible;
    internal uint ExtraMessages;

    internal MainWindow(Plugin plugin)
    {
        Plugin = plugin;
        Tabs = [
            new Write(Plugin),
            new MessageList(Plugin),
            new Settings(Plugin),
        ];
    }

    internal void Draw()
    {
        if (!Visible)
        {
            return;
        }

        if (ImGui.Button("Spawn on player position"))
        {
            Plugin.VfxSpawner.SpawnLightOnPlayerPosition();
        }

        ImGui.SetNextWindowSize(new Vector2(475, 350), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(Plugin.Name, ref Visible))
        {
            ImGui.End();
            return;
        }

        if (Plugin.Config.ApiKey == string.Empty)
        {
            DrawApiKey();
        }
        else
        {
            DrawTabs();
        }

        ImGui.End();
    }

    private void DrawTabs()
    {
        if (!ImGui.BeginTabBar("##ogt-main-tabs"))
        {
            return;
        }

        foreach (var tab in Tabs)
        {
            if (!ImGui.BeginTabItem(tab.Name))
            {
                continue;
            }

            if (ImGui.BeginChild("##tab-content"))
            {
                tab.Draw();
            }

            ImGui.EndChild();

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawApiKey()
    {
        ImGui.PushTextWrapPos();

        ImGui.TextUnformatted($"Somehow, {Plugin.Name} wasn't able to register you an account automatically.");
        ImGui.TextUnformatted("Click the button below to try again.");

        ImGui.PopTextWrapPos();

        if (ImGui.Button("Register"))
        {
            Plugin.GetApiKey();
        }
    }
}