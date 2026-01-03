using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NorthStar.Map;
using NorthStar.MiniPenumbra;
using NorthStar.Ui;

namespace NorthStar;

public class Plugin : IDalamudPlugin
{
    internal static string Name => "North Star";

    private const string CommandName = "/ns";

    [PluginService]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    internal static IPluginLog Log { get; private set; }

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
    [PluginService]
    internal IObjectTable ObjectTable { get; init; }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    internal Configuration Config { get; }
    internal Vfx Vfx { get; }
    internal VfxReplacer VfxReplacer { get; }
    internal VfxSpawner VfxSpawner { get; }
    internal ChatCoordsReader ChatCoordsReader { get; }
    internal string AvfxFilePath { get; }

    public readonly WindowSystem WindowSystem = new("NorthStar");
    private MainWindow MainWindow { get; init; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Interface = pluginInterface;
        AvfxFilePath = CopyAvfxFile();

        Config = pluginInterface?.GetPluginConfig() as Configuration ?? new Configuration();
        Vfx = new Vfx(this);
        VfxReplacer = new VfxReplacer(this);
        VfxSpawner = new VfxSpawner(this);
        VfxSpawner.AttachUpdateBasedOnDistance(Framework!);
        ChatCoordsReader = new ChatCoordsReader(this);
        ChatCoordsReader.Attach();

        CommandManager?.AddHandler(CommandName, new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open the NorthStar configuration window"
        });

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);
        pluginInterface.UiBuilder.Draw += DrawUI;

        // The configuration UI is the main UI as well
        pluginInterface.UiBuilder.OpenConfigUi += ToggleMainUI;
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

    public void Dispose()
    {
        Interface.UiBuilder.Draw -= WindowSystem.Draw;
        Interface.UiBuilder.OpenConfigUi -= ToggleMainUI;
        Interface.UiBuilder.OpenMainUi -= ToggleMainUI;

        VfxReplacer.Dispose();
        Vfx.Dispose();
        ChatCoordsReader.Dispose();
        VfxSpawner.Dispose();
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

        var stream = Resourcer.Resource.AsStreamUnChecked($"NorthStar.vfx.PillarOfLightWithFlareStarTop_groundTarget.avfx");
        var path = Path.Join(configDir, $"PillarOfLightWithFlareStarTop_groundTarget.avfx");
        stream.CopyTo(File.Create(path));

        stream = Resourcer.Resource.AsStreamUnChecked($"NorthStar.vfx.HighFlareStar_groundTarget.avfx");
        path = Path.Join(configDir, $"HighFlareStar_groundTarget.avfx");
        stream.CopyTo(File.Create(path));

        return configDir;
    }
}