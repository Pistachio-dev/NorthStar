using Dalamud.Game.Command;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using System.Text;

namespace NorthStar;

internal class Commands : IDisposable
{
    private const string CommandName = "/ogt";
    private Plugin Plugin { get; }

    internal Commands(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = $"Toggle UI - try {CommandName} help",
        });
    }

    public void Dispose()
    {
        Plugin.CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments)
        {
            case "ban":
                {
                    var name = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(Plugin.ClientState.TerritoryType)
                        ?.PlaceName
                        .Value
                        .Name
                        .ToDalamudString()
                        .TextValue;

                    if (Plugin.Config.BannedTerritories.Contains(Plugin.ClientState.TerritoryType))
                    {
                        Plugin.ChatGui.Print($"{name} is already on the ban list.");
                        return;
                    }

                    Plugin.Config.BannedTerritories.Add(Plugin.ClientState.TerritoryType);
                    Plugin.SaveConfig();
                    Plugin.ChatGui.Print($"Added {name} to the ban list.");

                    Plugin.Framework.RunOnFrameworkThread(() =>
                    {
                        Plugin.Messages.RemoveVfx();
                        Plugin.Messages.Clear();
                    });
                    break;
                }
            case "unban":
                {
                    var name = Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(Plugin.ClientState.TerritoryType)
                        ?.PlaceName
                        .Value
                        .Name
                        .ToDalamudString()
                        .TextValue;

                    if (!Plugin.Config.BannedTerritories.Contains(Plugin.ClientState.TerritoryType))
                    {
                        Plugin.ChatGui.Print($"{name} is not on the ban list.");
                        return;
                    }

                    Plugin.Config.BannedTerritories.Remove(Plugin.ClientState.TerritoryType);
                    Plugin.SaveConfig();
                    Plugin.ChatGui.Print($"Removed {name} from the ban list.");

                    Plugin.Messages.SpawnVfx();
                    break;
                }
            case "refresh":
                Plugin.Messages.SpawnVfx();
                break;

            case "viewer":
                Plugin.Ui.Viewer.Visible ^= true;
                break;

            case "help":
                {
                    var sb = new StringBuilder("\n");
                    sb.Append(CommandName);
                    sb.Append(" - open the main interface");

                    sb.Append('\n');
                    sb.Append(CommandName);
                    sb.Append(" ban - bans the current zone, hiding messages");

                    sb.Append('\n');
                    sb.Append(CommandName);
                    sb.Append(" unban - unbans the current zone, allowing messages to appear");

                    sb.Append('\n');
                    sb.Append(CommandName);
                    sb.Append(" refresh - refreshes the messages in the current zone");

                    sb.Append('\n');
                    sb.Append(CommandName);
                    sb.Append(" viewer - toggle the message viewer window");

                    sb.Append('\n');
                    sb.Append(CommandName);
                    sb.Append(" help - show this help");

                    Plugin.ChatGui.Print(sb.ToString());
                    break;
                }
            default:
                Plugin.Ui.MainWindow.Visible ^= true;
                break;
        }
    }
}