using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using NorthStar.Helpers;
using NorthStar.Util;
using System.Numerics;

namespace NorthStar.Ui;

internal class Viewer
{
    private Plugin Plugin { get; }

    internal bool Visible;

    internal delegate void MessageViewDelegate(Message? message);

    internal event MessageViewDelegate? View;

    private Guid _lastViewed = Guid.Empty;

    private int _idx;

    internal Viewer(Plugin plugin)
    {
        Plugin = plugin;
    }

    internal void Draw()
    {
        if (!Visible)
        {
            if (_lastViewed != Guid.Empty)
            {
                View?.Invoke(null);
            }

            _lastViewed = Guid.Empty;
            return;
        }

        var flags = ImGuiWindowFlags.NoFocusOnAppearing;
        flags |= Plugin.Config.HideTitlebar ? ImGuiWindowFlags.NoTitleBar : ImGuiWindowFlags.None;
        flags |= Plugin.Config.LockViewer ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None;
        flags |= Plugin.Config.ClickThroughViewer ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None;
        ImGui.SetNextWindowSize(new Vector2(350, 175), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(Plugin.Config.ViewerOpacity / 100.0f);
        using var end = new OnDispose(ImGui.End);
        if (!ImGui.Begin("Messages", ref Visible, flags))
        {
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            _idx = 0;
        }

        var nearby = Plugin.Messages.Nearby()
            .OrderBy(msg => msg.Id)
            .ToList();
        if (nearby.Count == 0)
        {
            if (Plugin.Config.AutoViewerClose)
            {
                Visible = false;
            }
            else
            {
                ImGui.TextUnformatted("No nearby messages");
            }

            return;
        }

        if (_idx >= nearby.Count)
        {
            _idx = Math.Max(0, nearby.Count - 1);
        }

        if (!ImGui.BeginTable("##viewer-table", 3))
        {
            return;
        }

        using var endTable = new OnDispose(ImGui.EndTable);

        ImGui.TableSetupColumn("##prev-arrow", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##content", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##next-arrow", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableNextRow();

        if (ImGui.TableSetColumnIndex(0))
        {
            var height = ImGui.GetContentRegionAvail().Y;
            var buttonHeight = ImGuiHelpers.GetButtonSize("<").Y;
            ImGui.Dummy(new Vector2(1, height / 2 - buttonHeight / 2 - ImGui.GetStyle().ItemSpacing.Y));
            if (_idx == 0)
            {
                ImGui.BeginDisabled();
            }

            if (ImGuiHelper.IconButton(FontAwesomeIcon.AngleLeft))
            {
                _idx -= 1;
            }

            if (_idx == 0)
            {
                ImGui.EndDisabled();
            }
        }

        if (ImGui.TableSetColumnIndex(1) && _idx > -1 && _idx < nearby.Count)
        {
            var message = nearby[_idx];
            if (_lastViewed != message.Id)
            {
                try
                {
                    View?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error in View event");
                }
            }

            _lastViewed = message.Id;

            var size = ImGui.CalcTextSize(message.Text, wrapWidth: ImGui.GetContentRegionAvail().X).Y;
            size += ImGui.GetStyle().ItemSpacing.Y * 2;
            size += ImGui.CalcTextSize("A").Y;
            size += ImGuiHelpers.GetButtonSize("A").Y;
            var height = ImGui.GetContentRegionAvail().Y;
            ImGui.Dummy(new Vector2(1, height / 2 - size / 2 - ImGui.GetStyle().ItemSpacing.Y));

            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message.Text);
            ImGui.PopTextWrapPos();

            var appraisals = Math.Max(0, message.PositiveVotes - message.NegativeVotes);
            ImGui.TextUnformatted($"Appraisals: {appraisals:N0}");

            void Vote(int way)
            {
                Task.Run(async () =>
                {
                    var resp = await ServerHelper.SendRequest(
                        Plugin.Config.ApiKey,
                        HttpMethod.Patch,
                        $"/messages/{message.Id}/votes",
                        "application/json",
                        new StringContent(way.ToString())
                    );

                    if (resp.IsSuccessStatusCode)
                    {
                        var oldWay = message.UserVote;
                        switch (oldWay)
                        {
                            case 1:
                                message.PositiveVotes -= 1;
                                break;

                            case -1:
                                message.NegativeVotes -= 1;
                                break;
                        }

                        switch (way)
                        {
                            case 1:
                                message.PositiveVotes += 1;
                                break;

                            case -1:
                                message.NegativeVotes += 1;
                                break;
                        }

                        message.UserVote = way;
                    }
                });
            }

            var vote = message.UserVote;
            if (vote == 1)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Like"))
            {
                Vote(1);
            }

            if (vote == 1)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();

            if (vote == -1)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Dislike"))
            {
                Vote(-1);
            }

            if (vote == -1)
            {
                ImGui.EndDisabled();
            }
        }

        if (ImGui.TableSetColumnIndex(2))
        {
            var height = ImGui.GetContentRegionAvail().Y;
            var buttonHeight = ImGuiHelpers.GetButtonSize(">").Y;
            ImGui.Dummy(new Vector2(1, height / 2 - buttonHeight / 2 - ImGui.GetStyle().ItemSpacing.Y));

            if (_idx == nearby.Count - 1)
            {
                ImGui.BeginDisabled();
            }

            if (ImGuiHelper.IconButton(FontAwesomeIcon.AngleRight))
            {
                _idx += 1;
            }

            if (_idx == nearby.Count - 1)
            {
                ImGui.EndDisabled();
            }
        }
    }
}