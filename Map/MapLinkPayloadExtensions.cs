using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace NorthStar.Map
{
    internal static class MapLinkPayloadExtensions
    {
        internal static Vector3 GetPosition(this MapLinkPayload payload, IClientState clientState)
        {
            var YCoord = clientState.LocalPlayer?.Position.Y ?? 0;
            return new Vector3(payload.RawX / 1000, YCoord, payload.RawY / 1000);
        }
    }
}