using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace NorthStar.Map
{
    internal class ChatCoordsReader : IDisposable
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

        public void Dispose()
        {
            plugin.ChatGui.ChatMessage -= ReadCoordsFromPostedFlag;
        }
    }
}