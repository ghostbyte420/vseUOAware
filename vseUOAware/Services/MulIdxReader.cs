namespace vseUOAware.Services;

/// <summary>
/// One "entry" from an .idx file: it tells us where in the matching
/// .mul file a piece of data lives, and how big it is.
/// Think of it like a page number + how many pages to read in a book.
/// </summary>
internal sealed record MulEntry(int Index, int Offset, int Length, int Extra);

/// <summary>
/// Reads classic UO ".idx" + ".mul" file PAIRS.
/// This format is used by art.mul, gumpart.mul, sound.mul, and others.
/// (NOT tiledata.mul -- that one is special and handled separately.)
/// </summary>
internal static class MulIdxReader
{
    // Each record in an .idx file is EXACTLY 12 bytes long:
    //   4 bytes = offset into the .mul file (where the data starts)
    //   4 bytes = length (how many bytes of data)
    //   4 bytes = "extra" (meaning depends on which file this is)
    private const int RecordSize = 12;

    /// <summary>Reads every entry (the "table of contents") from an .idx file.</summary>
    public static List<MulEntry> ReadIndex(string idxFilePath)
    {
        var entries = new List<MulEntry>();
        using var stream = File.OpenRead(idxFilePath);
        using var reader = new BinaryReader(stream);

        int index = 0;
        while (stream.Position + RecordSize <= stream.Length)
        {
            int offset = reader.ReadInt32();
            int length = reader.ReadInt32();
            int extra = reader.ReadInt32();

            // An offset of -1 means "this entry is empty / unused" -- skip it.
            if (offset >= 0 && length > 0)
                entries.Add(new MulEntry(index, offset, length, extra));

            index++;
        }

        return entries;
    }

    /// <summary>
    /// Reads a single record directly at a known record index (seeking
    /// straight to it) instead of reading the whole .idx file. Returns null
    /// if the index is out of range or the record is empty/unused.
    /// </summary>
    public static MulEntry? ReadEntryAt(string idxFilePath, int index)
    {
        long byteOffset = (long)index * RecordSize;

        using var stream = File.OpenRead(idxFilePath);
        if (byteOffset + RecordSize > stream.Length || index < 0)
            return null;

        using var reader = new BinaryReader(stream);
        stream.Seek(byteOffset, SeekOrigin.Begin);

        int offset = reader.ReadInt32();
        int length = reader.ReadInt32();
        int extra = reader.ReadInt32();

        return offset >= 0 && length > 0 ? new MulEntry(index, offset, length, extra) : null;
    }

    /// <summary>Pulls the raw bytes for ONE entry out of the big .mul file.</summary>
    public static byte[] ReadEntryData(string mulFilePath, MulEntry entry)
    {
        using var stream = File.OpenRead(mulFilePath);
        stream.Seek(entry.Offset, SeekOrigin.Begin);

        var buffer = new byte[entry.Length];
        stream.ReadExactly(buffer, 0, entry.Length);
        return buffer;
    }
}