namespace vseUOAware.Services;

/// <summary>One static-item component within a multi (house/boat) definition.</summary>
internal sealed record MultiComponent(int ItemId, int X, int Y, int Z, bool Visible);

/// <summary>Decoded multi.mul entry: the list of tile components that make up one multi.</summary>
internal sealed record MultiDetail(int MultiId, int ComponentCount, List<MultiComponent> Components);

/// <summary>
/// Reads multi.idx/multi.mul: definitions of "multis" (houses, boats, and
/// other multi-tile structures) as lists of component tiles with relative
/// offsets. Two on-disk record layouts exist depending on client version:
///   - Old (12 bytes/record): ushort ItemId, short X, short Y, short Z, uint Flags
///   - New (16 bytes/record): same as above plus a 4-byte "Unknown2" field
/// The record size is auto-detected per entry by dividing its total length,
/// so both old and new clients are supported without guessing.
/// </summary>
internal static class MultiReader
{
    private const int OldRecordSize = 12;
    private const int NewRecordSize = 16;

    public static MultiDetail? GetMulti(string multiIdxFilePath, string multiMulFilePath, int multiId)
    {
        var index = MulIdxReader.ReadIndex(multiIdxFilePath);
        var entry = index.Find(e => e.Index == multiId);
        if (entry is null)
            return null;

        int recordSize = entry.Length % NewRecordSize == 0 ? NewRecordSize : OldRecordSize;
        if (entry.Length % recordSize != 0)
            recordSize = OldRecordSize; // fall back; still best-effort if neither divides evenly

        var data = MulIdxReader.ReadEntryData(multiMulFilePath, entry);
        int count = data.Length / recordSize;

        var components = new List<MultiComponent>(count);
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            ushort itemId = reader.ReadUInt16();
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();
            short z = reader.ReadInt16();
            uint flags = reader.ReadUInt32();

            if (recordSize == NewRecordSize)
                reader.ReadInt32(); // Unknown2, no verified meaning - skipped

            components.Add(new MultiComponent(itemId, x, y, z, flags != 0));
        }

        return new MultiDetail(multiId, count, components);
    }
}
