using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NorthStar.Map
{
    internal class ChatCoordsReader
    {
        private readonly Plugin plugin;

        public ChatCoordsReader(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public MapLinkPayload? LastCoords;

        public void Attach()
        {
            plugin.ChatGui.ChatMessage += ReadCoordsFromPostedFlag;
            plugin.ClientState.TerritoryChanged += OnTerritoryChange;

        }

        private void OnTerritoryChange(ushort newTerritory)
        {
            LastCoords = null;
            plugin.VfxSpawner.DespawnAllVFX();
            Plugin.Log.Info($"Coordinates cleared. All VFX despawned.");
        }

        private void ReadCoordsFromPostedFlag(XivChatType type, int timestamp, ref SeString messageSender, ref SeString messageMessage, ref bool isHandled)
        {
            MapLinkPayload? mapLinkPayload = (MapLinkPayload?)messageMessage.Payloads.FirstOrDefault(p => p is MapLinkPayload);
            if (mapLinkPayload == null)
            {
                return;
            }

            LastCoords = mapLinkPayload;
            plugin.VfxSpawner.SpawnBeaconOnFlag(mapLinkPayload);
        }
    }
}