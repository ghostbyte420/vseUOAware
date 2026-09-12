using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>Tools for rendering light source bitmaps from light.mul.</summary>
internal class LightTools
{
    [McpServerTool]
    [Description("Renders a light source bitmap from light.mul/lightidx.mul to a PNG file (grayscale intensity mask) and returns the saved file path.")]
    public string RenderLight(
        [Description("Full path to lightidx.mul.")] string lightIdxFilePath,
        [Description("Full path to light.mul.")] string lightMulFilePath,
        [Description("Light index, e.g. 0 for the first defined light.")] int lightId,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/Light/light<lightId>.png' if omitted.")] string? outputPngPath = null)
    {
        if (!File.Exists(lightIdxFilePath))
            return $"File not found: {lightIdxFilePath}";
        if (!File.Exists(lightMulFilePath))
            return $"File not found: {lightMulFilePath}";

        try
        {
            var result = LightReader.GetLight(lightIdxFilePath, lightMulFilePath, lightId);
            if (result is null)
                return $"Light {lightId} was not found or has no bitmap data.";

            var (rgba, width, height) = result.Value;
            string resolvedPath = ExportPathResolver.Resolve(outputPngPath, $"light{lightId}.png", "Light");
            PngImageWriter.SaveRgbaAsPng(rgba, width, height, resolvedPath);

            return $"Rendered light {lightId} ({width}x{height}) to {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to render light {lightId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Gets the number of defined lights in lightidx.mul.")]
    public string GetLightCount(
        [Description("Full path to lightidx.mul.")] string lightIdxFilePath)
    {
        if (!File.Exists(lightIdxFilePath))
            return $"File not found: {lightIdxFilePath}";

        try
        {
            return $"{LightReader.GetCount(lightIdxFilePath)} defined light(s).";
        }
        catch (Exception ex)
        {
            return $"Failed to get light count: {ex.Message}";
        }
    }
}
