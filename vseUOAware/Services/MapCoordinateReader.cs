namespace vseUOAware.Services;

/// <summary>One static (placed) item found at a map coordinate.</summary>
internal sealed record MapStaticTileInfo(int TileId, int X, int Y, int Z, int Hue);

/// <summary>Land + statics info for a single world coordinate on one facet.</summary>
internal sealed record MapCoordinateInfo(int X, int Y, int LandTileId, int LandZ, IReadOnlyList<MapStaticTileInfo> Statics);

/// <summary>
/// Looks up land tile/Z and static items at a specific world (x, y) coordinate
/// on a facet, by combining the facet's map*LegacyMUL.uop block data (land)
/// with the classic staticsN.mul/staidxN.mul pair (statics). This relies on
/// the same "entries are laid out in sequential block order" assumption used
/// by RenderMapAscii, so results are best-effort rather than guaranteed exact.
/// </summary>
internal static class MapCoordinateReader
{
    private const int BlockSize = 4 + 64 * 3; // 4-byte header + 64 tiles * (ushort ID + sbyte Z)
    private const int StaticRecordSize = 7;    // ushort TileID + byte X + byte Y + sbyte Z + ushort Hue

    public static MapCoordinateInfo GetCoordinate(
        string mapUopFilePath,
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int facetWidthBlocks,
        int x,
        int y,
        bool includeStatics = true)
    {
        int blockX = x / 8;
        int blockY = y / 8;
        int tileXInBlock = x % 8;
        int tileYInBlock = y % 8;
        int blockIndex = blockY * facetWidthBlocks + blockX;

        var (landTileId, landZ) = ReadLand(mapUopFilePath, blockIndex, tileXInBlock, tileYInBlock);

        var statics = includeStatics
            ? ReadStatics(staticsMulFilePath, staIdxMulFilePath, blockIndex, tileXInBlock, tileYInBlock)
            : [];

        return new MapCoordinateInfo(x, y, landTileId, landZ, statics);
    }

    /// <summary>
    /// Reads a bounded rectangular region of world coordinates, returning
    /// land/statics for every tile. Reuses the UOP entry list and open file
    /// handles across the whole region instead of re-reading them per tile,
    /// making this far cheaper than repeated single-coordinate calls.
    /// </summary>
    public static List<MapCoordinateInfo> GetRegion(
        string mapUopFilePath,
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int facetWidthBlocks,
        int x,
        int y,
        int width,
        int height,
        bool includeStatics = true)
    {
        var entries = uopFileReader.ReadEntries(mapUopFilePath).OrderBy(e => e.Offset).ToList();
        var landBlockCache = new Dictionary<int, byte[]>();

        using var idxStream = includeStatics ? File.OpenRead(staIdxMulFilePath) : null;
        using var idxReader = includeStatics ? new BinaryReader(idxStream!) : null;
        using var staticsStream = includeStatics ? File.OpenRead(staticsMulFilePath) : null;
        using var staticsReader = includeStatics ? new BinaryReader(staticsStream!) : null;

        var results = new List<MapCoordinateInfo>(width * height);

        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int worldX = x + dx;
                int worldY = y + dy;

                int blockX = worldX / 8;
                int blockY = worldY / 8;
                int tileXInBlock = worldX % 8;
                int tileYInBlock = worldY % 8;
                int blockIndex = blockY * facetWidthBlocks + blockX;

                var (landTileId, landZ) = ReadLandCached(entries, mapUopFilePath, landBlockCache, blockIndex, tileXInBlock, tileYInBlock);

                var statics = includeStatics
                    ? ReadStaticsWithReaders(idxReader!, staticsReader!, staticsStream!, blockIndex, tileXInBlock, tileYInBlock)
                    : [];

                results.Add(new MapCoordinateInfo(worldX, worldY, landTileId, landZ, statics));
            }
        }

        return results;
    }

    private static (int TileId, int Z) ReadLandCached(
        List<UopEntry> entries,
        string mapUopFilePath,
        Dictionary<int, byte[]> blockCache,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        if (!blockCache.TryGetValue(blockIndex, out var data) || data is null)
        {
            long blocksSoFar = 0;
            byte[]? found = null;
            int localBlockIndex = 0;

            foreach (var entry in entries)
            {
                long blocksInEntry = entry.DecompressedLength / BlockSize;
                if (blockIndex < blocksSoFar + blocksInEntry)
                {
                    var entryData = uopFileReader.ReadEntryData(mapUopFilePath, entry);
                    localBlockIndex = (int)(blockIndex - blocksSoFar);
                    int blockOffset = localBlockIndex * BlockSize;
                    found = entryData.AsSpan(blockOffset, BlockSize).ToArray();
                    break;
                }

                blocksSoFar += blocksInEntry;
            }

            if (found is null)
                throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapUopFilePath}.");

            data = found;
            blockCache[blockIndex] = data;
        }

        int tileIndex = tileYInBlock * 8 + tileXInBlock;
        int tileOffset = 4 + tileIndex * 3;

        int tileId = BitConverter.ToUInt16(data, tileOffset);
        int z = (sbyte)data[tileOffset + 2];
        return (tileId, z);
    }

    private static List<MapStaticTileInfo> ReadStaticsWithReaders(
        BinaryReader idxReader,
        BinaryReader staticsReader,
        FileStream staticsStream,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        long idxOffset = (long)blockIndex * 12;
        if (idxOffset + 12 > idxReader.BaseStream.Length)
            return [];

        idxReader.BaseStream.Seek(idxOffset, SeekOrigin.Begin);
        int offset = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        idxReader.ReadInt32(); // extra - unused here

        if (offset < 0 || length <= 0)
            return [];

        staticsStream.Seek(offset, SeekOrigin.Begin);

        int recordCount = length / StaticRecordSize;
        var results = new List<MapStaticTileInfo>();

        for (int i = 0; i < recordCount; i++)
        {
            int tileId = staticsReader.ReadUInt16();
            int recordX = staticsReader.ReadByte();
            int recordY = staticsReader.ReadByte();
            int z = staticsReader.ReadSByte();
            int hue = staticsReader.ReadUInt16();

            if (recordX == tileXInBlock && recordY == tileYInBlock)
                results.Add(new MapStaticTileInfo(tileId, recordX, recordY, z, hue));
        }

        return results;
    }

    private static (int TileId, int Z) ReadLand(string mapUopFilePath, int blockIndex, int tileXInBlock, int tileYInBlock)
    {
        var entries = uopFileReader.ReadEntries(mapUopFilePath).OrderBy(e => e.Offset).ToList();

        long blocksSoFar = 0;
        foreach (var entry in entries)
        {
            long blocksInEntry = entry.DecompressedLength / BlockSize;
            if (blockIndex < blocksSoFar + blocksInEntry)
            {
                var data = uopFileReader.ReadEntryData(mapUopFilePath, entry);
                int localBlockIndex = (int)(blockIndex - blocksSoFar);
                int blockOffset = localBlockIndex * BlockSize;
                int tileIndex = tileYInBlock * 8 + tileXInBlock;
                int tileOffset = blockOffset + 4 + tileIndex * 3;

                int tileId = BitConverter.ToUInt16(data, tileOffset);
                int z = (sbyte)data[tileOffset + 2];
                return (tileId, z);
            }

            blocksSoFar += blocksInEntry;
        }

        throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapUopFilePath}.");
    }

    private static List<MapStaticTileInfo> ReadStatics(
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        using var idxStream = File.OpenRead(staIdxMulFilePath);
        long idxOffset = (long)blockIndex * 12;
        if (idxOffset + 12 > idxStream.Length)
            return [];

        using var idxReader = new BinaryReader(idxStream);
        idxStream.Seek(idxOffset, SeekOrigin.Begin);
        int offset = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        idxReader.ReadInt32(); // extra - unused here

        if (offset < 0 || length <= 0)
            return [];

        using var staticsStream = File.OpenRead(staticsMulFilePath);
        staticsStream.Seek(offset, SeekOrigin.Begin);
        using var staticsReader = new BinaryReader(staticsStream);

        int recordCount = length / StaticRecordSize;
        var results = new List<MapStaticTileInfo>();

        for (int i = 0; i < recordCount; i++)
        {
            int tileId = staticsReader.ReadUInt16();
            int recordX = staticsReader.ReadByte();
            int recordY = staticsReader.ReadByte();
            int z = staticsReader.ReadSByte();
            int hue = staticsReader.ReadUInt16();

            if (recordX == tileXInBlock && recordY == tileYInBlock)
                results.Add(new MapStaticTileInfo(tileId, recordX, recordY, z, hue));
        }

        return results;
    }
}
