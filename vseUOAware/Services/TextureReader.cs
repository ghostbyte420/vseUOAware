namespace vseUOAware.Services;

/// <summary>
/// Reads texmaps.mul/texidx.mul: large repeating ground textures (distinct
/// from land art tiles), used for terrain rendering. Each entry is a raw
/// (uncompressed) 16bpp (5-5-5) square bitmap - 64x64 normally, or 128x128
/// when the .idx "extra" field is non-zero.
/// </summary>
internal static class TextureReader
{
    public static (byte[] Rgba, int Width, int Height)? GetTexture(string texIdxFilePath, string texMapsMulFilePath, int index)
    {
        var entry = MulIdxReader.ReadEntryAt(texIdxFilePath, index);
        if (entry is null)
            return null;

        int size = entry.Extra == 0 ? 64 : 128;
        int expectedBytes = size * size * 2;

        var data = MulIdxReader.ReadEntryData(texMapsMulFilePath, entry);
        if (data.Length < expectedBytes)
            return null;

        var rgba = new byte[size * size * 4];
        for (int i = 0; i < size * size; i++)
        {
            ushort raw = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));
            ushort color = (ushort)(raw ^ 0x8000);

            var (r, g, b) = Rgb555.ToRgb888(color);
            int o = i * 4;
            rgba[o] = r;
            rgba[o + 1] = g;
            rgba[o + 2] = b;
            rgba[o + 3] = 255;
        }

        return (rgba, size, size);
    }
}
