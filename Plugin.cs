using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NorthStar.MiniPenumbra;
using NorthStar.Util;

namespace NorthStar;

public class Plugin : IDalamudPlugin
{
    internal static string Name => "Orange Guidance Tomestone";

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

    internal string AvfxFilePath { get; }

    public Plugin()
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

        if (Config.ApiKey == string.Empty)
        {
            GetApiKey();
        }
    }

    public void Dispose()
    {
        Pinger.Dispose();
        Commands.Dispose();
        VfxReplacer.Dispose();
        ActorManager.Dispose();
        Ui.Dispose();
        Messages.Dispose();
        Vfx.Dispose();
    }

    internal void SaveConfig()
    {
        Interface.SavePluginConfig(Config);
    }

    private string CopyAvfxFile()
    {
        var configDir = Interface!.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDir);
        for (var i = 0; i < Messages.VfxPaths.Length; i++)
        {
            var letter = (char)('a' + i);
            var stream = Resourcer.Resource.AsStreamUnChecked($"OrangeGuidanceTomestone.vfx.sign_{letter}.avfx");
            var path = Path.Join(configDir, $"sign_{letter}.avfx");
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