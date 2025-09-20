using Dalamud.Bindings.ImGui;
using System.Numerics;
using OrangeGuidanceTomestone.Ui.MainWindowTabs;

namespace OrangeGuidanceTomestone.Ui;

internal class MainWindow {
    private Plugin Plugin { get; }
    private List<ITab> Tabs { get; }

    internal bool Visible;
    internal uint ExtraMessages;

    internal MainWindow(Plugin plugin) {
        this.Plugin = plugin;
        this.Tabs = [
            new Write(this.Plugin),
            new MessageList(this.Plugin),
            new Settings(this.Plugin),
        ];
    }

    internal void Draw() {
        if (!this.Visible) {
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(475, 350), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(Plugin.Name, ref this.Visible)) {
            ImGui.End();
            return;
        }

        if (this.Plugin.Config.ApiKey == string.Empty) {
            this.DrawApiKey();
        } else {
            this.DrawTabs();
        }

        ImGui.End();
    }

    private void DrawTabs() {
        if (!ImGui.BeginTabBar("##ogt-main-tabs")) {
            return;
        }

        foreach (var tab in this.Tabs) {
            if (!ImGui.BeginTabItem(tab.Name)) {
                continue;
            }

            if (ImGui.BeginChild("##tab-content")) {
                tab.Draw();
            }

            ImGui.EndChild();

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawApiKey() {
        ImGui.PushTextWrapPos();

        ImGui.TextUnformatted($"Somehow, {Plugin.Name} wasn't able to register you an account automatically.");
        ImGui.TextUnformatted("Click the button below to try again.");

        ImGui.PopTextWrapPos();

        if (ImGui.Button("Register")) {
            this.Plugin.GetApiKey();
        }
    }
}
