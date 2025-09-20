using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;
using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class MessageList : ITab {
    public string Name => "Your messages";
    private Plugin Plugin { get; }
    private SortMode Sort { get; set; }

    private SemaphoreSlim MessagesMutex { get; } = new(1, 1);
    private List<MessageWithTerritory> Messages { get; } = [];

    internal MessageList(Plugin plugin) {
        this.Plugin = plugin;
    }

    public void Dispose() {
    }

    public void Draw() {
        if (ImGui.Button("Refresh")) {
            this.Refresh();
        }

        ImGui.SameLine();

        if (ImGui.BeginCombo("Sort", $"{this.Sort}")) {
            foreach (var mode in Enum.GetValues<SortMode>()) {
                if (ImGui.Selectable($"{mode}", mode == this.Sort)) {
                    this.Sort = mode;
                }
            }

            ImGui.EndCombo();
        }

        this.MessagesMutex.Wait();
        try {
            this.ShowList();
        } finally {
            this.MessagesMutex.Release();
        }
    }

    private void ShowList() {
        ImGui.TextUnformatted($"Messages: {this.Messages.Count:N0} / {OrangeGuidanceTomestone.Messages.MaxAmount + this.Plugin.Ui.MainWindow.ExtraMessages:N0}");

        ImGui.Separator();

        if (ImGui.BeginChild("##messages-list")) {
            var messages = this.Messages;
            if (this.Sort != SortMode.Date) {
                messages = messages.ToList();
                messages.Sort((a, b) => {
                    return this.Sort switch {
                        SortMode.Date => 0,
                        SortMode.Appraisals => Math.Max(b.PositiveVotes - b.NegativeVotes, 0)
                            .CompareTo(Math.Max(a.PositiveVotes - a.NegativeVotes, 0)),
                        SortMode.Likes => b.PositiveVotes.CompareTo(a.PositiveVotes),
                        SortMode.Dislikes => b.NegativeVotes.CompareTo(a.NegativeVotes),
                        SortMode.Location => a.Territory.CompareTo(b.Territory),
                        _ => throw new ArgumentOutOfRangeException(),
                    };
                });
            }

            foreach (var message in messages) {
                var territory = this.Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(message.Territory);
                var territoryName = territory?.PlaceName.Value.Name.ToDalamudString().TextValue ?? "???";

                var loc = $"Location: {territoryName}";
                if (message.Ward != null) {
                    loc += " (";

                    if (message.World != null) {
                        var world = this.Plugin.DataManager.GetExcelSheet<World>().GetRowOrDefault((ushort) message.World);
                        if (world != null) {
                            loc += world.Value.Name.ToDalamudString().TextValue;
                        }
                    }

                    if (loc != " (") {
                        loc += ", ";
                    }

                    loc += $"Ward {message.Ward.Value}";

                    if (message.Plot != null) {
                        if (message.Plot.Value >= HousingLocationExt.Apt) {
                            var apartment = message.Plot.Value - 10_000;
                            var wing = apartment < HousingLocationExt.Wng ? 1 : 2;
                            var apt = wing == 2 ? apartment - HousingLocationExt.Wng : apartment;

                            if (apt == 0) {
                                loc += $", Wing {wing}, Lobby";
                            } else {
                                loc += $", Wing {wing}, Apt. {apt}";
                            }
                        } else {
                            loc += $", Plot {message.Plot.Value}";
                        }
                    }

                    loc += ")";
                }

                ImGui.TextUnformatted(message.Text);
                ImGui.TreePush("location");
                using (new OnDispose(ImGui.TreePop)) {
                    ImGui.TextUnformatted(loc);
                    ImGui.SameLine();

                    if (ImGuiHelper.SmallIconButton(FontAwesomeIcon.MapMarkerAlt, $"{message.Id}") && territory != null) {
                        this.Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(
                            territory.Value.RowId,
                            territory.Value.Map.RowId,
                            (int) (message.X * 1_000),
                            (int) (message.Z * 1_000)
                        ));
                    }

                    if (message.IsHidden) {
                        ImGuiHelper.WarningText("This message will not be shown to other players due to its low score.");
                    }

                    var ctrl = ImGui.GetIO().KeyCtrl;

                    var appraisals = Math.Max(0, message.PositiveVotes - message.NegativeVotes);
                    ImGui.TextUnformatted($"Appraisals: {appraisals:N0} ({message.PositiveVotes:N0} - {message.NegativeVotes:N0})");

                    if (!ctrl) {
                        ImGui.BeginDisabled();
                    }

                    try {
                        if (ImGui.Button($"Delete##{message.Id}")) {
                            this.Delete(message.Id);
                        }
                    } finally {
                        if (!ctrl) {
                            ImGui.EndDisabled();
                        }
                    }

                    ImGui.SameLine();
                    ImGuiHelper.HelpIcon("Hold Ctrl to enable the delete button.");
                }

                ImGui.Separator();
            }
        }

        ImGui.EndChild();
    }

    private void Refresh() {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Get,
                "/messages?v=2"
            );
            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonConvert.DeserializeObject<MyMessages>(json)!;
            await this.MessagesMutex.WaitAsync();
            try {
                this.Plugin.Ui.MainWindow.ExtraMessages = messages.Extra;
                this.Messages.Clear();
                this.Messages.AddRange(messages.Messages);
            } finally {
                this.MessagesMutex.Release();
            }
        });
    }

    private void Delete(Guid id) {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Delete,
                $"/messages/{id}"
            );

            if (resp.IsSuccessStatusCode) {
                this.Refresh();
                this.Plugin.Vfx.QueueRemove(id);
                this.Plugin.Messages.Remove(id);
            }
        });
    }

    private enum SortMode {
        Date,
        Appraisals,
        Likes,
        Dislikes,
        Location,
    }
}
