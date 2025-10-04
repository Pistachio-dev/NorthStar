using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace NorthStar.Map
{
    internal static class MapLinkPayloadExtensions
    {
        internal static Vector3 GetPosition(this MapLinkPayload payload, IClientState clientState)
        {
            var YCoord = clientState.LocalPlayer?.Position.Y ?? 0;
            return new Vector3(payload.RawX/1000, YCoord, payload.RawY/1000);
        }
    }
}
