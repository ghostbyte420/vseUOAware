using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools for rendering and discovering UO body animations, covering both
/// storage formats used across client versions:
/// - Classic Anim.mul/Anim2.mul-Anim6.mul (.idx/.mul pairs) - most bodies.
/// - Newer AnimationFrame1-4.uop archives - bodies flagged in mobtypes.txt
///   with the 0x10000 "UOP body" bit (e.g. gargoyles).
/// The UOP path is the riskiest/most complex format this project decodes,
/// so results should be spot-checked visually before being trusted for
/// anything beyond exploration.
/// </summary>
internal class AnimationTools
{
    [McpServerTool]
    [Description("Lists every body ID defined in mobtypes.txt along with its category (monster/animal/human/etc.) and whether it uses the newer UOP animation format or the classic Anim*.mul format.")]
    public string ListAnimationBodies(
        [Description("Full path to mobtypes.txt.")] string mobTypesTxtFilePath,
        [Description("Optional text filter on category name (e.g. 'human', 'monster'). Case-insensitive substring match.")] string? categoryFilter = null)
    {
        if (!File.Exists(mobTypesTxtFilePath))
            return $"File not found: {mobTypesTxtFilePath}";

        try
        {
            var bodies = AnimationCatalog.GetCatalogedBodies(mobTypesTxtFilePath);
            if (!string.IsNullOrWhiteSpace(categoryFilter))
                bodies = bodies.Where(b => b.Category.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (bodies.Count == 0)
                return "No bodies matched.";

            var sb = new StringBuilder();
            sb.AppendLine($"{bodies.Count} bodies:");
            foreach (var (body, category, isUop) in bodies)
                sb.AppendLine($"  {body}\t{category}\t{(isUop ? "UOP format" : "classic Anim*.mul")}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to list animation bodies: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Lists which action indices (0-79) actually have animation data for a given body, so you don't have to guess. Works for both classic Anim*.mul bodies and newer UOP bodies.")]
    public string ListDefinedActions(
        [Description("Body/creature ID to probe.")] int body,
        [Description("Full path to the UO game folder (containing anim.idx/anim.mul, anim2-6, bodyconv.def, mobtypes.txt).")] string uoDirectoryPath,
        [Description("One or more full paths to AnimationFrame1.uop through AnimationFrame4.uop (only needed if the body turns out to be UOP-based).")] string[]? animationFrameUopFilePaths = null)
    {
        if (!Directory.Exists(uoDirectoryPath))
            return $"Directory not found: {uoDirectoryPath}";

        string mobTypesPath = Path.Combine(uoDirectoryPath, "mobtypes.txt");
        string bodyConvPath = Path.Combine(uoDirectoryPath, "Bodyconv.def");
        if (!File.Exists(bodyConvPath))
            bodyConvPath = Path.Combine(uoDirectoryPath, "bodyconv.def");

        try
        {
            bool isUop = File.Exists(mobTypesPath) && AnimationCatalog.IsUopBody(mobTypesPath, body);
            var actions = AnimationCatalog.GetDefinedActions(
                animationFrameUopFilePaths ?? Array.Empty<string>(),
                uoDirectoryPath,
                bodyConvPath,
                mobTypesPath,
                body);

            if (actions.Count == 0)
            {
                return isUop
                    ? $"Body {body} is UOP-based but no defined actions were found (did you pass animationFrameUopFilePaths?)."
                    : $"No defined actions found for body {body} in classic Anim*.mul files.";
            }

            return $"Body {body} ({(isUop ? "UOP format" : "classic Anim*.mul")}) has {actions.Count} defined action(s): {string.Join(", ", actions)}";
        }
        catch (Exception ex)
        {
            return $"Failed to list defined actions for body {body}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Renders every frame of one body/action/direction animation to numbered PNG files, returning the saved file paths. Automatically uses the classic Anim.mul/Anim2-6.mul format or the newer AnimationFrame*.uop format depending on the body. Complex/risky formats - verify output visually. Hue tinting is not supported (native frame palette only).")]
    public string RenderAnimationFrames(
        [Description("Body/creature ID, e.g. 400 for a human, 666 for a gargoyle male.")] int body,
        [Description("Action/animation index (0-79), e.g. 0 for walk, 4 for attack (varies by body). Use ListDefinedActions if unsure.")] int action,
        [Description("Facing direction (0-7). 0-4 are stored natively; 5-7 are mirrored copies of 3-1.")] int direction,
        [Description("Full path to the UO game folder (containing anim.idx/anim.mul, anim2-6, bodyconv.def, mobtypes.txt). Required for classic (non-UOP) bodies.")] string? uoDirectoryPath = null,
        [Description("One or more full paths to AnimationFrame1.uop through AnimationFrame4.uop. Required for UOP-based bodies (e.g. gargoyles).")] string[]? animationFrameUopFilePaths = null,
        [Description("Full path, or bare filename prefix, for output PNGs. Defaults to 'uoaExport/anim_<body>_<action>_<direction>_frame<N>.png' if omitted.")] string? outputPngPathPrefix = null)
    {
        try
        {
            string? mobTypesPath = uoDirectoryPath is not null ? Path.Combine(uoDirectoryPath, "mobtypes.txt") : null;
            bool isUop = mobTypesPath is not null && File.Exists(mobTypesPath) && AnimationCatalog.IsUopBody(mobTypesPath, body);

            List<AnimationFrameImage?>? frames;

            if (isUop || (animationFrameUopFilePaths is { Length: > 0 } && uoDirectoryPath is null))
            {
                if (animationFrameUopFilePaths is not { Length: > 0 })
                    return $"Body {body} is UOP-based (per mobtypes.txt) but no animationFrameUopFilePaths were provided.";

                var existing = animationFrameUopFilePaths.Where(File.Exists).ToArray();
                if (existing.Length == 0)
                    return $"None of the provided animation UOP files exist: {string.Join(", ", animationFrameUopFilePaths)}";

                frames = AnimationReader.GetFrames(existing, body, action, direction);
            }
            else
            {
                if (uoDirectoryPath is null || !Directory.Exists(uoDirectoryPath))
                    return $"uoDirectoryPath is required for classic (non-UOP) body {body} and must exist.";

                string bodyConvPath = Path.Combine(uoDirectoryPath, "Bodyconv.def");
                if (!File.Exists(bodyConvPath))
                    bodyConvPath = Path.Combine(uoDirectoryPath, "bodyconv.def");

                frames = ClassicAnimationReader.GetFrames(uoDirectoryPath, bodyConvPath, body, action, direction);
            }

            if (frames is null)
                return $"No animation data found for body {body}, action {action}, direction {direction}.";
            if (frames.Count == 0)
                return $"Body {body}, action {action}, direction {direction} resolved to zero frames.";

            string defaultPrefix = $"anim_{body}_{action}_{direction}";
            string prefixPath = ExportPathResolver.Resolve(outputPngPathPrefix, defaultPrefix, "Animation");
            string prefixWithoutExt = Path.Combine(
                Path.GetDirectoryName(prefixPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(prefixPath));

            var sb = new StringBuilder();
            sb.AppendLine($"Body {body} ({(isUop ? "UOP format" : "classic Anim*.mul")}), action {action}, direction {direction}: {frames.Count} frame(s).");

            int savedCount = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame is null)
                {
                    sb.AppendLine($"  Frame {i}: empty/placeholder (no pixel data).");
                    continue;
                }

                string framePath = $"{prefixWithoutExt}_frame{i}.png";
                PngImageWriter.SaveRgbaAsPng(frame.Rgba, frame.Width, frame.Height, framePath);
                sb.AppendLine($"  Frame {i}: {frame.Width}x{frame.Height}, center=({frame.CenterX},{frame.CenterY}) -> {framePath}");
                savedCount++;
            }

            if (savedCount == 0)
                sb.AppendLine("Warning: every resolved frame was an empty placeholder; the body/action/direction combination may not exist.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to render animation (body {body}, action {action}, direction {direction}): {ex.Message}. Use ListAnimationBodies/ListDefinedActions to verify the body/action exist.";
        }
    }
}
