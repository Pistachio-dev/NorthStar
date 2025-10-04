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

        public void Attach()
        {
            plugin.ChatGui.ChatMessage += ReadCoordsFromPostedFlag;
            plugin.ClientState.TerritoryChanged += OnTerritoryChange;
        }

        private void OnTerritoryChange(ushort newTerritory)
        {
            Plugin.Log.Info($"All VFX despawned. Checking if new location matches coordinates.");
            plugin.VfxSpawner.SpawnBeaconOnLastCoords();
        }

        private void ReadCoordsFromPostedFlag(XivChatType type, int timestamp, ref SeString messageSender, ref SeString messageMessage, ref bool isHandled)
        {
            MapLinkPayload? mapLinkPayload = (MapLinkPayload?)messageMessage.Payloads.FirstOrDefault(p => p is MapLinkPayload);
            if (mapLinkPayload == null)
            {
                return;
            }

            Plugin.Log.Debug($"Map: {mapLinkPayload.Map.RowId} TT: {mapLinkPayload.TerritoryType.RowId}");
            Plugin.Log.Debug($"Local Map: {plugin.ClientState.MapId} TT: {plugin.ClientState.TerritoryType}");
            plugin.VfxSpawner.LastReadCoords = mapLinkPayload;
            plugin.VfxSpawner.SpawnBeaconOnLastCoords();
        }

        public void Dispose()
        {
            plugin.ChatGui.ChatMessage -= ReadCoordsFromPostedFlag;
        }
    }
}