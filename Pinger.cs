using Dalamud.Plugin.Services;
using NorthStar.Helpers;
using System.Diagnostics;

namespace NorthStar;

internal class Pinger : IDisposable
{
    private Plugin Plugin { get; }
    private Stopwatch Stopwatch { get; } = new();
    private int _waitSecs;

    internal Pinger(Plugin plugin)
    {
        Plugin = plugin;

        Stopwatch.Start();

        Plugin.Framework.Update += Ping;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= Ping;
    }

    private void Ping(IFramework framework)
    {
        if (Stopwatch.Elapsed < TimeSpan.FromSeconds(_waitSecs))
        {
            return;
        }

        Stopwatch.Restart();

        if (Plugin.Config.ApiKey == string.Empty)
        {
            _waitSecs = 5;
            return;
        }

        // 30 mins
        _waitSecs = 1_800;

        Task.Run(async () =>
        {
            var resp = await ServerHelper.SendRequest(
                Plugin.Config.ApiKey,
                HttpMethod.Post,
                "/ping"
            );

            if (!resp.IsSuccessStatusCode)
            {
                Plugin.Log.Warning($"Failed to ping, status {resp.StatusCode}");
            }
        });
    }
}