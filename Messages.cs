using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;
using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone;

internal class Messages : IDisposable {
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

    private static string GetPath(IDataManager data, Message message) {
        var glyph = message.Glyph;
        if (glyph < 0 || glyph >= VfxPaths.Length) {
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

    internal IReadOnlyDictionary<Guid, Message> CurrentCloned {
        get {
            using var guard = this.CurrentMutex.With();
            return this.Current.ToDictionary(e => e.Key, e => e.Value);
        }
    }

    private HashSet<uint> Trials { get; } = [];
    private HashSet<uint> DeepDungeons { get; } = [];

    private bool CutsceneActive {
        get {
            var condition = this.Plugin.Condition;
            return condition[ConditionFlag.OccupiedInCutSceneEvent]
                   || condition[ConditionFlag.WatchingCutscene78];
        }
    }

    private bool GposeActive => this.Plugin.ClientState.IsGPosing;

    private bool _inCutscene;
    private bool _inGpose;

    internal Messages(Plugin plugin) {
        this.Plugin = plugin;

        foreach (var cfc in this.Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()) {
            // Trials, Raids, and Ultimate Raids
            if (cfc.ContentType.RowId is 4 or 5 or 28) {
                // "Raids" - but we only want non-alliance raids
                if (cfc.ContentType.RowId == 5 && cfc.ContentMemberType.RowId == 4) {
                    continue;
                }

                this.Trials.Add(cfc.TerritoryType.RowId);
            }

            if (cfc.ContentType.RowId == 21) {
                this.DeepDungeons.Add(cfc.TerritoryType.RowId);
            }
        }

        if (this.Plugin.Config.ApiKey != string.Empty) {
            this.Plugin.Framework.RunOnFrameworkThread(this.SpawnVfx);
        }

        this.Plugin.Framework.Update += this.DetermineIfSpawn;
        this.Plugin.Framework.Update += this.RemoveConditionally;
        this.Plugin.ClientState.TerritoryChanged += this.TerritoryChanged;
        this.Plugin.ClientState.Login += this.SpawnVfx;
        this.Plugin.ClientState.Logout += this.RemoveVfx;
    }

    public void Dispose() {
        this.Plugin.ClientState.Logout -= this.RemoveVfx;
        this.Plugin.ClientState.Login -= this.SpawnVfx;
        this.Plugin.ClientState.TerritoryChanged -= this.TerritoryChanged;
        this.Plugin.Framework.Update -= this.RemoveConditionally;
        this.Plugin.Framework.Update -= this.DetermineIfSpawn;

        this.RemoveVfx();
    }

    private readonly Stopwatch _timer = new();

    private void TerritoryChanged(ushort territory) {
        this._territoryChanged = true;
        this.RemoveVfx();
    }

    private ushort _lastTerritory;
    private bool _territoryChanged;

    private void DetermineIfSpawn(IFramework framework) {
        var current = this.Plugin.ClientState.TerritoryType;

        var diffTerritory = current != this._lastTerritory;
        var playerPresent = this.Plugin.ClientState.LocalPlayer != null;

        if ((this._territoryChanged || diffTerritory) && playerPresent) {
            this._territoryChanged = false;
            this._timer.Start();
        }

        if (this._timer.Elapsed >= TimeSpan.FromSeconds(1.5)) {
            this._timer.Reset();
            this.SpawnVfx();
        }

        this._lastTerritory = current;
    }

    private void RemoveConditionally(IFramework framework) {
        var nowCutscene = this.CutsceneActive;
        var cutsceneChanged = this._inCutscene != nowCutscene;
        if (this.Plugin.Config.DisableInCutscene && cutsceneChanged) {
            if (nowCutscene) {
                this.RemoveVfx();
                this.Clear();
            } else {
                this.SpawnVfx();
            }
        }

        var nowGpose = this.GposeActive;
        var gposeChanged = this._inGpose != nowGpose;
        if (this.Plugin.Config.DisableInGpose && gposeChanged) {
            if (nowGpose) {
                this.RemoveVfx();
                this.Clear();
            } else {
                this.SpawnVfx();
            }
        }

        this._inCutscene = nowCutscene;
        this._inGpose = nowGpose;
    }

    internal void SpawnVfx() {
        var territory = this.Plugin.ClientState.TerritoryType;
        if (territory == 0 || this.Plugin.Config.BannedTerritories.Contains(territory)) {
            return;
        }

        var world = this.Plugin.ClientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (world == 0) {
            return;
        }

        var housing = HousingLocation.Current();
        var ward = housing?.Ward;
        var plot = housing?.CombinedPlot();

        if (this.Plugin.Config.DisableTrials && this.Trials.Contains(territory)) {
            return;
        }

        if (this.Plugin.Config.DisableDeepDungeon && this.DeepDungeons.Contains(territory)) {
            return;
        }

        if (this.Plugin.Config.DisableInCutscene && this.CutsceneActive) {
            return;
        }

        if (this.Plugin.Config.DisableInGpose && this.GposeActive) {
            return;
        }

        this.RemoveVfx();

        Task.Run(async () => {
            try {
                await this.DownloadMessages(world, territory, ward, plot);
            } catch (Exception ex) {
                Plugin.Log.Error(ex, $"Failed to get messages for territory {territory}");
            }
        });
    }

    private async Task DownloadMessages(uint world, ushort territory, ushort? ward, ushort? plot) {
        var route = $"/messages/{territory}";
        if (ward != null) {
            route += $"?ward={ward}";

            if (plot != null) {
                route += $"&plot={plot}";
            }

            route += $"&world={world}";
        }

        var resp = await ServerHelper.SendRequest(
            this.Plugin.Config.ApiKey,
            HttpMethod.Get,
            route
        );
        var json = await resp.Content.ReadAsStringAsync();
        var messages = JsonConvert.DeserializeObject<Message[]>(json)!;

        await this.CurrentMutex.WaitAsync();
        try {
            this.Current.Clear();

            foreach (var message in messages) {
                this.Current[message.Id] = message;
                var path = GetPath(this.Plugin.DataManager, message);
                var rotation = Quaternion.CreateFromYawPitchRoll(message.Yaw, 0, 0);
                this.Plugin.Vfx.QueueSpawn(message.Id, path, message.Position, rotation);
            }
        } finally {
            this.CurrentMutex.Release();
        }
    }

    private void RemoveVfx(int type, int code) {
        this.RemoveVfx();
    }

    internal void RemoveVfx() {
        this.Plugin.Vfx.QueueRemoveAll();
    }

    internal void Clear() {
        this.CurrentMutex.Wait();
        try {
            this.Current.Clear();
        } finally {
            this.CurrentMutex.Release();
        }
    }

    internal IEnumerable<Message> Nearby() {
        if (this.Plugin.ClientState.LocalPlayer is not { } player) {
            return [];
        }

        var position = player.Position;

        List<Message> nearby;
        this.CurrentMutex.Wait();
        try {
            nearby = this.Current
                .Values
                .Where(msg => Math.Abs(msg.Position.Y - position.Y) <= 1f)
                .Where(msg => Vector3.DistanceSquared(msg.Position, position) <= 4f)
                .ToList();
        } finally {
            this.CurrentMutex.Release();
        }

        return nearby;
    }

    internal void Add(Message message) {
        this.CurrentMutex.Wait();
        try {
            this.Current[message.Id] = message;
        } finally {
            this.CurrentMutex.Release();
        }

        var path = GetPath(this.Plugin.DataManager, message);
        var rotation = Quaternion.CreateFromYawPitchRoll(message.Yaw, 0, 0);
        this.Plugin.Vfx.QueueSpawn(message.Id, path, message.Position, rotation);
    }

    internal void Remove(Guid id) {
        this.CurrentMutex.Wait();
        try {
            this.Current.Remove(id);
        } finally {
            this.CurrentMutex.Release();
        }
    }
}
