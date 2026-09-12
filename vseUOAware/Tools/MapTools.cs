using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools for inspecting a specific world coordinate on a facet: land tile/Z
/// and any static items placed there.
/// </summary>
internal class MapTools
{
    [McpServerTool]
    [Description("Inspects one UO world coordinate on a facet: land tile ID/Z and any static items placed there. Best-effort - relies on the map*LegacyMUL.uop entries being laid out in sequential block order.")]
    public string InspectMapCoordinate(
        [Description("Full path to the facet's map*LegacyMUL.uop file, e.g. map1LegacyMUL.uop.")] string mapUopFilePath,
        [Description("Full path to the matching staticsN.mul file, e.g. statics1.mul.")] string staticsMulFilePath,
        [Description("Full path to the matching staidxN.mul file, e.g. staidx1.mul.")] string staIdxMulFilePath,
        [Description("World X coordinate.")] int x,
        [Description("World Y coordinate.")] int y,
        [Description("Width of the facet in 8x8-tile blocks (768 for facets 0/1, 288 for facet 2/5, 160 for facet 3, 181 for facet 4).")] int facetWidthBlocks = 768,
        [Description("Whether to also include static items at this tile.")] bool includeStatics = true)
    {
        if (!File.Exists(mapUopFilePath))
            return $"File not found: {mapUopFilePath}";
        if (includeStatics && !File.Exists(staticsMulFilePath))
            return $"File not found: {staticsMulFilePath}";
        if (includeStatics && !File.Exists(staIdxMulFilePath))
            return $"File not found: {staIdxMulFilePath}";

        try
        {
            var info = MapCoordinateReader.GetCoordinate(mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, includeStatics);

            var sb = new StringBuilder();
            sb.AppendLine($"Coordinate ({info.X}, {info.Y}): land tile 0x{info.LandTileId:X4} ({info.LandTileId}), Z={info.LandZ}");

            if (includeStatics)
            {
                if (info.Statics.Count == 0)
                {
                    sb.AppendLine("No static items at this tile.");
                }
                else
                {
                    sb.AppendLine($"{info.Statics.Count} static item(s):");
                    foreach (var s in info.Statics)
                        sb.AppendLine($"  - tile 0x{s.TileId:X4} ({s.TileId}), Z={s.Z}, hue={s.Hue}");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to inspect coordinate ({x}, {y}): {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Reads a bounded rectangular coordinate region for land tiles/elevation and statics. Limited to 64x64 tiles per call to keep output manageable.")]
    public string InspectMapRegion(
        [Description("Full path to the facet's map*LegacyMUL.uop file, e.g. map1LegacyMUL.uop.")] string mapUopFilePath,
        [Description("Full path to the matching staticsN.mul file, e.g. statics1.mul.")] string staticsMulFilePath,
        [Description("Full path to the matching staidxN.mul file, e.g. staidx1.mul.")] string staIdxMulFilePath,
        [Description("World X coordinate of the region's top-left corner.")] int x,
        [Description("World Y coordinate of the region's top-left corner.")] int y,
        [Description("Region width in tiles (max 64).")] int width,
        [Description("Region height in tiles (max 64).")] int height,
        [Description("Width of the facet in 8x8-tile blocks (768 for facets 0/1, 288 for facet 2/5, 160 for facet 3, 181 for facet 4).")] int facetWidthBlocks = 768,
        [Description("Whether to also include static items at each tile.")] bool includeStatics = true)
    {
        if (!File.Exists(mapUopFilePath))
            return $"File not found: {mapUopFilePath}";
        if (includeStatics && !File.Exists(staticsMulFilePath))
            return $"File not found: {staticsMulFilePath}";
        if (includeStatics && !File.Exists(staIdxMulFilePath))
            return $"File not found: {staIdxMulFilePath}";

        if (width <= 0 || height <= 0)
            return "Width and height must both be positive.";
        if (width > 64 || height > 64)
            return "Width and height are each limited to 64 tiles per call to keep output manageable.";

        try
        {
            var region = MapCoordinateReader.GetRegion(mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, width, height, includeStatics);

            var sb = new StringBuilder();
            sb.AppendLine($"Region ({x}, {y}) to ({x + width - 1}, {y + height - 1}): {region.Count} tile(s).");

            foreach (var info in region)
            {
                sb.Append($"({info.X}, {info.Y}): land 0x{info.LandTileId:X4} ({info.LandTileId}), Z={info.LandZ}");

                if (includeStatics)
                {
                    if (info.Statics.Count == 0)
                    {
                        sb.AppendLine(" - no statics");
                    }
                    else
                    {
                        sb.AppendLine($" - {info.Statics.Count} static(s):");
                        foreach (var s in info.Statics)
                            sb.AppendLine($"    - tile 0x{s.TileId:X4} ({s.TileId}), Z={s.Z}, hue={s.Hue}");
                    }
                }
                else
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to inspect region ({x}, {y}, {width}x{height}): {ex.Message}";
        }
    }
}
