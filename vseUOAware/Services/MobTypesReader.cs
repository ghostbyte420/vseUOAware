namespace vseUOAware.Services;

/// <summary>Broad animation category for a body, matching mobtypes.txt conventions.</summary>
internal enum MobType
{
    Monster = 0,
    Sea = 1,
    Animal = 2,
    Human = 3,
    Equipment = 4
}

/// <summary>One parsed mobtypes.txt entry: a body's category and optional-action bit flags.</summary>
internal sealed record MobTypeEntry(int Body, MobType Type, uint Flags);

/// <summary>
/// Reads mobtypes.txt: an optional file that tells the client which broad
/// category (monster/sea/animal/human/equipment) a body belongs to, which in
/// turn determines its action-count convention and whether it's flagged as a
/// "UOP body" (the 0x10000 bit) that uses AnimationFrame*.uop instead of
/// classic Anim*.mul.
/// </summary>
internal static class MobTypesReader
{
    private static readonly string[] TypeNames = { "monster", "sea_monster", "animal", "human", "equipment" };

    // Per-category action counts, matching the historical client convention.
    private static readonly int[] ActionCounts = { 22, 9, 13, 35, 35 };

    public const uint UopBodyFlag = 0x10000;

    public static Dictionary<int, MobTypeEntry> ReadAll(string mobTypesTxtPath)
    {
        var results = new Dictionary<int, MobTypeEntry>();
        if (!File.Exists(mobTypesTxtPath))
            return results;

        foreach (var rawLine in File.ReadLines(mobTypesTxtPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#' || !char.IsDigit(line[0]))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[0], out int id))
                continue;

            string typeName = parts[1].ToLowerInvariant();
            string flagStr = parts[2];
            int commentIdx = flagStr.IndexOf('#');
            if (commentIdx == 0)
                continue;
            if (commentIdx > 0)
                flagStr = flagStr[..commentIdx].Trim();

            flagStr = flagStr.Replace("0x", "").Replace("0X", "");
            if (!uint.TryParse(flagStr, System.Globalization.NumberStyles.HexNumber, null, out uint flags))
                continue;

            int typeIdx = Array.IndexOf(TypeNames, typeName);
            if (typeIdx < 0)
                continue;

            results[id] = new MobTypeEntry(id, (MobType)typeIdx, flags);
        }

        return results;
    }

    /// <summary>Whether a body is defined in mobtypes.txt with the UOP-animation flag bit set.</summary>
    public static bool IsUopBody(string mobTypesTxtPath, int body)
    {
        var all = ReadAll(mobTypesTxtPath);
        return all.TryGetValue(body, out var entry) && (entry.Flags & UopBodyFlag) != 0;
    }

    /// <summary>Number of defined actions for a given category, used to compute classic Anim*.idx record strides.</summary>
    public static int GetActionCount(MobType type)
    {
        int idx = (int)type;
        return (uint)idx < ActionCounts.Length ? ActionCounts[idx] : 22;
    }
}
