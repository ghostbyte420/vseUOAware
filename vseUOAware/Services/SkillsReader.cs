namespace vseUOAware.Services;

/// <summary>One skill from skills.mul: its index, name, and whether it has an action button.</summary>
internal sealed record SkillEntry(int SkillId, string Name, bool HasAction);

/// <summary>
/// Reads Skills.idx + skills.mul: the classic .idx/.mul pair listing every
/// skill name and whether it can be activated directly (like Hiding or
/// Stealth) versus passive skills.
/// </summary>
internal static class SkillsReader
{
    /// <summary>Reads every skill entry using the paired Skills.idx + skills.mul files.</summary>
    public static List<SkillEntry> ReadAll(string skillsIdxFilePath, string skillsMulFilePath)
    {
        var entries = MulIdxReader.ReadIndex(skillsIdxFilePath);
        var results = new List<SkillEntry>();

        foreach (var entry in entries)
        {
            var data = MulIdxReader.ReadEntryData(skillsMulFilePath, entry);
            if (data.Length < 1)
                continue;

            bool hasAction = data[0] != 0;
            string name = System.Text.Encoding.ASCII.GetString(data, 1, data.Length - 1).TrimEnd('\0');

            results.Add(new SkillEntry(entry.Index, name, hasAction));
        }

        return results;
    }

    /// <summary>Finds skills whose name contains the given search text (case-insensitive).</summary>
    public static List<SkillEntry> FindByName(string skillsIdxFilePath, string skillsMulFilePath, string searchText)
    {
        var all = ReadAll(skillsIdxFilePath, skillsMulFilePath);
        return all.FindAll(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
}
