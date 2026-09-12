namespace vseUOAware.Services;

/// <summary>
/// Reads light.mul/lightidx.mul: small grayscale bitmaps used for dynamic
/// light sources (torches, campfires, etc). Each pixel is stored as a
/// signed intensity delta rather than a full RGB color, so decoding
/// produces a grayscale image centered around mid-gray.
/// </summary>
internal static class LightReader
{
    /// <summary>Decodes one light bitmap to RGBA, along with its width/height (packed into the .idx "extra" field).</summary>
    public static (byte[] Rgba, int Width, int Height)? GetLight(string lightIdxFilePath, string lightMulFilePath, int index)
    {
        var entry = MulIdxReader.ReadEntryAt(lightIdxFilePath, index);
        if (entry is null)
            return null;

        int width = entry.Extra & 0xFFFF;
        int height = (entry.Extra >> 16) & 0xFFFF;
        if (width <= 0 || height <= 0)
            return null;

        var data = MulIdxReader.ReadEntryData(lightMulFilePath, entry);
        var rgba = new byte[width * height * 4];

        int pixelCount = Math.Min(data.Length, width * height);
        for (int i = 0; i < pixelCount; i++)
        {
            sbyte value = unchecked((sbyte)data[i]);
            int level = Math.Clamp(0x1F + value, 0, 0x1F);
            byte gray = (byte)((level * 255) / 0x1F);

            int o = i * 4;
            rgba[o] = gray;
            rgba[o + 1] = gray;
            rgba[o + 2] = gray;
            rgba[o + 3] = 255;
        }

        return (rgba, width, height);
    }

    /// <summary>Number of defined lights, based on lightidx.mul's record count.</summary>
    public static int GetCount(string lightIdxFilePath)
    {
        var info = new FileInfo(lightIdxFilePath);
        return info.Exists ? (int)(info.Length / 12) : 0;
    }
}
