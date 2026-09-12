using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>
/// Tools that render UO graphics (art tiles, gumps, hue swatches) to local
/// PNG files, since MCP tool results are text-only. Each tool writes the
/// image to a caller-supplied path and returns that path.
/// </summary>
internal class VisualRenderTools
{
    [McpServerTool]
    [Description("Renders a land or item art tile from artLegacyMUL.uop to a PNG file and returns the saved file path. Land tiles (ID < 0x4000) are saved under 'uoaExport/Land', item/static tiles (ID >= 0x4000) under 'uoaExport/Art', when outputPngPath is omitted or a bare filename.")]
    public string RenderArtTile(
        [Description("Full path to artLegacyMUL.uop.")] string artUopFilePath,
        [Description("Tile ID to render (land tiles are 0-0x3FFF, item tiles are 0x4000+).")] int tileId,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/art_0x<tileId>.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(artUopFilePath))
            return $"File not found: {artUopFilePath}";

        bool isLandTile = tileId < 0x4000;
        // Fiddler displays land tile IDs zero-padded to 4 hex digits (e.g. 0x0002);
        // item/static IDs are shown at their natural width (e.g. 0x420D).
        string hexId = isLandTile ? tileId.ToString("X4") : tileId.ToString("X");
        string category = isLandTile ? "Land" : "Art";
        string defaultFileName = isLandTile ? $"land_0x{hexId}.png" : $"art_0x{hexId}.png";
        string resolvedPath = ExportPathResolver.Resolve(outputPngPath, defaultFileName, category);

        try
        {
            var image = ArtReader.GetArtTile(artUopFilePath, tileId);
            if (image is null)
                return $"No art entry found for tile ID {tileId} (0x{hexId}).";

            PngImageWriter.SaveRgbaAsPng(image.Rgba, image.Width, image.Height, resolvedPath);
            string kind = isLandTile ? "LAND" : "ITEM";
            return $"Rendered {kind} tile {tileId} (0x{hexId}, {image.Width}x{image.Height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render art tile {tileId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders a gump graphic from gumpartLegacyMUL.uop to a PNG file and returns the saved file path. If outputPngPath is omitted or a bare filename, the file is written to a 'uoaExport' folder beside the MCP server project.")]
    public string RenderGump(
        [Description("Full path to gumpartLegacyMUL.uop.")] string gumpArtUopFilePath,
        [Description("Gump ID to render.")] int gumpId,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/gump<gumpId>.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(gumpArtUopFilePath))
            return $"File not found: {gumpArtUopFilePath}";

        string resolvedPath = ExportPathResolver.Resolve(outputPngPath, $"gump{gumpId}.png", "Gump");

        try
        {
            var image = GumpReader.GetGump(gumpArtUopFilePath, gumpId);
            if (image is null)
                return $"No gump entry found for gump ID {gumpId} (0x{gumpId:X}).";

            PngImageWriter.SaveRgbaAsPng(image.Rgba, image.Width, image.Height, resolvedPath);
            return $"Rendered gump {gumpId} (0x{gumpId:X}, {image.Width}x{image.Height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render gump {gumpId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders a hue's 32-color palette as a horizontal swatch strip PNG and returns the saved file path. If outputPngPath is omitted or a bare filename, the file is written to a 'uoaExport' folder beside the MCP server project.")]
    public string RenderHueSwatch(
        [Description("Full path to hues.mul.")] string huesFilePath,
        [Description("1-based hue ID (0 means 'no hue' and is not stored in the file).")] int hueId,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/hue<hueId>.png' if omitted.")] string? outputPngPath = null,
        [Description("Width in pixels of each color swatch cell.")] int cellSize = 16)
    {
        if (!File.Exists(huesFilePath))
            return $"File not found: {huesFilePath}";

        string resolvedPath = ExportPathResolver.Resolve(outputPngPath, $"hue{hueId}.png", "Hue");

        try
        {
            var hue = HuesReader.GetHue(huesFilePath, hueId);
            int width = hue.Colors.Count * cellSize;
            int height = cellSize;
            var rgba = new byte[width * height * 4];

            for (int c = 0; c < hue.Colors.Count; c++)
            {
                var color = hue.Colors[c];
                for (int y = 0; y < cellSize; y++)
                {
                    for (int x = 0; x < cellSize; x++)
                    {
                        int px = c * cellSize + x;
                        int i = (y * width + px) * 4;
                        rgba[i + 0] = color.R;
                        rgba[i + 1] = color.G;
                        rgba[i + 2] = color.B;
                        rgba[i + 3] = 255;
                    }
                }
            }

            PngImageWriter.SaveRgbaAsPng(rgba, width, height, resolvedPath);
            return $"Rendered hue {hue.HueId} (\"{hue.Name}\", {width}x{height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render hue {hueId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders the world overview map (Multimap.rle, a black/white RLE image) to a PNG file and returns the saved file path.")]
    public string RenderMultiMap(
        [Description("Full path to Multimap.rle.")] string multiMapRleFilePath,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/MultiMap/multimap.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(multiMapRleFilePath))
            return $"File not found: {multiMapRleFilePath}";

        try
        {
            var result = MultiMapReader.GetMultiMap(multiMapRleFilePath);
            if (result is null)
                return "Multimap.rle contained no renderable image data.";

            var (rgba, width, height) = result.Value;
            string resolvedPath = ExportPathResolver.Resolve(outputPngPath, "multimap.png", "MultiMap");
            PngImageWriter.SaveRgbaAsPng(rgba, width, height, resolvedPath);

            return $"Rendered multi-map ({width}x{height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render multi-map: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders a terrain (ground) texture from texmaps.mul/texidx.mul to a PNG file and returns the saved file path. These are the large repeating ground textures, distinct from land art tiles.")]
    public string RenderTexture(
        [Description("Full path to texidx.mul.")] string texIdxFilePath,
        [Description("Full path to texmaps.mul.")] string texMapsMulFilePath,
        [Description("Texture index to render.")] int textureId,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/Texture/texture_0x<textureId>.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(texIdxFilePath))
            return $"File not found: {texIdxFilePath}";
        if (!File.Exists(texMapsMulFilePath))
            return $"File not found: {texMapsMulFilePath}";

        try
        {
            var result = TextureReader.GetTexture(texIdxFilePath, texMapsMulFilePath, textureId);
            if (result is null)
                return $"No texture entry found for ID {textureId} (0x{textureId:X}).";

            var (rgba, width, height) = result.Value;
            string resolvedPath = ExportPathResolver.Resolve(outputPngPath, $"texture_0x{textureId:X}.png", "Texture");
            PngImageWriter.SaveRgbaAsPng(rgba, width, height, resolvedPath);

            return $"Rendered texture {textureId} (0x{textureId:X}, {width}x{height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render texture {textureId}: {ex.Message}";
        }
    }
}
