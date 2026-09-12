namespace vseUOAware.Services;

/// <summary>One animdata.mul entry: per-static-tile animation timing/frame-sequence metadata (e.g. for torches, water).</summary>
internal sealed record AnimDataEntry(sbyte[] FrameData, byte Unknown, byte FrameCount, byte FrameInterval, byte FrameStart);

/// <summary>
/// Reads animdata.mul: a flat file of fixed-size chunks (8 entries per
/// chunk, prefixed by a 4-byte header) describing which relative art-tile
/// offsets to cycle through, and how fast, for animated static item tiles.
/// Unlike most UO files this has no separate .idx - entries are addressed
/// directly by (id / 8) chunk position.
/// </summary>
internal static class AnimDataReader
{
    private const int ChunkHeaderSize = 4;
    private const int EntrySize = 64 + 4; // 64 signed frame-offset bytes + unk/frameCount/frameInterval/frameStart
    private const int EntriesPerChunk = 8;
    private const int ChunkSize = ChunkHeaderSize + EntrySize * EntriesPerChunk;

    /// <summary>Reads one animdata.mul entry by ID, or null if undefined/out of range.</summary>
    public static AnimDataEntry? GetAnimData(string animDataMulFilePath, int id)
    {
        if (id < 0)
            return null;

        int chunkIndex = id / EntriesPerChunk;
        int entryInChunk = id % EntriesPerChunk;

        long chunkOffset = (long)chunkIndex * ChunkSize;
        long entryOffset = chunkOffset + ChunkHeaderSize + (long)entryInChunk * EntrySize;

        using var stream = File.OpenRead(animDataMulFilePath);
        if (entryOffset + EntrySize > stream.Length)
            return null;

        using var reader = new BinaryReader(stream);
        stream.Seek(entryOffset, SeekOrigin.Begin);

        var frameData = new sbyte[64];
        for (int i = 0; i < 64; i++)
            frameData[i] = reader.ReadSByte();

        byte unknown = reader.ReadByte();
        byte frameCount = reader.ReadByte();
        byte frameInterval = reader.ReadByte();
        byte frameStart = reader.ReadByte();

        if (frameCount == 0)
            return null;

        return new AnimDataEntry(frameData, unknown, frameCount, frameInterval, frameStart);
    }
}
