using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools that let Copilot read your Ultima Online / RunUO / uoAvox folder:
/// plain text files directly, and classic .idx/.mul binary pairs via a
/// generic reader. (tiledata.mul item-name lookups come in a later step.)
/// </summary>
internal class FileTools
{
    [McpServerTool]
    [Description("Returns the currently remembered UO folder path, or a message saying none is saved yet.")]
    public string GetSavedUoDirectory()
    {
        var saved = DirectoryService.LoadSavedDirectory();
        return saved is null
            ? "No UO folder has been saved yet. Call SetUoDirectory with a path first."
            : saved;
    }

    [McpServerTool]
    [Description("Saves/confirms the UO folder path to remember for future tool calls.")]
    public string SetUoDirectory(
        [Description("Full path to the UO/RunUO/uoAvox folder, e.g. D:\\UO")] string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return $"That folder does not exist: {directoryPath}";

        DirectoryService.SaveDirectory(directoryPath);
        return $"Saved UO folder: {directoryPath}";
    }

    [McpServerTool]
    [Description("Lists every file in the UO folder (recursively) with its extension and size.")]
    public string ListUoFiles(
        [Description("Full path to the UO folder to scan.")] string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return $"That folder does not exist: {directoryPath}";

        var sb = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            sb.AppendLine($"{info.Name}\t{info.Extension}\t{info.Length} bytes");
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Reads a plain text UO file (.cfg, .txt, .cs, .html) and returns its contents.")]
    public string ReadUoTextFile(
        [Description("Full path to the text file to read.")] string filePath)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        return File.ReadAllText(filePath);
    }

    [McpServerTool]
    [Description("Reads a classic .idx file and lists its entries (offset, length, extra) for a matching .mul file.")]
    public string ListMulEntries(
        [Description("Full path to the .idx file, e.g. artidx.mul's index file.")] string idxFilePath,
        [Description("Maximum number of entries to return (to avoid huge output).")] int maxEntries = 50)
    {
        if (!File.Exists(idxFilePath))
            return $"File not found: {idxFilePath}";

        var entries = MulIdxReader.ReadIndex(idxFilePath);
        var sb = new StringBuilder();
        sb.AppendLine($"Total entries: {entries.Count}");

        foreach (var entry in entries.Take(maxEntries))
            sb.AppendLine($"#{entry.Index}: offset={entry.Offset}, length={entry.Length}, extra={entry.Extra}");

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Extracts the raw bytes of one entry from a .mul file (using its .idx table) and returns a hex preview.")]
    public string ReadMulEntryPreview(
        [Description("Full path to the .idx file.")] string idxFilePath,
        [Description("Full path to the matching .mul file.")] string mulFilePath,
        [Description("Which entry index to read.")] int entryIndex,
        [Description("How many bytes to show in the preview.")] int previewBytes = 32)
    {
        if (!File.Exists(idxFilePath))
            return $"File not found: {idxFilePath}";
        if (!File.Exists(mulFilePath))
            return $"File not found: {mulFilePath}";

        var entries = MulIdxReader.ReadIndex(idxFilePath);
        var entry = entries.FirstOrDefault(e => e.Index == entryIndex);
        if (entry is null)
            return $"No entry found at index {entryIndex}.";

        var data = MulIdxReader.ReadEntryData(mulFilePath, entry);
        var previewLength = Math.Min(previewBytes, data.Length);
        var hex = Convert.ToHexString(data, 0, previewLength);

        return $"Entry #{entryIndex}: {data.Length} total bytes. First {previewLength} bytes (hex): {hex}";
    }

    [McpServerTool]
    [Description("Searches tiledata.mul for item names matching the search text and returns their tile/item IDs.")]
    public string FindItemIdByName(
    [Description("Full path to tiledata.mul.")] string tileDataFilePath,
    [Description("Text to search for in item names, e.g. 'sandals'.")] string searchText)
    {
        if (!File.Exists(tileDataFilePath))
            return $"File not found: {tileDataFilePath}";

        var matches = TileDataReader.FindByName(tileDataFilePath, searchText);
        if (matches.Count == 0)
            return $"No items found matching '{searchText}'.";

        var sb = new StringBuilder();
        foreach (var match in matches)
            sb.AppendLine($"ID 0x{match.TileId:X4} ({match.TileId}): \"{match.Name}\" [{(match.IsStaticItem ? "static/item" : "land")}]");

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Gets complete tiledata.mul metadata for a LAND tile ID (0 to 16383): name, texture ID, and decoded flag names (e.g. Wet, Impassable, NoDiagonal).")]
    public string GetLandTile(
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Land tile ID from 0 through 16383.")] int tileId)
    {
        if (!File.Exists(tileDataFilePath))
            return $"File not found: {tileDataFilePath}";

        try
        {
            var detail = TileDataReader.GetLandDetail(tileDataFilePath, tileId);
            var flagList = detail.FlagNames.Count > 0 ? string.Join(", ", detail.FlagNames) : "(none)";
            return $"Land tile 0x{detail.TileId:X4} ({detail.TileId}): \"{detail.Name}\", textureId=0x{detail.TextureId:X4}, flags=[{flagList}] (raw=0x{(ulong)detail.Flags:X})";
        }
        catch (Exception ex)
        {
            return $"Failed to read land tile 0x{tileId:X4}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets complete tiledata.mul metadata for an ITEM/static tile ID (0x4000+): name, decoded flag names (e.g. Wearable, Surface, LightSource), and remaining raw attribute bytes (weight/quality/hue/height/etc., as hex, since their exact byte order varies by client version).")]
    public string GetItemTile(
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Item tile ID in decimal, such as 0x0EED expressed as 3821 (must be >= 16384).")] int tileId)
    {
        if (!File.Exists(tileDataFilePath))
            return $"File not found: {tileDataFilePath}";

        try
        {
            var detail = TileDataReader.GetItemDetail(tileDataFilePath, tileId);
            var flagList = detail.FlagNames.Count > 0 ? string.Join(", ", detail.FlagNames) : "(none)";
            return $"Item tile 0x{detail.TileId:X4} ({detail.TileId}): \"{detail.Name}\", flags=[{flagList}] (raw=0x{(ulong)detail.Flags:X}), rawAttributes(hex)={detail.RawAttributesHex}";
        }
        catch (Exception ex)
        {
            return $"Failed to read item tile 0x{tileId:X4}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Reads a .uop archive (e.g. map1LegacyMUL.uop, artLegacyMUL.uop) and lists its entries (offset, compressed/decompressed length, hash) in on-disk order.")]
    public string ListUopEntries(
        [Description("Full path to the .uop file.")] string uopFilePath,
        [Description("Maximum number of entries to return (to avoid huge output).")] int maxEntries = 50)
    {
        if (!File.Exists(uopFilePath))
            return $"File not found: {uopFilePath}";

        var entries = TryReadUopEntries(uopFilePath, out var error);
        if (error is not null)
            return error;

        var sb = new StringBuilder();
        sb.AppendLine($"Total entries: {entries!.Count}");

        foreach (var entry in entries.Take(maxEntries))
            sb.AppendLine($"#{entry.Index}: offset={entry.Offset}, compressedLength={entry.CompressedLength}, decompressedLength={entry.DecompressedLength}, hash={entry.Hash}, compressionFlag={entry.CompressionFlag}");

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Extracts (and zlib-decompresses if needed) the bytes of one entry from a .uop archive and returns a hex preview.")]
    public string ReadUopEntryPreview(
        [Description("Full path to the .uop file.")] string uopFilePath,
        [Description("Which entry index to read (in on-disk block order, as returned by ListUopEntries).")] int entryIndex,
        [Description("How many bytes to show in the preview.")] int previewBytes = 32)
    {
        if (!File.Exists(uopFilePath))
            return $"File not found: {uopFilePath}";

        var entries = TryReadUopEntries(uopFilePath, out var error);
        if (error is not null)
            return error;

        var entry = entries!.FirstOrDefault(e => e.Index == entryIndex);
        if (entry is null)
            return $"No entry found at index {entryIndex}.";

        byte[] data;
        try
        {
            data = uopFileReader.ReadEntryData(uopFilePath, entry);
        }
        catch (Exception ex)
        {
            return $"Failed to read/decompress entry #{entryIndex}: {ex.Message}";
        }

        var previewLength = Math.Min(previewBytes, data.Length);
        var hex = Convert.ToHexString(data, 0, previewLength);

        return $"Entry #{entryIndex}: {data.Length} total bytes (decompressed). First {previewLength} bytes (hex): {hex}";
    }

    private static List<UopEntry>? TryReadUopEntries(string uopFilePath, out string? error)
    {
        try
        {
            error = null;
            return uopFileReader.ReadEntries(uopFilePath);
        }
        catch (Exception ex)
        {
            error = $"Failed to read UOP file: {ex.Message}";
            return null;
        }
    }

    [McpServerTool]
    [Description("Renders a downsampled ASCII terrain map from a map*LegacyMUL.uop file, using average block altitude. Useful for a quick visual overview of a facet.")]
    public string RenderMapAscii(
        [Description("Full path to the map*LegacyMUL.uop file, e.g. map1LegacyMUL.uop.")] string uopFilePath,
        [Description("Width in characters of the ASCII output.")] int outputWidth = 100,
        [Description("Height in characters of the ASCII output.")] int outputHeight = 50,
        [Description("Width of the facet in 8x8-tile blocks (768 for facets 0/1, 288 for facet 2/5, 160 for facet 3, 181 for facet 4).")] int facetWidthBlocks = 768,
        [Description("Height of the facet in 8x8-tile blocks (512 for facets 0/1, 200 for facet 2/5, 512 for facet 3, 108 for facet 4).")] int facetHeightBlocks = 512)
    {
        if (!File.Exists(uopFilePath))
            return $"File not found: {uopFilePath}";

        try
        {
            var art = MapAsciiRenderer.Render(uopFilePath, outputWidth, outputHeight, facetWidthBlocks, facetHeightBlocks);
            return art;
        }
        catch (Exception ex)
        {
            return $"Failed to render map: {ex.Message}";
        }
    }
}