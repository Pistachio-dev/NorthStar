using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    }
}
