using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using NorthStar.Ui;
using System.Numerics;

namespace NorthStar;

public class PluginUi : IDisposable
{
    private Plugin Plugin { get; }

    internal MainWindow MainWindow { get; }
    internal Viewer Viewer { get; }
    internal ViewerButton ViewerButton { get; }
#if DEBUG
    internal bool Debug = true;
#else
    internal bool Debug;
#endif

    private List<(string, string)> Modals { get; } = [];
    private Queue<string> ToShow { get; } = new();

    internal PluginUi(Plugin plugin)
    {
        Plugin = plugin;
        MainWindow = new MainWindow(Plugin);
        Viewer = new Viewer(Plugin);
        ViewerButton = new ViewerButton(Plugin);

        Plugin.Interface.UiBuilder.Draw += Draw;
        Plugin.Interface.UiBuilder.OpenConfigUi += OpenConfig;
    }

    public void Dispose()
    {
        Plugin.Interface.UiBuilder.OpenConfigUi -= OpenConfig;
        Plugin.Interface.UiBuilder.Draw -= Draw;
    }

    private void OpenConfig()
    {
        MainWindow.Visible = true;
    }

    private void Draw()
    {
        if (Debug)
        {
            DrawDebug();
        }

        MainWindow.Draw();
        ViewerButton.Draw();
        Viewer.Draw();
        DrawModals();
    }

    private void DrawModals()
    {
        while (ToShow.TryDequeue(out var toShow))
        {
            ImGui.OpenPopup($"{Plugin.Name}##{toShow}");
        }

        var toRemove = -1;
        for (var i = 0; i < Modals.Count; i++)
        {
            var (id, text) = Modals[i];
            if (!ImGui.BeginPopupModal($"{Plugin.Name}##{id}"))
            {
                continue;
            }

            ImGui.PushID(id);

            ImGui.TextUnformatted(text);
            ImGui.Separator();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.Button("Close"))
            {
                ImGui.CloseCurrentPopup();
                toRemove = i;
            }

            ImGui.PopID();

            ImGui.EndPopup();
        }

        if (toRemove > -1)
        {
            Modals.RemoveAt(toRemove);
        }
    }

    internal void ShowModal(string text)
    {
        ShowModal(Guid.NewGuid().ToString(), text);
    }

    internal void ShowModal(string id, string text)
    {
        Modals.Add((id, text));
        ToShow.Enqueue(id);
    }

    private void DrawDebug()
    {
        foreach (var msg in Plugin.Messages.CurrentCloned.Values)
        {
            if (!Plugin.GameGui.WorldToScreen(msg.Position, out var screen))
            {
                continue;
            }

            ImGui.GetBackgroundDrawList().AddCircleFilled(screen, 6f * ImGuiHelpers.GlobalScale, 0xff0000ff);

            var label = msg.Id.ToString("N");
            var size = ImGui.CalcTextSize(label);
            ImGui.GetBackgroundDrawList().AddRectFilled(
                screen - Vector2.One * 4 * ImGuiHelpers.GlobalScale,
                screen + size + Vector2.One * 4 * ImGuiHelpers.GlobalScale,
                0xff000000
            );
            ImGui.GetBackgroundDrawList().AddText(screen, 0xffffffff, label);
        }
    }
}