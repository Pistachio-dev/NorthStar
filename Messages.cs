using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using NorthStar.Helpers;
using NorthStar.Util;
using System.Diagnostics;
using System.Numerics;

namespace NorthStar;

internal class Messages : IDisposable
{
    internal const uint MaxAmount = 20;

    internal static readonly string[] VfxPaths = [
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1a_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1b_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1c_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1d_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1e_o.avfx",
        "bg/ex2/02_est_e3/common/vfx/eff/b0941trp1f_o.avfx",
        "bg/ex4/07_lak_l5/common/vfx/eff/b2640trp1g_o.avfx",
    ];

    private static string GetPath(IDataManager data, Message message)
    {
        var glyph = message.Glyph;
        if (glyph < 0 || glyph >= VfxPaths.Length)
        {
            // not checking if this exists, but the check is really only for the
            // last file in the array anyway. we're guaranteed to have these
            // files with an up-to-date install
            return VfxPaths[0];
        }

        return data.FileExists(VfxPaths[glyph])
            ? VfxPaths[glyph]
            : VfxPaths[message.Id.ToByteArray()[^1] % 5];
    }

    private Plugin Plugin { get; }

    private SemaphoreSlim CurrentMutex { get; } = new(1, 1);
    private Dictionary<Guid, Message> Current { get; } = [];

    internal IReadOnlyDictionary<Guid, Message> CurrentCloned
    {
        get
        {
            using var guard = CurrentMutex.With();
            return Current.ToDictionary(e => e.Key, e => e.Value);
        }
    }

    private HashSet<uint> Trials { get; } = [];
    private HashSet<uint> DeepDungeons { get; } = [];

    private bool CutsceneActive
    {
        get
        {
            var condition = Plugin.Condition;
            return condition[ConditionFlag.OccupiedInCutSceneEvent]
                   || condition[ConditionFlag.WatchingCutscene78];
        }
    }

    private bool GposeActive => Plugin.ClientState.IsGPosing;

    private bool _inCutscene;
    private bool _inGpose;

    internal Messages(Plugin plugin)
    {
        Plugin = plugin;

        foreach (var cfc in Plugin.DataManager.GetExcelSheet<ContentFinderCondition>())
        {
            // Trials, Raids, and Ultimate Raids
            if (cfc.ContentType.RowId is 4 or 5 or 28)
            {
                // "Raids" - but we only want non-alliance raids
                if (cfc.ContentType.RowId == 5 && cfc.ContentMemberType.RowId == 4)
                {
                    continue;
                }

                Trials.Add(cfc.TerritoryType.RowId);
            }

            if (cfc.ContentType.RowId == 21)
            {
                DeepDungeons.Add(cfc.TerritoryType.RowId);
            }
        }

        if (Plugin.Config.ApiKey != string.Empty)
        {
            Plugin.Framework.RunOnFrameworkThread(SpawnVfx);
        }

        Plugin.Framework.Update += DetermineIfSpawn;
        Plugin.Framework.Update += RemoveConditionally;
        Plugin.ClientState.TerritoryChanged += TerritoryChanged;
        Plugin.ClientState.Login += SpawnVfx;
        Plugin.ClientState.Logout += RemoveVfx;
    }

    public void Dispose()
    {
        Plugin.ClientState.Logout -= RemoveVfx;
        Plugin.ClientState.Login -= SpawnVfx;
        Plugin.ClientState.TerritoryChanged -= TerritoryChanged;
        Plugin.Framework.Update -= RemoveConditionally;
        Plugin.Framework.Update -= DetermineIfSpawn;

        RemoveVfx();
    }

    private readonly Stopwatch _timer = new();

    private void TerritoryChanged(ushort territory)
    {
        _territoryChanged = true;
        RemoveVfx();
    }

    private ushort _lastTerritory;
    private bool _territoryChanged;

    private void DetermineIfSpawn(IFramework framework)
    {
        var current = Plugin.ClientState.TerritoryType;

        var diffTerritory = current != _lastTerritory;
        var playerPresent = Plugin.ClientState.LocalPlayer != null;

        if ((_territoryChanged || diffTerritory) && playerPresent)
        {
            _territoryChanged = false;
            _timer.Start();
        }

        if (_timer.Elapsed >= TimeSpan.FromSeconds(1.5))
        {
            _timer.Reset();
            SpawnVfx();
        }

        _lastTerritory = current;
    }

    private void RemoveConditionally(IFramework framework)
    {
        var nowCutscene = CutsceneActive;
        var cutsceneChanged = _inCutscene != nowCutscene;
        if (Plugin.Config.DisableInCutscene && cutsceneChanged)
        {
            if (nowCutscene)
            {
                RemoveVfx();
                Clear();
            }
            else
            {
                SpawnVfx();
            }
        }

        var nowGpose = GposeActive;
        var gposeChanged = _inGpose != nowGpose;
        if (Plugin.Config.DisableInGpose && gposeChanged)
        {
            if (nowGpose)
            {
                RemoveVfx();
                Clear();
            }
            else
            {
                SpawnVfx();
            }
        }

        _inCutscene = nowCutscene;
        _inGpose = nowGpose;
    }

    internal void SpawnVfx()
    {
        var territory = Plugin.ClientState.TerritoryType;
        if (territory == 0 || Plugin.Config.BannedTerritories.Contains(territory))
        {
            return;
        }

        var world = Plugin.ClientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (world == 0)
        {
            return;
        }

        var housing = HousingLocation.Current();
        var ward = housing?.Ward;
        var plot = housing?.CombinedPlot();

        if (Plugin.Config.DisableTrials && Trials.Contains(territory))
        {
            return;
        }

        if (Plugin.Config.DisableDeepDungeon && DeepDungeons.Contains(territory))
        {
            return;
        }

        if (Plugin.Config.DisableInCutscene && CutsceneActive)
        {
            return;
        }

        if (Plugin.Config.DisableInGpose && GposeActive)
        {
            return;
        }

        RemoveVfx();

        Task.Run(async () =>
        {
            try
            {
                await DownloadMessages(world, territory, ward, plot);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to get messages for territory {territory}");
            }
        });
    }

    private async Task DownloadMessages(uint world, ushort territory, ushort? ward, ushort? plot)
    {
        var route = $"/messages/{territory}";
        if (ward != null)
        {
            route += $"?ward={ward}";

            if (plot != null)
            {
                route += $"&plot={plot}";
            }

            route += $"&world={world}";
        }

        var resp = await ServerHelper.SendRequest(
            Plugin.Config.ApiKey,
            HttpMethod.Get,
            route
        );
        var json = await resp.Content.ReadAsStringAsync();
        var messages = JsonConvert.DeserializeObject<Message[]>(json)!;

        await CurrentMutex.WaitAsync();
        try
        {
            Current.Clear();

            foreach (var message in messages)
            {
                Current[message.Id] = message;
                var path = GetPath(Plugin.DataManager, message);
                var rotation = Quaternion.CreateFromYawPitchRoll(message.Yaw, 0, 0);
                Plugin.Vfx.QueueSpawn(message.Id, path, message.Position, rotation);
            }
        }
        finally
        {
            CurrentMutex.Release();
        }
    }

    private void RemoveVfx(int type, int code)
    {
        RemoveVfx();
    }

    internal void RemoveVfx()
    {
        Plugin.Vfx.QueueRemoveAll();
    }

    internal void Clear()
    {
        CurrentMutex.Wait();
        try
        {
            Current.Clear();
        }
        finally
        {
            CurrentMutex.Release();
        }
    }

    internal IEnumerable<Message> Nearby()
    {
        if (Plugin.ClientState.LocalPlayer is not { } player)
        {
            return [];
        }

        var position = player.Position;

        List<Message> nearby;
        CurrentMutex.Wait();
        try
        {
            nearby = Current
                .Values
                .Where(msg => Math.Abs(msg.Position.Y - position.Y) <= 1f)
                .Where(msg => Vector3.DistanceSquared(msg.Position, position) <= 4f)
                .ToList();
        }
        finally
        {
            CurrentMutex.Release();
        }

        return nearby;
    }

    internal void Add(Message message)
    {
        CurrentMutex.Wait();
        try
        {
            Current[message.Id] = message;
        }
        finally
        {
            CurrentMutex.Release();
        }

        var path = GetPath(Plugin.DataManager, message);
        var rotation = Quaternion.CreateFromYawPitchRoll(message.Yaw, 0, 0);
        Plugin.Vfx.QueueSpawn(message.Id, path, message.Position, rotation);
    }

    internal void Remove(Guid id)
    {
        CurrentMutex.Wait();
        try
        {
            Current.Remove(id);
        }
        finally
        {
            CurrentMutex.Release();
        }
    }
}