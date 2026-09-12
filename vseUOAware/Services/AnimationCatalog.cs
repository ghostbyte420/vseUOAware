namespace vseUOAware.Services;

/// <summary>One discovered action index available for a body (in either the UOP or classic animation storage).</summary>
internal sealed record ActionAvailability(int Action, bool IsUopFormat);

/// <summary>
/// Answers "what body/action combinations actually exist" without requiring
/// the caller to guess numbers or manually cross-reference mobtypes.txt.
/// Wraps mobtypes.txt (UOP-body detection), bodyconv.def (classic file
/// routing), and probing against whichever animation archive/files apply.
/// </summary>
internal static class AnimationCatalog
{
    private const int MaxAnimActions = 80;

    /// <summary>Whether a body is stored in the AnimationFrame*.uop archives (vs. classic Anim*.mul).</summary>
    public static bool IsUopBody(string mobTypesTxtPath, int body)
        => MobTypesReader.IsUopBody(mobTypesTxtPath, body);

    /// <summary>Every body ID with a mobtypes.txt entry, along with its category and whether it's UOP-based.</summary>
    public static List<(int Body, string Category, bool IsUop)> GetCatalogedBodies(string mobTypesTxtPath)
    {
        var all = MobTypesReader.ReadAll(mobTypesTxtPath);
        var result = new List<(int, string, bool)>(all.Count);
        foreach (var entry in all.Values.OrderBy(e => e.Body))
            result.Add((entry.Body, entry.Type.ToString(), (entry.Flags & MobTypesReader.UopBodyFlag) != 0));
        return result;
    }

    /// <summary>
    /// Probes actions 0-79 for a body and returns which ones actually have
    /// data, using the UOP hash table if the body is UOP-based, or classic
    /// Anim*.idx record lookups otherwise.
    /// </summary>
    public static List<int> GetDefinedActions(
        IReadOnlyList<string> animationFrameUopFilePaths,
        string uoDirectoryPath,
        string bodyConvDefPath,
        string mobTypesTxtPath,
        int body)
    {
        var defined = new List<int>();

        if (IsUopBody(mobTypesTxtPath, body))
        {
            foreach (var path in animationFrameUopFilePaths)
            {
                if (!File.Exists(path))
                    continue;

                var entries = uopFileReader.ReadEntries(path);
                var hashes = new HashSet<ulong>(entries.Select(e => e.Hash));

                for (int action = 0; action < MaxAnimActions; action++)
                {
                    string virtualPath = $"build/animationlegacyframe/{body:D6}/{action:D2}.bin";
                    if (hashes.Contains(UopHashHelper.HashFileName(virtualPath)))
                        defined.Add(action);
                }

                if (defined.Count > 0)
                    break; // first archive containing this body wins, mirroring GetFrames' lookup order
            }

            return defined;
        }

        var (fileType, resolvedBody) = BodyConverterReader.Resolve(bodyConvDefPath, body);
        string idxFileName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
        string? idxPath = ResolveCaseInsensitive(uoDirectoryPath, idxFileName);
        if (idxPath is null)
            return defined;

        for (int action = 0; action < MaxAnimActions; action++)
        {
            int recordIndex;
            try
            {
                recordIndex = ComputeRecordIndexForProbe(fileType, resolvedBody, action);
            }
            catch (ArgumentOutOfRangeException)
            {
                break;
            }

            if (MulIdxReader.ReadEntryAt(idxPath, recordIndex) is not null)
                defined.Add(action);
        }

        return defined;
    }

    // Mirrors ClassicAnimationReader.ComputeRecordIndex but for direction 0
    // only, since we're just probing whether the action's block exists.
    private static int ComputeRecordIndexForProbe(int fileType, int body, int action)
    {
        int index = fileType switch
        {
            1 or 2 or 4 or 6 => body < 200 ? body * 110 : body < 400 ? 22000 + (body - 200) * 65 : 35000 + (body - 400) * 175,
            3 => body < 300 ? body * 65 : body < 400 ? 33000 + (body - 300) * 110 : 35000 + (body - 400) * 175,
            5 => (body < 200 && body != 34) ? body * 110 : body < 400 ? 22000 + (body - 200) * 65 : 35000 + (body - 400) * 175,
            _ => throw new ArgumentOutOfRangeException(nameof(fileType))
        };

        return index + action * 5;
    }

    private static string? ResolveCaseInsensitive(string directoryPath, string fileName)
    {
        string direct = Path.Combine(directoryPath, fileName);
        if (File.Exists(direct))
            return direct;

        return Directory.EnumerateFiles(directoryPath)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    }
}
