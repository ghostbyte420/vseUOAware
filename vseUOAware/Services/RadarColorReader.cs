namespace vseUOAware.Services;

/// <summary>One radar-map color entry: its index in radarcol.mul and the RGB it represents.</summary>
internal sealed record RadarColorEntry(int Index, byte R, byte G, byte B);

/// <summary>
/// Reads radarcol.mul: a flat array of 16-bit UO colors (same 5-5-5 format
/// as hues.mul) used to render the in-game overhead radar/mini-map. The
/// exact land-vs-item tile ID boundary within this array is not officially
/// documented in a way we can verify against this specific file, so this
/// reader exposes a direct index lookup rather than asserting a tile ID mapping.
/// </summary>
internal static class RadarColorReader
{
    public static int GetEntryCount(string radarColFilePath)
        => (int)(new FileInfo(radarColFilePath).Length / 2);

    public static RadarColorEntry GetColor(string radarColFilePath, int index)
    {
        int count = GetEntryCount(radarColFilePath);
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), $"Index must be between 0 and {count - 1}.");

        using var stream = File.OpenRead(radarColFilePath);
        stream.Seek((long)index * 2, SeekOrigin.Begin);
        using var reader = new BinaryReader(stream);

        ushort value = reader.ReadUInt16();
        return new RadarColorEntry(index, ToR(value), ToG(value), ToB(value));
    }

    private static byte ToR(ushort value) => (byte)(((value >> 10) & 0x1F) * 255 / 31);
    private static byte ToG(ushort value) => (byte)(((value >> 5) & 0x1F) * 255 / 31);
    private static byte ToB(ushort value) => (byte)((value & 0x1F) * 255 / 31);
}
