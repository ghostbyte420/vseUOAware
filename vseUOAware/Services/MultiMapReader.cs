namespace vseUOAware.Services;

/// <summary>
/// Reads Multimap.rle: a simple black/white run-length-encoded overview
/// map image (the in-game "world map"). Format: 4-byte width, 4-byte
/// height, then a stream of RLE bytes where the low 7 bits are a run
/// length and the high bit selects black (1) or white (0).
/// </summary>
internal static class MultiMapReader
{
    public static (byte[] Rgba, int Width, int Height)? GetMultiMap(string multiMapRleFilePath)
    {
        if (!File.Exists(multiMapRleFilePath))
            return null;

        using var stream = File.OpenRead(multiMapRleFilePath);
        using var reader = new BinaryReader(stream);

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        if (width <= 0 || height <= 0)
            return null;

        var rgba = new byte[width * height * 4];
        int remaining = (int)(stream.Length - stream.Position);
        var buffer = reader.ReadBytes(remaining);

        int x = 0, y = 0, j = 0;
        while (j < buffer.Length && y < height)
        {
            byte pixel = buffer[j++];
            int count = pixel & 0x7F;
            bool black = (pixel & 0x80) != 0;
            byte gray = black ? (byte)0 : (byte)255;

            for (int i = 0; i < count; i++)
            {
                if (y < height && x < width)
                {
                    int o = (y * width + x) * 4;
                    rgba[o] = gray;
                    rgba[o + 1] = gray;
                    rgba[o + 2] = gray;
                    rgba[o + 3] = 255;
                }

                x++;
                if (x >= width)
                {
                    x = 0;
                    y++;
                    if (y >= height)
                        break;
                }
            }
        }

        return (rgba, width, height);
    }
}
