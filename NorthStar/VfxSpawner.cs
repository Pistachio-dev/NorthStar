using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using NorthStar.Map;
using System.Diagnostics;
using System.Numerics;

namespace NorthStar
{
    internal class VfxSpawner : IDisposable
    {
        private const string VfxRoute1 = "vfx/monster/gimmick4/eff/m5fa_b0_g11c0w.avfx"; //Mount ordeals effect
        private const string VfxRoute2 = "vfx/monster/gimmick4/eff/m5fa_b0_g12c0w.avfx"; //Mount ordeals effect too
        private static VfxSpawnState SpawnState = VfxSpawnState.Nothing;

        public static readonly Dictionary<string, string> Replacements = new()
        {
            {VfxRoute1, "PillarOfLightWithFlareStarTop_groundTarget.avfx" },
            {VfxRoute2, "HighFlareStar_groundTarget.avfx" }
        };

        private Stopwatch stopwatch = new();
        private MapLinkPayload? lastReadCoords;

        public MapLinkPayload? LastReadCoords
        {
            get { return lastReadCoords; }
            set
            {
                lastReadCoords = value;
                if (value == null)
                {
                    Plugin.Log.Info("Last coords set to null");
                    return;
                }

                Plugin.Log.Info($"New read coords: {value.XCoord}, {value.YCoord}: Map {value.Map.RowId} TerritoryType: {value.TerritoryType.RowId}");
            }
        }

        private readonly Plugin plugin;

        public VfxSpawner(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void AttachUpdateBasedOnDistance(IFramework framework)
        {
            framework.Update += OnUpdate;
        }

        public bool IsTerritoryWithOriginalEffects()
        {
            return plugin.ClientState.MapId == 823 // Mount Ordeals map
                || plugin.ClientState.TerritoryType == 1095 // Normal Mount Ordeals
                || plugin.ClientState.TerritoryType == 1095; // Extreme Mount Ordeals
        }

        public void SpawnBeaconOnLastCoords()
        {
            if (plugin.ClientState.IsPvP)
            {
                return;
            }

            DespawnAllVFX();

            if (!plugin.Config.Enabled)
            {
                Plugin.Log.Debug("VFX not spawned. Reason: Plugin disabled.");
                return;
            }
            if (lastReadCoords == null)
            {
                Plugin.Log.Debug("VFX not spawned. Reason: Last read coordinates are null.");
                return;
            }

            if (!DoCoordsMatchCurrentMap())
            {
                Plugin.Log.Debug("VFX not spawned. Last received coords do not match the current map.");
                return;
            }

            var player = plugin.ObjectTable.LocalPlayer;
            if (player == null)
            {
                SpawnState = VfxSpawnState.Nothing;
                Plugin.Log.Warning($"Could not spawn VFX: local player is null");
                return;
            }

            Vector3 position = lastReadCoords.GetPosition(plugin.ClientState);
            float distance = Vector3.Distance(position, player.Position);
            Plugin.Log.Debug("Distance: " + distance);
            if (distance > plugin.Config.PillarOfLightMinDistance)
            {
                // Spawn pillar
                Plugin.Log.Debug($"Spawning beacon at {position}");
                plugin.Vfx.QueueSpawn(Guid.NewGuid(), VfxRoute1, position, System.Numerics.Quaternion.Identity);
                SpawnState = VfxSpawnState.Pillar;
                return;
            }

            if (distance < plugin.Config.StarMinDistance)
            {
                // Despawn, player is already there
                DespawnAllVFX();
                SpawnState = VfxSpawnState.Nothing;
                return;
            }

            // Spawn star
            var adjustedPosition = new Vector3(position.X, position.Y + plugin.Config.StarHeightOffset, position.Z);

            Plugin.Log.Info($"Spawning star at {adjustedPosition}");
            plugin.Vfx.QueueSpawn(Guid.NewGuid(), VfxRoute2, adjustedPosition, System.Numerics.Quaternion.Identity);
            SpawnState = VfxSpawnState.Star;
        }

        public void OnUpdate(IFramework framework)
        {
            if (lastReadCoords == null || !DoCoordsMatchCurrentMap())
            {
                return;
            }

            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
                return;
            }

            if (HasDistanceThresholdBeenCrossed())
            {
                SpawnBeaconOnLastCoords();
                Plugin.Log.Info("Changing VFX based on distance.");
                return;
            }

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(plugin.Config.RefreshInterval))
            {
                // Redraw
                SpawnBeaconOnLastCoords();
                Plugin.Log.Debug("Redrawing beacon VFX");
                stopwatch.Restart();
            }
        }

        public void DespawnAllVFX()
        {
            plugin.Vfx.QueueRemoveAll();
        }

        public void Dispose()
        {
            DespawnAllVFX();
            plugin.Framework.Update -= OnUpdate;
        }

        private bool DoCoordsMatchCurrentMap()
        {
            if (lastReadCoords == null)
            {
                return false;
            }

            if (plugin.ClientState.MapId != lastReadCoords.Map.RowId
                || plugin.ClientState.TerritoryType != lastReadCoords.TerritoryType.RowId)
            {
                return false;
            }

            return true;
        }

        private bool HasDistanceThresholdBeenCrossed()
        {
            if (lastReadCoords == null) return false;
            var vfxPosition = lastReadCoords.GetPosition(plugin.ClientState);
            var playerPosition = plugin.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
            var distance = Vector3.Distance(vfxPosition, playerPosition);
            return (SpawnState == VfxSpawnState.Pillar && distance < plugin.Config.PillarOfLightMinDistance)
                || (SpawnState == VfxSpawnState.Star && (distance > plugin.Config.PillarOfLightMinDistance || distance < plugin.Config.StarMinDistance))
                || (SpawnState == VfxSpawnState.Nothing && distance > plugin.Config.StarMinDistance);
        }
    }
}