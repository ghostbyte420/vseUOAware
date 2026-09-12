using System.IO.Compression;

namespace vseUOAware.Services;

/// <summary>
/// One entry inside a UOP (Ultima Offline Package) archive, such as
/// map1LegacyMUL.uop or artLegacyMUL.uop. Unlike classic .idx/.mul pairs,
/// UOP entries are found by walking a linked chain of "blocks" rather than
/// a flat table, and each entry may be zlib-compressed.
/// </summary>
internal sealed record UopEntry(
    int Index,
    long Offset,
    int HeaderLength,
    int CompressedLength,
    int DecompressedLength,
    ulong Hash,
    uint DataHash,
    short CompressionFlag);

/// <summary>
/// Reads UOP archives: a newer container format used by later UO clients
/// instead of classic .idx/.mul pairs. Entries are addressed by a hash of
/// their internal virtual filename rather than a sequential index, so we
/// simply enumerate them in on-disk order.
/// </summary>
internal static class uopFileReader
{
    private const uint UopMagic = 0x0050594D; // "MYP\0"

    /// <summary>Reads every entry (in on-disk block order) from a .uop file.</summary>
    public static List<UopEntry> ReadEntries(string uopFilePath)
    {
        using var stream = File.OpenRead(uopFilePath);
        using var reader = new BinaryReader(stream);

        uint magic = reader.ReadUInt32();
        if (magic != UopMagic)
            throw new InvalidDataException($"Not a UOP file (bad magic number): {uopFilePath}");

        reader.ReadUInt32(); // version - not needed
        reader.ReadUInt32(); // signature/format - not needed
        long nextBlock = reader.ReadInt64();
        reader.ReadUInt32(); // block capacity (entries per block) - not needed
        reader.ReadUInt32(); // total file count - informational only

        var entries = new List<UopEntry>();
        int index = 0;

        while (nextBlock != 0)
        {
            stream.Seek(nextBlock, SeekOrigin.Begin);

            int filesInBlock = reader.ReadInt32();
            nextBlock = reader.ReadInt64();

            for (int i = 0; i < filesInBlock; i++)
            {
                long offset = reader.ReadInt64();
                int headerLength = reader.ReadInt32();
                int compressedLength = reader.ReadInt32();
                int decompressedLength = reader.ReadInt32();
                ulong hash = reader.ReadUInt64();
                uint dataHash = reader.ReadUInt32();
                short flag = reader.ReadInt16();

                if (offset == 0)
                    continue;

                entries.Add(new UopEntry(index, offset, headerLength, compressedLength, decompressedLength, hash, dataHash, flag));
                index++;
            }
        }

        return entries;
    }

    /// <summary>
    /// Finds the entry whose stored hash matches the given 64-bit hash
    /// (typically computed from a virtual filename via UopHashHelper).
    /// Returns null if no entry matches.
    /// </summary>
    public static UopEntry? FindEntryByHash(List<UopEntry> entries, ulong hash)
        => entries.Find(e => e.Hash == hash);

    /// <summary>
    /// Pulls the raw bytes for ONE entry out of the .uop file, decompressing
    /// it with zlib first if the entry's compression flag says it's compressed.
    /// </summary>
    public static byte[] ReadEntryData(string uopFilePath, UopEntry entry)
    {
        using var stream = File.OpenRead(uopFilePath);
        stream.Seek(entry.Offset + entry.HeaderLength, SeekOrigin.Begin);

        var raw = new byte[entry.CompressedLength];
        stream.ReadExactly(raw, 0, entry.CompressedLength);

        if (entry.CompressionFlag == 0)
            return raw;

        // Compression flag 1 (and others) means zlib-compressed data.
        using var compressedStream = new MemoryStream(raw);
        using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var output = new MemoryStream(entry.DecompressedLength > 0 ? entry.DecompressedLength : entry.CompressedLength);
        zlib.CopyTo(output);
        return output.ToArray();
    }
}
