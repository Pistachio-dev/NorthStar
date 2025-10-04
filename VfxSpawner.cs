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
        private readonly Plugin plugin;

        public VfxSpawner(Plugin plugin)
        {
            this.plugin = plugin;
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

        public void SpawnLightOnFlag(MapLinkPayload mapLinkPayload)
        {
            var player = plugin.ClientState.LocalPlayer;
            if (player == null)
            {
                Plugin.Log.Warning($"Could not spawn VFX: local player is null");
                return;
            }

            Vector3 position = mapLinkPayload.GetPosition(plugin.ClientState);
            Plugin.Log.Info($"Spawning beacon at {position}");
            plugin.Vfx.QueueSpawn(Guid.NewGuid(), "bg/ex2/02_est_e3/common/vfx/eff/b0941trp1f_o.avfx", position, System.Numerics.Quaternion.Identity);

        }

        public void DespawnAllVFX()
        {
            plugin.Vfx.QueueRemoveAll();
        }
    }
}
