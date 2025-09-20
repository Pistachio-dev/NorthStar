using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using OrangeGuidanceTomestone.Ui;

namespace OrangeGuidanceTomestone;

public class PluginUi : IDisposable {
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

    internal PluginUi(Plugin plugin) {
        this.Plugin = plugin;
        this.MainWindow = new MainWindow(this.Plugin);
        this.Viewer = new Viewer(this.Plugin);
        this.ViewerButton = new ViewerButton(this.Plugin);

        this.Plugin.Interface.UiBuilder.Draw += this.Draw;
        this.Plugin.Interface.UiBuilder.OpenConfigUi += this.OpenConfig;
    }

    public void Dispose() {
        this.Plugin.Interface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        this.Plugin.Interface.UiBuilder.Draw -= this.Draw;
    }

    private void OpenConfig() {
        this.MainWindow.Visible = true;
    }

    private void Draw() {
        if (this.Debug) {
            this.DrawDebug();
        }

        this.MainWindow.Draw();
        this.ViewerButton.Draw();
        this.Viewer.Draw();
        this.DrawModals();
    }

    private void DrawModals() {
        while (this.ToShow.TryDequeue(out var toShow)) {
            ImGui.OpenPopup($"{Plugin.Name}##{toShow}");
        }

        var toRemove = -1;
        for (var i = 0; i < this.Modals.Count; i++) {
            var (id, text) = this.Modals[i];
            if (!ImGui.BeginPopupModal($"{Plugin.Name}##{id}")) {
                continue;
            }

            ImGui.PushID(id);

            ImGui.TextUnformatted(text);
            ImGui.Separator();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.Button("Close")) {
                ImGui.CloseCurrentPopup();
                toRemove = i;
            }

            ImGui.PopID();

            ImGui.EndPopup();
        }

        if (toRemove > -1) {
            this.Modals.RemoveAt(toRemove);
        }
    }

    internal void ShowModal(string text) {
        this.ShowModal(Guid.NewGuid().ToString(), text);
    }

    internal void ShowModal(string id, string text) {
        this.Modals.Add((id, text));
        this.ToShow.Enqueue(id);
    }

    private void DrawDebug() {
        foreach (var msg in this.Plugin.Messages.CurrentCloned.Values) {
            if (!this.Plugin.GameGui.WorldToScreen(msg.Position, out var screen)) {
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
