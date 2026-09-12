namespace vseUOAware.Services;

/// <summary>One ASCII font glyph: fixed-size 16bpp bitmap (0 = transparent, else color).</summary>
internal sealed record AsciiGlyph(int Width, int Height, ushort[] Pixels);

/// <summary>One parsed ASCII font (fonts.mul): 224 glyphs covering characters 0x20-0xFF.</summary>
internal sealed record AsciiFontData(int FontId, int Height, AsciiGlyph?[] Glyphs);

/// <summary>
/// Reads fonts.mul: 10 fixed-width-ish ASCII fonts (chat/menu text), each
/// with 224 glyphs (character codes 0x20 upward). Every glyph is a 16bpp
/// bitmap where 0 means transparent and any other value is the pixel
/// color XORed with 0x8000.
/// </summary>
internal static class AsciiFontReader
{
    private const int GlyphsPerFont = 224;
    private const int FontCount = 10;

    public static List<AsciiFontData> ReadAll(string fontsMulFilePath)
    {
        var results = new List<AsciiFontData>();
        if (!File.Exists(fontsMulFilePath))
            return results;

        using var stream = File.OpenRead(fontsMulFilePath);
        using var reader = new BinaryReader(stream);

        for (int f = 0; f < FontCount && stream.Position < stream.Length; f++)
        {
            reader.ReadByte(); // header byte
            var glyphs = new AsciiGlyph?[GlyphsPerFont];
            int fontHeight = 0;

            for (int k = 0; k < GlyphsPerFont; k++)
            {
                if (stream.Position + 3 > stream.Length)
                    break;

                byte width = reader.ReadByte();
                byte height = reader.ReadByte();
                reader.ReadByte(); // unk/delimiter

                if (width == 0 || height == 0)
                    continue;

                var pixels = new ushort[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    ushort raw = reader.ReadUInt16();
                    pixels[i] = raw == 0 ? (ushort)0 : (ushort)(raw ^ 0x8000);
                }

                glyphs[k] = new AsciiGlyph(width, height, pixels);
                if (height > fontHeight && k < 96)
                    fontHeight = height;
            }

            results.Add(new AsciiFontData(f, fontHeight, glyphs));
        }

        return results;
    }

    /// <summary>Renders a string using one ASCII font to a flat RGBA buffer.</summary>
    public static (byte[] Rgba, int Width, int Height)? RenderText(AsciiFontData font, string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        var glyphs = new List<AsciiGlyph>();
        int totalWidth = 0;
        int maxHeight = 0;

        foreach (char c in text)
        {
            int idx = ((c - 0x20) & 0x7FFFFFFF) % GlyphsPerFont;
            var glyph = font.Glyphs[idx];
            if (glyph is null)
                continue;

            glyphs.Add(glyph);
            totalWidth += glyph.Width;
            maxHeight = Math.Max(maxHeight, glyph.Height);
        }

        if (totalWidth == 0 || maxHeight == 0)
            return null;

        var rgba = new byte[totalWidth * maxHeight * 4];
        int dx = 0;
        foreach (var glyph in glyphs)
        {
            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    ushort pixel = glyph.Pixels[y * glyph.Width + x];
                    if (pixel == 0)
                        continue;

                    int destX = dx + x;
                    int o = (y * totalWidth + destX) * 4;
                    var (r, g, b) = Rgb555.ToRgb888(pixel);
                    rgba[o] = r;
                    rgba[o + 1] = g;
                    rgba[o + 2] = b;
                    rgba[o + 3] = 255;
                }
            }

            dx += glyph.Width;
        }

        return (rgba, totalWidth, maxHeight);
    }
}

/// <summary>Shared 16-bit (5-5-5) color unpacking used by several UO image formats.</summary>
internal static class Rgb555
{
    public static (byte R, byte G, byte B) ToRgb888(ushort color)
    {
        byte r = (byte)(((color >> 10) & 0x1F) * 255 / 31);
        byte g = (byte)(((color >> 5) & 0x1F) * 255 / 31);
        byte b = (byte)((color & 0x1F) * 255 / 31);
        return (r, g, b);
    }
}
