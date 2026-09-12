namespace vseUOAware.Services;

/// <summary>One localized string entry from a Cliloc.* file.</summary>
internal sealed record ClilocEntry(int Number, string Text);

/// <summary>
/// Reads Cliloc.* files: the localized string tables used for all in-game
/// text (item names, system messages, etc.). Format: a 6-byte header
/// (int32 + int16, both unused here), then repeated records of
/// [int32 number][byte flag][ushort length][UTF8 text].
/// </summary>
internal static class ClilocReader
{
    /// <summary>Reads one entry by its cliloc number. Returns null if not found.</summary>
    public static ClilocEntry? GetByNumber(string clilocFilePath, int number)
    {
        using var stream = File.OpenRead(clilocFilePath);
        using var reader = new BinaryReader(stream);

        SkipHeader(reader);

        while (stream.Position < stream.Length)
        {
            if (!TryReadRecord(reader, out var entry))
                break;

            if (entry!.Number == number)
                return entry;
        }

        return null;
    }

    /// <summary>Searches for entries whose text contains the given search text (case-insensitive).</summary>
    public static List<ClilocEntry> Search(string clilocFilePath, string searchText, int offset, int limit)
    {
        using var stream = File.OpenRead(clilocFilePath);
        using var reader = new BinaryReader(stream);

        SkipHeader(reader);

        var results = new List<ClilocEntry>();
        int matched = 0;

        while (stream.Position < stream.Length && results.Count < limit)
        {
            if (!TryReadRecord(reader, out var entry))
                break;

            if (entry!.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                if (matched >= offset)
                    results.Add(entry);
                matched++;
            }
        }

        return results;
    }

    private static void SkipHeader(BinaryReader reader)
    {
        reader.ReadInt32(); // unknown/version
        reader.ReadInt16(); // unknown
    }

    private static bool TryReadRecord(BinaryReader reader, out ClilocEntry? entry)
    {
        entry = null;
        if (reader.BaseStream.Position + 4 + 1 + 2 > reader.BaseStream.Length)
            return false;

        int number = reader.ReadInt32();
        reader.ReadByte(); // flag - unused
        ushort length = reader.ReadUInt16();

        if (reader.BaseStream.Position + length > reader.BaseStream.Length)
            return false;

        var bytes = reader.ReadBytes(length);
        string text = System.Text.Encoding.UTF8.GetString(bytes);

        entry = new ClilocEntry(number, text);
        return true;
    }
}
