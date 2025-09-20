using System.Text;
using Dalamud.Game.Command;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace OrangeGuidanceTomestone;

internal class Commands : IDisposable {
    private const string CommandName = "/ogt";
    private Plugin Plugin { get; }

    internal Commands(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand) {
            HelpMessage = $"Toggle UI - try {CommandName} help",
        });
    }

    public void Dispose() {
        this.Plugin.CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string arguments) {
        switch (arguments) {
            case "ban": {
                var name = this.Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(this.Plugin.ClientState.TerritoryType)
                    ?.PlaceName
                    .Value
                    .Name
                    .ToDalamudString()
                    .TextValue;

                if (this.Plugin.Config.BannedTerritories.Contains(this.Plugin.ClientState.TerritoryType)) {
                    this.Plugin.ChatGui.Print($"{name} is already on the ban list.");
                    return;
                }

                this.Plugin.Config.BannedTerritories.Add(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.ChatGui.Print($"Added {name} to the ban list.");

                this.Plugin.Framework.RunOnFrameworkThread(() => {
                    this.Plugin.Messages.RemoveVfx();
                    this.Plugin.Messages.Clear();
                });
                break;
            }
            case "unban": {
                var name = this.Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(this.Plugin.ClientState.TerritoryType)
                    ?.PlaceName
                    .Value
                    .Name
                    .ToDalamudString()
                    .TextValue;

                if (!this.Plugin.Config.BannedTerritories.Contains(this.Plugin.ClientState.TerritoryType)) {
                    this.Plugin.ChatGui.Print($"{name} is not on the ban list.");
                    return;
                }

                this.Plugin.Config.BannedTerritories.Remove(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.ChatGui.Print($"Removed {name} from the ban list.");

                this.Plugin.Messages.SpawnVfx();
                break;
            }
            case "refresh":
                this.Plugin.Messages.SpawnVfx();
                break;
            case "viewer":
                this.Plugin.Ui.Viewer.Visible ^= true;
                break;
            case "help": {
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

                this.Plugin.ChatGui.Print(sb.ToString());
                break;
            }
            default:
                this.Plugin.Ui.MainWindow.Visible ^= true;
                break;
        }
    }
}
