namespace vseUOAware.Services;

/// <summary>
/// Decodes classic UO map block data (from a map*LegacyMUL.uop entry) into
/// a small downsampled ASCII picture, using each 8x8 block's average tile
/// altitude (Z) as a rough stand-in for terrain height/water/land.
/// </summary>
internal static class MapAsciiRenderer
{
    // Each map block is 4 header bytes + 64 tiles * (2-byte land ID + 1-byte Z).
    private const int BlockSize = 4 + 64 * 3;
    private const int TilesPerBlock = 64;

    /// <summary>
    /// Renders a facet's map data (already read from its .uop entries, in
    /// on-disk/offset order) into an ASCII grid of outputWidth x outputHeight,
    /// using average block altitude per output cell.
    /// </summary>
    public static string Render(
        string uopFilePath,
        int outputWidth,
        int outputHeight,
        int facetWidthBlocks,
        int facetHeightBlocks)
    {
        var entries = uopFileReader.ReadEntries(uopFilePath)
            .OrderBy(e => e.Offset)
            .ToList();

        int totalBlocks = facetWidthBlocks * facetHeightBlocks;
        var blockZ = new sbyte[totalBlocks];
        var filled = new bool[totalBlocks];
        int blockIndex = 0;

        foreach (var entry in entries)
        {
            byte[] data;
            try
            {
                data = uopFileReader.ReadEntryData(uopFilePath, entry);
            }
            catch
            {
                continue; // skip unreadable entries rather than failing the whole render
            }

            int blocksInEntry = data.Length / BlockSize;
            for (int b = 0; b < blocksInEntry && blockIndex < totalBlocks; b++, blockIndex++)
            {
                int baseOffset = b * BlockSize;
                int sum = 0;
                for (int t = 0; t < TilesPerBlock; t++)
                {
                    int zOffset = baseOffset + 4 + t * 3 + 2;
                    sum += (sbyte)data[zOffset];
                }

                blockZ[blockIndex] = (sbyte)(sum / TilesPerBlock);
                filled[blockIndex] = true;
            }
        }

        var sb = new System.Text.StringBuilder();
        double blocksPerCellX = (double)facetWidthBlocks / outputWidth;
        double blocksPerCellY = (double)facetHeightBlocks / outputHeight;

        for (int cy = 0; cy < outputHeight; cy++)
        {
            int by0 = (int)(cy * blocksPerCellY);
            int by1 = Math.Max(by0 + 1, (int)((cy + 1) * blocksPerCellY));

            for (int cx = 0; cx < outputWidth; cx++)
            {
                int bx0 = (int)(cx * blocksPerCellX);
                int bx1 = Math.Max(bx0 + 1, (int)((cx + 1) * blocksPerCellX));

                int sum = 0;
                int count = 0;
                for (int by = by0; by < by1 && by < facetHeightBlocks; by++)
                {
                    for (int bx = bx0; bx < bx1 && bx < facetWidthBlocks; bx++)
                    {
                        int idx = by * facetWidthBlocks + bx;
                        if (idx < totalBlocks && filled[idx])
                        {
                            sum += blockZ[idx];
                            count++;
                        }
                    }
                }

                sb.Append(count == 0 ? ' ' : ToChar(sum / (double)count));
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static char ToChar(double z) => z switch
    {
        <= -20 => '~', // deep water
        <= -2 => '.',  // shallow water / low ground
        <= 15 => ':',  // plains
        <= 35 => '+',  // hills
        <= 60 => '*',  // mountains
        _ => '^',      // peaks
    };
}
