using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools for UO's remaining semantic/reference data: localized strings
/// (Cliloc), speech keywords, radar map colors, and multi (house/boat)
/// component layouts.
/// </summary>
internal class SemanticTools
{
    [McpServerTool]
    [Description("Gets one localized string entry from a Cliloc.* file by its cliloc number.")]
    public string GetLocalizedString(
        [Description("Full path to the Cliloc file, e.g. Cliloc.enu.")] string clilocFilePath,
        [Description("The cliloc number to look up.")] int number)
    {
        if (!File.Exists(clilocFilePath))
            return $"File not found: {clilocFilePath}";

        try
        {
            var entry = ClilocReader.GetByNumber(clilocFilePath, number);
            if (entry is null)
                return $"No cliloc entry found for number {number}.";

            return $"Cliloc {entry.Number}: \"{entry.Text}\"";
        }
        catch (Exception ex)
        {
            return $"Failed to read cliloc {number}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches a Cliloc.* file for entries whose text contains the given search text.")]
    public string SearchLocalizedStrings(
        [Description("Full path to the Cliloc file, e.g. Cliloc.enu.")] string clilocFilePath,
        [Description("Text to search for within cliloc entry text.")] string searchText,
        [Description("How many matches to skip before returning results.")] int offset = 0,
        [Description("Maximum number of matches to return.")] int limit = 25)
    {
        if (!File.Exists(clilocFilePath))
            return $"File not found: {clilocFilePath}";

        try
        {
            var matches = ClilocReader.Search(clilocFilePath, searchText, offset, limit);
            if (matches.Count == 0)
                return $"No cliloc entries found matching '{searchText}'.";

            var sb = new StringBuilder();
            foreach (var entry in matches)
                sb.AppendLine($"{entry.Number}: \"{entry.Text}\"");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to search cliloc for '{searchText}': {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches speech.mul for speech-command keywords containing the given search text.")]
    public string SearchSpeech(
        [Description("Full path to speech.mul.")] string speechMulFilePath,
        [Description("Text to search for within speech keywords.")] string searchText)
    {
        if (!File.Exists(speechMulFilePath))
            return $"File not found: {speechMulFilePath}";

        try
        {
            var matches = SpeechReader.Search(speechMulFilePath, searchText);
            if (matches.Count == 0)
                return $"No speech keywords found matching '{searchText}'.";

            var sb = new StringBuilder();
            foreach (var entry in matches)
                sb.AppendLine($"{entry.Id}: \"{entry.Keyword}\"");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to search speech.mul for '{searchText}': {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets the RGB color at a given index in radarcol.mul, used for the in-game overhead radar/mini-map.")]
    public string GetRadarColor(
        [Description("Full path to radarcol.mul.")] string radarColFilePath,
        [Description("Index into the radar color table.")] int index)
    {
        if (!File.Exists(radarColFilePath))
            return $"File not found: {radarColFilePath}";

        try
        {
            var color = RadarColorReader.GetColor(radarColFilePath, index);
            return $"Radar color [{color.Index}]: #{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch (Exception ex)
        {
            return $"Failed to read radar color {index}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets a multi (house/boat) definition's component tiles with their relative offsets, from multi.idx/multi.mul.")]
    public string GetMulti(
        [Description("Full path to multi.idx.")] string multiIdxFilePath,
        [Description("Full path to multi.mul.")] string multiMulFilePath,
        [Description("The multi ID to look up.")] int multiId)
    {
        if (!File.Exists(multiIdxFilePath))
            return $"File not found: {multiIdxFilePath}";
        if (!File.Exists(multiMulFilePath))
            return $"File not found: {multiMulFilePath}";

        try
        {
            var detail = MultiReader.GetMulti(multiIdxFilePath, multiMulFilePath, multiId);
            if (detail is null)
                return $"No multi found for ID {multiId}.";

            var sb = new StringBuilder();
            sb.AppendLine($"Multi {detail.MultiId}: {detail.ComponentCount} components");
            foreach (var c in detail.Components)
                sb.AppendLine($"  ItemId=0x{c.ItemId:X4} X={c.X} Y={c.Y} Z={c.Z} Visible={c.Visible}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read multi {multiId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Lists bounded verdata.mul patch metadata (an optional legacy patch file that overrides records in other .mul files), including the patched source file name and record index.")]
    public string ListVerdataPatches(
        [Description("Full path to verdata.mul.")] string verdataMulFilePath,
        [Description("Zero-based patch offset to start from.")] int offset = 0,
        [Description("Maximum number of patches to return.")] int limit = 100)
    {
        if (!File.Exists(verdataMulFilePath))
            return $"File not found: {verdataMulFilePath}";

        try
        {
            var all = VerdataReader.ReadAll(verdataMulFilePath);
            if (all.Count == 0)
                return "verdata.mul contains no patches.";

            var page = all.Skip(offset).Take(Math.Max(0, limit)).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"{all.Count} total patch(es); showing {page.Count} starting at offset {offset}:");
            foreach (var p in page)
                sb.AppendLine($"  [{p.Index}] file={VerdataReader.GetFileName(p.FileId)} (id={p.FileId}) index={p.FileIndex} lookup={p.Lookup} length={p.Length} extra={p.Extra}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read verdata patches: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Lists ASCII font slots (fonts.mul) available from the client files, including glyph height.")]
    public string ListAsciiFonts(
        [Description("Full path to fonts.mul.")] string fontsMulFilePath)
    {
        if (!File.Exists(fontsMulFilePath))
            return $"File not found: {fontsMulFilePath}";

        try
        {
            var fonts = AsciiFontReader.ReadAll(fontsMulFilePath);
            if (fonts.Count == 0)
                return "No fonts found.";

            var sb = new StringBuilder();
            sb.AppendLine($"{fonts.Count} ASCII font(s):");
            foreach (var f in fonts)
            {
                int glyphCount = f.Glyphs.Count(g => g is not null);
                sb.AppendLine($"  Font {f.FontId}: height={f.Height}, {glyphCount} defined glyph(s)");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list ASCII fonts: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders a short line of text using an ASCII font (fonts.mul) to a PNG file and returns the saved file path.")]
    public string RenderAsciiText(
        [Description("Full path to fonts.mul.")] string fontsMulFilePath,
        [Description("ASCII font index (0-9).")] int fontId,
        [Description("Text to render.")] string text,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/Font/asciiText_<fontId>.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(fontsMulFilePath))
            return $"File not found: {fontsMulFilePath}";

        try
        {
            var fonts = AsciiFontReader.ReadAll(fontsMulFilePath);
            if (fontId < 0 || fontId >= fonts.Count)
                return $"Font {fontId} is out of range (0-{fonts.Count - 1}).";

            var rendered = AsciiFontReader.RenderText(fonts[fontId], text);
            if (rendered is null)
                return "Rendered text had zero width/height (empty or unsupported characters).";

            var (rgba, width, height) = rendered.Value;
            string resolvedPath = ExportPathResolver.Resolve(outputPngPath, $"asciiText_{fontId}.png", "Font");
            PngImageWriter.SaveRgbaAsPng(rgba, width, height, resolvedPath);

            return $"Rendered text with font {fontId} ({width}x{height}) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render ASCII text with font {fontId}: {ex.Message}";
        }
    }
}
