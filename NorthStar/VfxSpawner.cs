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

        private Stopwatch stopwach = new();
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

        public void SpawnLightOnPlayerPosition()
        {
            var player = plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                Plugin.Log.Warning($"Could not spawn VFX on player position: local player is null");
                return;
            }

            plugin.Vfx.QueueSpawn(Guid.NewGuid(), "bg/ex2/02_est_e3/common/vfx/eff/b0941trp1f_o.avfx", player.Position, System.Numerics.Quaternion.Identity);
        }

        public void SpawnBeaconOnLastCoords()
        {
            if (!plugin.Config.Enabled)
            {
                return;
            }
            if (lastReadCoords == null)
            {
                return;
            }

            if (!DoCoordsMatchCurrentMap())
            {
                Plugin.Log.Info("Last received coords do not match the current map.");
                return;
            }

            DespawnAllVFX();
            var player = plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                SpawnState = VfxSpawnState.Nothing;
                Plugin.Log.Warning($"Could not spawn VFX: local player is null");
                return;
            }

            Vector3 position = lastReadCoords.GetPosition(plugin.ClientState);
            float distance = Vector3.Distance(position, player.Position);
            Plugin.Log.Info("Distance: " + distance);
            if (distance > plugin.Config.PillarOfLightMinDistance)
            {
                // Spawn pillar
                Plugin.Log.Info($"Spawning beacon at {position}");
                plugin.Vfx.QueueSpawn(Guid.NewGuid(), VfxRoute1, position, System.Numerics.Quaternion.Identity);
                SpawnState = VfxSpawnState.Pillar;
                return;
            }

            if (distance < plugin.Config.StarMinDistance)
            {
                // Despawn, they are there
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

            if (!stopwach.IsRunning)
            {
                stopwach.Start();
                return;
            }

            if (HasDistanceThresholdBeenCrossed())
            {
                DespawnAllVFX();
                SpawnBeaconOnLastCoords();
                Plugin.Log.Info("Changing VFX based on distance.");
                return;
            }

            if (stopwach.Elapsed > TimeSpan.FromSeconds(4))
            {
                // Redraw
                DespawnAllVFX();
                SpawnBeaconOnLastCoords();
                Plugin.Log.Debug("Redrawing beacon VFX");
                stopwach.Restart();
            }
        }

        public void DespawnAllVFX()
        {
            plugin.Vfx.QueueRemoveAll();
        }

        public void Dispose()
        {
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
            var playerPosition = plugin.ClientState.LocalPlayer?.Position ?? Vector3.Zero;
            var distance = Vector3.Distance(vfxPosition, playerPosition);
            return (SpawnState == VfxSpawnState.Pillar && distance < plugin.Config.PillarOfLightMinDistance)
                || (SpawnState == VfxSpawnState.Star && (distance > plugin.Config.PillarOfLightMinDistance || distance < plugin.Config.StarMinDistance))
                || (SpawnState == VfxSpawnState.Nothing && distance > plugin.Config.StarMinDistance);
        }
    }
}