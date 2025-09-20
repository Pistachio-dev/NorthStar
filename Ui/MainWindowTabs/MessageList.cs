using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using NorthStar.Helpers;
using NorthStar.Util;

namespace NorthStar.Ui.MainWindowTabs;

internal class MessageList : ITab
{
    public string Name => "Your messages";
    private Plugin Plugin { get; }
    private SortMode Sort { get; set; }

    private SemaphoreSlim MessagesMutex { get; } = new(1, 1);
    private List<MessageWithTerritory> Messages { get; } = [];

    internal MessageList(Plugin plugin)
    {
        Plugin = plugin;
    }

    public void Dispose()
    {
    }

    public void Draw()
    {
        if (ImGui.Button("Refresh"))
        {
            Refresh();
        }

        ImGui.SameLine();

        if (ImGui.BeginCombo("Sort", $"{Sort}"))
        {
            foreach (var mode in Enum.GetValues<SortMode>())
            {
                if (ImGui.Selectable($"{mode}", mode == Sort))
                {
                    Sort = mode;
                }
            }

            ImGui.EndCombo();
        }

        MessagesMutex.Wait();
        try
        {
            ShowList();
        }
        finally
        {
            MessagesMutex.Release();
        }
    }

    private void ShowList()
    {
        ImGui.TextUnformatted($"Messages: {Messages.Count:N0} / {NorthStar.Messages.MaxAmount + Plugin.Ui.MainWindow.ExtraMessages:N0}");

        ImGui.Separator();

        if (ImGui.BeginChild("##messages-list"))
        {
            var messages = Messages;
            if (Sort != SortMode.Date)
            {
                messages = messages.ToList();
                messages.Sort((a, b) =>
                {
                    return Sort switch
                    {
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

            foreach (var message in messages)
            {
                var territory = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(message.Territory);
                var territoryName = territory?.PlaceName.Value.Name.ToDalamudString().TextValue ?? "???";

                var loc = $"Location: {territoryName}";
                if (message.Ward != null)
                {
                    loc += " (";

                    if (message.World != null)
                    {
                        var world = Plugin.DataManager.GetExcelSheet<World>().GetRowOrDefault((ushort)message.World);
                        if (world != null)
                        {
                            loc += world.Value.Name.ToDalamudString().TextValue;
                        }
                    }

                    if (loc != " (")
                    {
                        loc += ", ";
                    }

                    loc += $"Ward {message.Ward.Value}";

                    if (message.Plot != null)
                    {
                        if (message.Plot.Value >= HousingLocationExt.Apt)
                        {
                            var apartment = message.Plot.Value - 10_000;
                            var wing = apartment < HousingLocationExt.Wng ? 1 : 2;
                            var apt = wing == 2 ? apartment - HousingLocationExt.Wng : apartment;

                            if (apt == 0)
                            {
                                loc += $", Wing {wing}, Lobby";
                            }
                            else
                            {
                                loc += $", Wing {wing}, Apt. {apt}";
                            }
                        }
                        else
                        {
                            loc += $", Plot {message.Plot.Value}";
                        }
                    }

                    loc += ")";
                }

                ImGui.TextUnformatted(message.Text);
                ImGui.TreePush("location");
                using (new OnDispose(ImGui.TreePop))
                {
                    ImGui.TextUnformatted(loc);
                    ImGui.SameLine();

                    if (ImGuiHelper.SmallIconButton(FontAwesomeIcon.MapMarkerAlt, $"{message.Id}") && territory != null)
                    {
                        Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(
                            territory.Value.RowId,
                            territory.Value.Map.RowId,
                            (int)(message.X * 1_000),
                            (int)(message.Z * 1_000)
                        ));
                    }

                    if (message.IsHidden)
                    {
                        ImGuiHelper.WarningText("This message will not be shown to other players due to its low score.");
                    }

                    var ctrl = ImGui.GetIO().KeyCtrl;

                    var appraisals = Math.Max(0, message.PositiveVotes - message.NegativeVotes);
                    ImGui.TextUnformatted($"Appraisals: {appraisals:N0} ({message.PositiveVotes:N0} - {message.NegativeVotes:N0})");

                    if (!ctrl)
                    {
                        ImGui.BeginDisabled();
                    }

                    try
                    {
                        if (ImGui.Button($"Delete##{message.Id}"))
                        {
                            Delete(message.Id);
                        }
                    }
                    finally
                    {
                        if (!ctrl)
                        {
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

    private void Refresh()
    {
        Task.Run(async () =>
        {
            var resp = await ServerHelper.SendRequest(
                Plugin.Config.ApiKey,
                HttpMethod.Get,
                "/messages?v=2"
            );
            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonConvert.DeserializeObject<MyMessages>(json)!;
            await MessagesMutex.WaitAsync();
            try
            {
                Plugin.Ui.MainWindow.ExtraMessages = messages.Extra;
                Messages.Clear();
                Messages.AddRange(messages.Messages);
            }
            finally
            {
                MessagesMutex.Release();
            }
        });
    }

    private void Delete(Guid id)
    {
        Task.Run(async () =>
        {
            var resp = await ServerHelper.SendRequest(
                Plugin.Config.ApiKey,
                HttpMethod.Delete,
                $"/messages/{id}"
            );

            if (resp.IsSuccessStatusCode)
            {
                Refresh();
                Plugin.Vfx.QueueRemove(id);
                Plugin.Messages.Remove(id);
            }
        });
    }

    private enum SortMode
    {
        Date,
        Appraisals,
        Likes,
        Dislikes,
        Location,
    }
}