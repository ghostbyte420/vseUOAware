using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace vseUOAware.Services;

/// <summary>Writes decoded RGBA pixel buffers (from ArtReader/GumpReader/HuesReader) out as PNG files.</summary>
internal static class PngImageWriter
{
    public static void SaveRgbaAsPng(byte[] rgba, int width, int height, string outputPngPath)
    {
        using var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    row[x] = new Rgba32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }
        });

        var directory = Path.GetDirectoryName(outputPngPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        image.SaveAsPng(outputPngPath);
    }
}
