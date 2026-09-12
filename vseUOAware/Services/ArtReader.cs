namespace vseUOAware.Services;

/// <summary>Decoded RGBA pixel buffer for one art tile.</summary>
internal sealed record ArtImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// Decodes UO art tiles (land and item graphics) from artLegacyMUL.uop.
/// Land tiles (ID &lt; 0x4000) are fixed 44x44 diamond-shaped bitmaps with
/// no header. Item tiles (ID &gt;= 0x4000, looked up here by their raw art
/// entry ID) use a small header (unused/unknown int32, width, height, then
/// a per-row lookup table of ushort offsets) followed by run-length-encoded
/// scanlines of (count, color) pairs, each color a 16-bit A1R5G5B5 value.
/// </summary>
internal static class ArtReader
{
    private const int LandTileSize = 44;

    /// <summary>Looks up and decodes one art tile (land or item) by its numeric tile ID from artLegacyMUL.uop.</summary>
    public static ArtImage? GetArtTile(string artUopFilePath, int tileId)
    {
        var entries = uopFileReader.ReadEntries(artUopFilePath);
        ulong hash = UopHashHelper.HashFileName(UopHashHelper.ArtFileName(tileId));
        var entry = uopFileReader.FindEntryByHash(entries, hash);
        if (entry is null)
            return null;

        var data = uopFileReader.ReadEntryData(artUopFilePath, entry);

        return tileId < 0x4000
            ? DecodeLandTile(data)
            : DecodeItemTile(data);
    }

    private static ArtImage DecodeLandTile(byte[] data)
    {
        // Fixed 44x44 diamond. Each of the 44 rows has a fixed run length
        // (2, 6, 10, ... up to 44, then back down), stored contiguously as
        // 16-bit A1R5G5B5 pixels, left-padded to center each row.
        const int size = LandTileSize;
        var rgba = new byte[size * size * 4];

        int pos = 0;
        for (int y = 0; y < size; y++)
        {
            int rowWidth = y < size / 2
                ? (y + 1) * 2
                : (size - y) * 2;
            int rowStart = (size - rowWidth) / 2;

            for (int x = 0; x < rowWidth; x++)
            {
                if (pos + 1 >= data.Length)
                    break;

                ushort color = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2;

                int px = rowStart + x;
                WritePixel(rgba, size, px, y, color, color != 0);
            }
        }

        return new ArtImage(size, size, rgba);
    }

    private static ArtImage DecodeItemTile(byte[] data)
    {
        if (data.Length < 8)
            throw new InvalidDataException("Item art data too short to contain a header.");

        // Header: 2 unused ushorts, then width, height as ushorts (indices 0-3 in ushort units).
        int width = data[4] | (data[5] << 8);
        int height = data[6] | (data[7] << 8);

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            throw new InvalidDataException($"Implausible item art dimensions ({width}x{height}); refusing to render potentially garbage data.");

        // Lookup table entries are ushort offsets, relative to a base of (height + 4)
        // ushort-units from the start of the buffer.
        int start = height + 4;
        var lookups = new int[height];
        for (int i = 0; i < height; i++)
        {
            int byteOffset = 8 + i * 2;
            if (byteOffset + 2 > data.Length)
                throw new InvalidDataException("Item art lookup table extends past end of data.");

            ushort rowOffset = (ushort)(data[byteOffset] | (data[byteOffset + 1] << 8));
            lookups[i] = start + rowOffset;
        }

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int count = lookups[y];
            int cur = 0;

            while (true)
            {
                if (!TryReadUshort(data, count, out int xOffset))
                    break;
                count++;
                if (!TryReadUshort(data, count, out int xRun))
                    break;
                count++;

                if (xOffset + xRun == 0)
                    break;

                if (xOffset > width)
                    break;

                cur += xOffset;
                if (xOffset + xRun > width)
                    break;

                int end = cur + xRun;
                for (; cur < end; cur++, count++)
                {
                    if (!TryReadUshort(data, count, out int colorValue))
                        break;

                    ushort color = (ushort)(colorValue ^ 0x8000);
                    WritePixel(rgba, width, cur, y, color, true);
                }
            }
        }

        return new ArtImage(width, height, rgba);
    }

    private static bool TryReadUshort(byte[] data, int ushortIndex, out int value)
    {
        int byteOffset = ushortIndex * 2;
        if (byteOffset + 2 > data.Length)
        {
            value = 0;
            return false;
        }

        value = data[byteOffset] | (data[byteOffset + 1] << 8);
        return true;
    }

    private static void WritePixel(byte[] rgba, int width, int x, int y, ushort color, bool opaque)
    {
        if (x < 0 || y < 0)
            return;

        int i = (y * width + x) * 4;
        if (i + 3 >= rgba.Length)
            return;

        rgba[i + 0] = (byte)(((color >> 10) & 0x1F) * 255 / 31); // R
        rgba[i + 1] = (byte)(((color >> 5) & 0x1F) * 255 / 31);  // G
        rgba[i + 2] = (byte)((color & 0x1F) * 255 / 31);          // B
        rgba[i + 3] = (byte)(opaque ? 255 : 0);                   // A
    }
}
