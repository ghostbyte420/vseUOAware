namespace vseUOAware.Services;

/// <summary>One verdata.mul patch record: which original file/index it overrides, and where.</summary>
internal sealed record VerdataPatchEntry(int Index, int FileId, int FileIndex, int Lookup, int Length, int Extra);

/// <summary>
/// Reads verdata.mul: an optional legacy patch file that overrides
/// records in other .mul files (identified by a small FileId table, e.g.
/// 4 = art.mul, 6 = anim.mul, 30 = tiledata.mul). Format: a 4-byte count,
/// followed by that many 20-byte (File, Index, Lookup, Length, Extra) records.
/// </summary>
internal static class VerdataReader
{
    private static readonly IReadOnlyDictionary<int, string> FileNames = new Dictionary<int, string>
    {
        [0] = "map0.mul",
        [1] = "staidx0.mul",
        [2] = "statics0.mul",
        [3] = "artidx.mul",
        [4] = "art.mul",
        [5] = "anim.idx",
        [6] = "anim.mul",
        [7] = "soundidx.mul",
        [8] = "sound.mul",
        [9] = "texidx.mul",
        [10] = "texmaps.mul",
        [11] = "gumpidx.mul",
        [12] = "gumpart.mul",
        [13] = "multi.idx",
        [14] = "multi.mul",
        [15] = "skills.idx",
        [16] = "skills.mul",
        [30] = "tiledata.mul",
        [31] = "animdata.mul"
    };

    public static string GetFileName(int fileId) => FileNames.GetValueOrDefault(fileId, $"file{fileId}");

    public static List<VerdataPatchEntry> ReadAll(string verdataMulFilePath)
    {
        var results = new List<VerdataPatchEntry>();
        if (!File.Exists(verdataMulFilePath))
            return results;

        using var stream = File.OpenRead(verdataMulFilePath);
        using var reader = new BinaryReader(stream);

        int count = reader.ReadInt32();
        if (count < 0 || count > 5_000_000)
            throw new InvalidDataException($"Implausible verdata.mul patch count ({count}); refusing to parse.");

        for (int i = 0; i < count && stream.Position + 20 <= stream.Length; i++)
        {
            int fileId = reader.ReadInt32();
            int index = reader.ReadInt32();
            int lookup = reader.ReadInt32();
            int length = reader.ReadInt32();
            int extra = reader.ReadInt32();
            results.Add(new VerdataPatchEntry(i, fileId, index, lookup, length, extra));
        }

        return results;
    }
}
