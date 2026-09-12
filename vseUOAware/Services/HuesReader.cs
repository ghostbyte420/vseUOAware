namespace vseUOAware.Services;

/// <summary>One RGB color, scaled up from UO's 5-bit-per-channel color format.</summary>
internal sealed record HueColor(byte R, byte G, byte B);

/// <summary>One hue (color palette) from hues.mul: 32 shades plus a display name.</summary>
internal sealed record HueEntry(int HueId, string Name, int TableStart, int TableEnd, IReadOnlyList<HueColor> Colors);

/// <summary>
/// Reads hues.mul: the file that defines every "hue" (re-colorable palette)
/// used to tint items, land, and text in the classic client. Hues are
/// grouped 8-per-block, with a 4-byte unused header before each group.
/// </summary>
internal static class HuesReader
{
    private const int ColorsPerHue = 32;
    private const int HuesPerGroup = 8;
    private const int NameLength = 20;
    private const int GroupHeaderSize = 4;

    // Each hue entry: 32 x ushort color table (64 bytes) + start (2) + end (2) + name (20) = 88 bytes.
    private const int HueEntrySize = ColorsPerHue * 2 + 2 + 2 + NameLength;
    private const int GroupSize = GroupHeaderSize + HuesPerGroup * HueEntrySize; // 708 bytes

    /// <summary>Reads every hue (1-based hue IDs, matching classic client convention) from hues.mul.</summary>
    public static List<HueEntry> ReadAll(string huesFilePath)
    {
        var fileInfo = new FileInfo(huesFilePath);
        int groupCount = (int)(fileInfo.Length / GroupSize);

        using var stream = File.OpenRead(huesFilePath);
        using var reader = new BinaryReader(stream);

        var results = new List<HueEntry>();
        int hueId = 1; // Hue IDs are conventionally 1-based; 0 means "no hue".

        for (int group = 0; group < groupCount; group++)
        {
            stream.Seek(GroupHeaderSize, SeekOrigin.Current);

            for (int i = 0; i < HuesPerGroup; i++)
            {
                var colors = new HueColor[ColorsPerHue];
                for (int c = 0; c < ColorsPerHue; c++)
                    colors[c] = ToColor(reader.ReadUInt16());

                int tableStart = reader.ReadUInt16();
                int tableEnd = reader.ReadUInt16();
                var name = ReadFixedName(reader);

                results.Add(new HueEntry(hueId, name, tableStart, tableEnd, colors));
                hueId++;
            }
        }

        return results;
    }

    /// <summary>Reads just one hue by its 1-based ID.</summary>
    public static HueEntry GetHue(string huesFilePath, int hueId)
    {
        if (hueId < 1)
            throw new ArgumentOutOfRangeException(nameof(hueId), "Hue IDs are 1-based (0 means 'no hue').");

        int index = hueId - 1;
        int group = index / HuesPerGroup;
        int indexInGroup = index % HuesPerGroup;

        long entryOffset = (long)group * GroupSize + GroupHeaderSize + (long)indexInGroup * HueEntrySize;
        var fileInfo = new FileInfo(huesFilePath);
        if (entryOffset + HueEntrySize > fileInfo.Length)
            throw new ArgumentOutOfRangeException(nameof(hueId), $"Hue ID {hueId} is beyond the end of hues.mul.");

        using var stream = File.OpenRead(huesFilePath);
        using var reader = new BinaryReader(stream);
        stream.Seek(entryOffset, SeekOrigin.Begin);

        var colors = new HueColor[ColorsPerHue];
        for (int c = 0; c < ColorsPerHue; c++)
            colors[c] = ToColor(reader.ReadUInt16());

        int tableStart = reader.ReadUInt16();
        int tableEnd = reader.ReadUInt16();
        var name = ReadFixedName(reader);

        return new HueEntry(hueId, name, tableStart, tableEnd, colors);
    }

    /// <summary>Finds hues whose name contains the given search text (case-insensitive).</summary>
    public static List<HueEntry> FindByName(string huesFilePath, string searchText)
    {
        var all = ReadAll(huesFilePath);
        return all.FindAll(h => h.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    // UO packs colors as 16-bit values: bit15 unused, bits10-14 = R, bits5-9 = G, bits0-4 = B (5 bits each).
    private static HueColor ToColor(ushort value)
    {
        byte r = (byte)(((value >> 10) & 0x1F) * 255 / 31);
        byte g = (byte)(((value >> 5) & 0x1F) * 255 / 31);
        byte b = (byte)((value & 0x1F) * 255 / 31);
        return new HueColor(r, g, b);
    }

    private static string ReadFixedName(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(NameLength);
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        int length = nullIndex >= 0 ? nullIndex : bytes.Length;
        return System.Text.Encoding.ASCII.GetString(bytes, 0, length);
    }
}
