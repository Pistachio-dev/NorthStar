using OrangeGuidanceTomestone.Util;

namespace OrangeGuidanceTomestone;

internal static class HousingLocationExt {
    internal const ushort Apt = 10_000;
    internal const ushort Wng = 5_000;

    internal static ushort? CombinedPlot(this HousingLocation housing) {
        return housing switch {
            // lobby
            { Apartment: null, ApartmentWing: { } wang } => (ushort) (Apt + (wang - 1) * Wng),
            // apartment
            { Apartment: { } apt, ApartmentWing: { } wing } => (ushort) (Apt + (wing - 1) * Wng + apt),
            // normal plot interior
            { Plot: { } plotNum } => plotNum,
            _ => null,
        };
    }
}
