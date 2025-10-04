using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NorthStar.Map;
using NorthStar.MiniPenumbra;
using NorthStar.Ui;
using NorthStar.Util;
using System;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyLetter;

namespace NorthStar;

public class Plugin : IDalamudPlugin
{
    internal static string Name => "North Star";

    private const string CommandName = "/ns";

    [PluginService]
    internal static IPluginLog Log { get; private set; }

    [PluginService]
    internal IDalamudPluginInterface Interface { get; init; }

    [PluginService]
    internal IChatGui ChatGui { get; init; }

    [PluginService]
    internal IClientState ClientState { get; init; }

    [PluginService]
    internal ICommandManager CommandManager { get; init; }

    [PluginService]
    internal ICondition Condition { get; init; }

    [PluginService]
    internal IDataManager DataManager { get; init; }

    [PluginService]
    internal IFramework Framework { get; init; }

    [PluginService]
    internal IGameGui GameGui { get; init; }

    [PluginService]
    internal IGameInteropProvider GameInteropProvider { get; init; }

    [PluginService]
    internal ITextureProvider TextureProvider { get; init; }

    internal Configuration Config { get; }
    internal Vfx Vfx { get; }
    internal PluginUi Ui { get; }
    internal Messages Messages { get; }
    internal ActorManager ActorManager { get; }
    internal VfxReplacer VfxReplacer { get; }
    internal Commands Commands { get; }
    internal Pinger Pinger { get; }
    internal VfxSpawner VfxSpawner { get; }
    internal ChatCoordsReader ChatCoordsReader { get; }
    internal string AvfxFilePath { get; }

    public readonly WindowSystem WindowSystem = new("NorthStar");
    private MainWindow MainWindow { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        AvfxFilePath = CopyAvfxFile();

        Config = Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        Vfx = new Vfx(this);
        Messages = new Messages(this);
        Ui = new PluginUi(this);
        ActorManager = new ActorManager(this);
        VfxReplacer = new VfxReplacer(this);
        Commands = new Commands(this);
        Pinger = new Pinger(this);
        VfxSpawner = new VfxSpawner(this);
        ChatCoordsReader = new ChatCoordsReader(this);
        ChatCoordsReader.Attach();

        CommandManager?.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open the NorthStar main window"
        });

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);
        pluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        pluginInterface.UiBuilder.OpenConfigUi += ToggleMainUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        if (Config.ApiKey == string.Empty)
        {
            GetApiKey();
        }
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public void Dispose()
    {
        Pinger.Dispose();
        Commands.Dispose();
        VfxReplacer.Dispose();
        ActorManager.Dispose();
        Ui.Dispose();
        Messages.Dispose();
        Vfx.Dispose();

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    internal void SaveConfig()
    {
        Interface.SavePluginConfig(Config);
    }

    private string CopyAvfxFile()
    {
        var configDir = Interface!.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDir);

        var stream = Resourcer.Resource.AsStreamUnChecked($"NorthStar.vfx.PillarOfLight_groundTarget.avfx");
        var path = Path.Join(configDir, $"PillarOfLight_groundTarget.avfx");
        stream.CopyTo(File.Create(path));


        for (var i = 0; i < Messages.VfxPaths.Length; i++)
        {
            var letter = (char)('a' + i);
            stream = Resourcer.Resource.AsStreamUnChecked($"NorthStar.vfx.sign_{letter}.avfx");
            path = Path.Join(configDir, $"sign_{letter}.avfx");
            stream.CopyTo(File.Create(path));
        }

        return configDir;
    }

    internal void GetApiKey()
    {
        Task.Run(async () =>
        {
            var resp = await new HttpClient().PostAsync("https://tryfingerbuthole.anna.lgbt/account", null);
            var key = await resp.Content.ReadAsStringAsync();
            Config.ApiKey = key;
            SaveConfig();
            Framework.RunOnFrameworkThread(Messages.SpawnVfx);
        });
    }
}