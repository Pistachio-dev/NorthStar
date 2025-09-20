using Dalamud.Bindings.ImGui;

namespace OrangeGuidanceTomestone.Ui;

internal class ViewerButton {
    private Plugin Plugin { get; }

    internal ViewerButton(Plugin plugin) {
        this.Plugin = plugin;
    }

    internal void Draw() {
        if (this.Plugin.Ui.Viewer.Visible) {
            return;
        }

        var nearby = this.Plugin.Messages.Nearby().ToList();
        if (nearby.Count == 0) {
            return;
        }

        if (this.Plugin.Config.AutoViewer) {
            this.Plugin.Ui.Viewer.Visible = true;
            return;
        }

        ImGui.SetNextWindowBgAlpha(0.5f);
        if (!ImGui.Begin("##ogt-viewer-button", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.End();
            return;
        }

        var label = "View message";
        if (nearby.Count > 1) {
            label += "s";
        }

        if (ImGui.Button(label)) {
            this.Plugin.Ui.Viewer.Visible = true;
        }

        ImGui.End();
    }
}
