namespace vseUOAware.Services;

/// <summary>
/// Decodes UO gump graphics (interface art) from gumpartLegacyMUL.uop.
/// Layout: int32 width, int32 height, then a per-row lookup table of
/// `height` int32 entries. Each lookup entry, doubled, is the ushort
/// index (relative to the start of the lookup table) where that row's
/// RLE-encoded pixel runs begin: repeating (color, runLength) pairs of
/// 16-bit A1R5G5B5 values, where color 0 means a transparent run of
/// `runLength` pixels and any other color fills `runLength` pixels
/// (with bit 15 flipped back on to restore alpha).
/// </summary>
internal static class GumpReader
{
    public static ArtImage? GetGump(string gumpArtUopFilePath, int gumpId)
    {
        var entries = uopFileReader.ReadEntries(gumpArtUopFilePath);
        ulong hash = UopHashHelper.HashFileName(UopHashHelper.GumpFileName(gumpId));
        var entry = uopFileReader.FindEntryByHash(entries, hash);
        if (entry is null)
            return null;

        var data = uopFileReader.ReadEntryData(gumpArtUopFilePath, entry);
        return Decode(data);
    }

    private static ArtImage Decode(byte[] data)
    {
        if (data.Length < 8)
            throw new InvalidDataException("Gump data too short to contain a header.");

        int width = data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24);
        int height = data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24);

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidDataException($"Implausible gump dimensions ({width}x{height}); refusing to render potentially garbage data.");

        const int dataOffset = 8;

        var lookups = new int[height];
        for (int i = 0; i < height; i++)
        {
            int lookupByteOffset = dataOffset + i * 4;
            if (lookupByteOffset + 4 > data.Length)
                throw new InvalidDataException("Gump lookup table extends past end of data.");

            lookups[i] = data[lookupByteOffset]
                | (data[lookupByteOffset + 1] << 8)
                | (data[lookupByteOffset + 2] << 16)
                | (data[lookupByteOffset + 3] << 24);
        }

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int count = lookups[y] * 2;
            int x = 0;

            while (x < width)
            {
                int colorByteOffset = dataOffset + count * 2;
                if (colorByteOffset + 4 > data.Length)
                    break;

                ushort color = (ushort)(data[colorByteOffset] | (data[colorByteOffset + 1] << 8));
                ushort runLength = (ushort)(data[colorByteOffset + 2] | (data[colorByteOffset + 3] << 8));
                count += 2;

                int runEnd = Math.Min(x + runLength, width);

                if (color != 0)
                {
                    ushort visibleColor = (ushort)(color ^ 0x8000);
                    for (; x < runEnd; x++)
                    {
                        int idx = (y * width + x) * 4;
                        rgba[idx + 0] = (byte)(((visibleColor >> 10) & 0x1F) * 255 / 31);
                        rgba[idx + 1] = (byte)(((visibleColor >> 5) & 0x1F) * 255 / 31);
                        rgba[idx + 2] = (byte)((visibleColor & 0x1F) * 255 / 31);
                        rgba[idx + 3] = 255;
                    }
                }
                else
                {
                    x = runEnd;
                }
            }
        }

        return new ArtImage(width, height, rgba);
    }
}
