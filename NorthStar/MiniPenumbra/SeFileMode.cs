namespace NorthStar.MiniPenumbra;

internal enum SeFileMode : byte
{
    LoadUnpackedResource = 0,
    LoadFileResource = 1, // The config files in MyGames use this.

    // Probably debug options only.
    LoadIndexResource = 0xA, // load index/index2

    LoadSqPackResource = 0xB,
}