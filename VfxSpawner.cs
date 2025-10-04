using Dalamud.Game.Text.SeStringHandling.Payloads;
using NorthStar.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NorthStar
{
    internal class VfxSpawner
    {
        private const string VfxRoute1 = "vfx/monster/gimmick4/eff/m5fa_b0_g11c0w.avfx"; //Mount ordeals effect
        private const string VfxRoute2 = "vfx/monster/gimmick4/eff/m5fa_b0_g12c0w.avfx"; //Mount ordeals effect too

        public static readonly Dictionary<string, string> Replacements = new()
        {
            {VfxRoute1, "PillarOfLight_groundTarget.avfx" },
            {VfxRoute2, "HighFlareStar_groundTarget.avfx" }
        };

        private const string CustomVFX1 = "PillarOfLight_groundTarget.avfx";
        private readonly Plugin plugin;

        public VfxSpawner(Plugin plugin)
        {
            this.plugin = plugin;
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

        public void SpawnBeaconOnFlag(MapLinkPayload mapLinkPayload)
        {
            var player = plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                Plugin.Log.Warning($"Could not spawn VFX: local player is null");
                return;
            }

            Vector3 position = mapLinkPayload.GetPosition(plugin.ClientState);
            float distance = Vector3.Distance(position, player.Position);
            Plugin.Log.Warning("Distance: " + distance);
            if (distance > 10)
            {
                Plugin.Log.Info($"Spawning beacon at {position}");
                plugin.Vfx.QueueSpawn(Guid.NewGuid(), VfxRoute1, position, System.Numerics.Quaternion.Identity);
                return;
            }

            
            var adjustedPosition = new Vector3(position.X, position.Y - 35, position.Z);
            Plugin.Log.Info($"Spawning star at {adjustedPosition}");
            plugin.Vfx.QueueSpawn(Guid.NewGuid(), VfxRoute2, adjustedPosition, System.Numerics.Quaternion.Identity);

        }

        public void DespawnAllVFX()
        {
            plugin.Vfx.QueueRemoveAll();
        }
    }
}
