using System.Diagnostics;
using Dalamud.Plugin.Services;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone;

internal class Pinger : IDisposable {
    private Plugin Plugin { get; }
    private Stopwatch Stopwatch { get; } = new();
    private int _waitSecs;

    internal Pinger(Plugin plugin) {
        this.Plugin = plugin;

        this.Stopwatch.Start();

        this.Plugin.Framework.Update += this.Ping;
    }

    public void Dispose() {
        this.Plugin.Framework.Update -= this.Ping;
    }

    private void Ping(IFramework framework) {
        if (this.Stopwatch.Elapsed < TimeSpan.FromSeconds(this._waitSecs)) {
            return;
        }

        this.Stopwatch.Restart();

        if (this.Plugin.Config.ApiKey == string.Empty) {
            this._waitSecs = 5;
            return;
        }

        // 30 mins
        this._waitSecs = 1_800;

        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Post,
                "/ping"
            );

            if (!resp.IsSuccessStatusCode) {
                Plugin.Log.Warning($"Failed to ping, status {resp.StatusCode}");
            }
        });
    }
}
