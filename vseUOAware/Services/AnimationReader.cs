namespace vseUOAware.Services;

/// <summary>One decoded animation frame's RGBA pixel buffer plus its draw-anchor center point.</summary>
internal sealed record AnimationFrameImage(int Width, int Height, byte[] Rgba, int CenterX, int CenterY);

/// <summary>
/// Decodes UO body animations from the "new" AnimationFrame*.uop archives
/// (post-HS/UOP-era clients). This mirrors the reference client's decode
/// pipeline closely (per-body/action .bin entry -> per-direction frame
/// headers -> per-frame 256-color palette + pointer-relative RLE pixel
/// runs), but resolves all pointer offsets with bounds-checked array math
/// instead of unsafe pointers, since this is inherently the riskiest/most
/// complex format we support.
/// </summary>
internal static class AnimationReader
{
    private const int MaxAnimActions = 80;
    private const int MaxDirectionsStored = 5; // directions 0-4; 5-7 are mirrored copies of 1-3
    private const int PaletteCapacity = 0x100;
    private const uint DoubleXor = unchecked((uint)((0x200 << 22) | (0x200 << 12)));
    private const uint FrameTerminator = 0x7FFF7FFF;

    /// <summary>
    /// Decodes every stored frame for one body/action/direction from whichever
    /// AnimationFrame*.uop file contains it. Returns null if the body/action
    /// combination isn't found in any of the given archives.
    /// </summary>
    public static List<AnimationFrameImage?>? GetFrames(
        IReadOnlyList<string> animationFrameUopFilePaths,
        int body,
        int action,
        int direction,
        int hueId = 0)
    {
        if (action < 0 || action >= MaxAnimActions)
            throw new ArgumentOutOfRangeException(nameof(action), $"Action must be 0-{MaxAnimActions - 1}.");
        if (direction < 0 || direction > 7)
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be 0-7.");

        string virtualPath = $"build/animationlegacyframe/{body:D6}/{action:D2}.bin";
        ulong hash = UopHashHelper.HashFileName(virtualPath);

        byte[]? data = null;
        foreach (var path in animationFrameUopFilePaths)
        {
            if (!File.Exists(path))
                continue;

            var entries = uopFileReader.ReadEntries(path);
            var entry = uopFileReader.FindEntryByHash(entries, hash);
            if (entry is null)
                continue;

            data = uopFileReader.ReadEntryData(path, entry);
            break;
        }

        if (data is null)
            return null;

        bool flip = direction > 4;
        int readDirection = direction <= 4 ? direction : direction - ((direction - 4) * 2);

        ushort[]? palette = hueId > 0 ? BuildHuePalette(hueId) : null;

        return ParseFrames(data, readDirection, flip, palette);
    }

    private static ushort[] BuildHuePalette(int hueId)
    {
        // Approximation: without the classic client's hue application curve
        // (which grays/tints existing palette entries), we can't perfectly
        // replicate in-game hue-shifting. Callers wanting "as authored"
        // colors should pass hueId 0 and rely on the frame's own 256-color
        // palette baked into each frame's pixel data instead.
        throw new NotSupportedException("Hue tinting is not implemented for animation frames; pass hueId 0 to use the frame's native palette.");
    }

    private static List<AnimationFrameImage?> ParseFrames(byte[] data, int direction, bool flip, ushort[]? hueOverridePalette)
    {
        if (data.Length < 36)
            throw new InvalidDataException("Animation frame data too short to contain a header.");

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        stream.Seek(32, SeekOrigin.Begin);
        int frameCount = reader.ReadInt32();
        uint dataStart = reader.ReadUInt32();

        if (frameCount <= 0 || dataStart >= data.Length)
            throw new InvalidDataException("Animation frame header reports an implausible frame table.");

        stream.Seek(dataStart, SeekOrigin.Begin);

        var headers = new List<(long Pos, ushort FrameId, uint PixelOffset)>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            long pos = stream.Position;
            reader.ReadUInt16(); // group - unused
            ushort frameId = reader.ReadUInt16();
            reader.ReadInt64(); // unknown
            uint pixelOffset = reader.ReadUInt32();
            headers.Add((pos, frameId, pixelOffset));
        }

        // Gap-fill missing frame IDs with placeholder (empty) entries so the
        // direction math below (realFrameCount, frameDir) lines up correctly.
        var filled = new List<(long Pos, ushort FrameId, uint PixelOffset)>(headers.Count);
        int lastId = 1;
        foreach (var (pos, frameId, pixelOffset) in headers)
        {
            while (frameId - lastId > 1)
            {
                lastId++;
                filled.Add((0L, (ushort)lastId, 0u));
            }

            filled.Add((pos, frameId, pixelOffset));
            lastId = frameId;
        }

        int realFrameCount = (int)Math.Round(filled.Count / (float)MaxDirectionsStored);
        if (realFrameCount <= 0)
            throw new InvalidDataException("Animation frame table did not resolve to any usable frames.");

        var result = new List<AnimationFrameImage?>();

        foreach (var (pos, frameId, pixelOffset) in filled)
        {
            int frameDir = (frameId - 1) / realFrameCount;
            if (frameDir < direction)
                continue;
            if (frameDir > direction)
                break;

            if (pos == 0)
            {
                result.Add(null);
                continue;
            }

            stream.Seek(pos + pixelOffset, SeekOrigin.Begin);

            var palette = new ushort[PaletteCapacity];
            for (int i = 0; i < PaletteCapacity; i++)
            {
                ushort raw = reader.ReadUInt16();
                palette[i] = raw == 0 ? (ushort)0 : (ushort)(raw | 0x8000);
            }

            result.Add(DecodeFrame(reader, hueOverridePalette ?? palette, flip));
        }

        return result;
    }

    /// <summary>
    /// Decodes one frame's pixel data (center point + width/height header,
    /// then pointer-relative RLE runs terminated by 0x7FFF7FFF) using an
    /// already-resolved 256-color palette. Shared by both the UOP-based
    /// reader and the classic Anim*.mul reader, since both formats use this
    /// same per-frame body.
    /// </summary>
    internal static AnimationFrameImage? DecodeFrame(BinaryReader reader, ushort[] palette, bool flip)
    {
        int xCenter = reader.ReadInt16();
        int yCenter = reader.ReadInt16();
        int width = reader.ReadUInt16();
        int height = reader.ReadUInt16();

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            return null;

        // GDI+ 16bpp bitmaps pad each scanline to a 4-byte boundary; replicate
        // that "delta" (pixels per stride) so pointer-relative math below
        // lands on the correct row, matching the reference client exactly.
        int delta = (width % 2 == 0) ? width : width + 1;

        var rgba = new byte[width * height * 4];

        int xBase = xCenter - 0x200;
        int yBase = (yCenter + height) - 0x200;

        // "basePixelIndex" stands in for the reference client's unsafe base
        // pointer (which may point outside the bitmap's own bounds); every
        // run's absolute pixel index is computed relative to it and then
        // bounds-checked before writing, instead of dereferencing directly.
        long basePixelIndex = flip
            ? (width - 1 - xBase) + (long)yBase * delta
            : xBase + (long)yBase * delta;

        int finalXCenter = xCenter;

        while (true)
        {
            uint header = reader.ReadUInt32();
            if (header == FrameTerminator)
                break;

            header ^= DoubleXor;

            int rowOffset = (int)((header >> 12) & 0x3FF);
            int colOffset = (int)((header >> 22) & 0x3FF);
            int runLength = (int)(header & 0xFFF);

            if (!flip)
            {
                long start = basePixelIndex + (long)rowOffset * delta + colOffset;
                for (int k = 0; k < runLength; k++)
                {
                    byte paletteIndex = reader.ReadByte();
                    WriteRunPixel(rgba, width, height, delta, start + k, palette[paletteIndex]);
                }
            }
            else
            {
                long start = basePixelIndex + (long)rowOffset * delta - colOffset;
                for (int k = 0; k < runLength; k++)
                {
                    byte paletteIndex = reader.ReadByte();
                    WriteRunPixel(rgba, width, height, delta, start - k, palette[paletteIndex]);
                }
            }
        }

        if (flip)
            finalXCenter = width - xCenter;

        return new AnimationFrameImage(width, height, rgba, finalXCenter, yCenter);
    }

    private static void WriteRunPixel(byte[] rgba, int width, int height, int delta, long index, ushort color)
    {
        if (color == 0)
            return; // transparent

        long y = (long)Math.Floor((double)index / delta);
        long x = index - y * delta;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return; // out of bounds for this frame's actual canvas; skip defensively

        ushort visible = (ushort)(color ^ 0x8000);
        int idx = ((int)y * width + (int)x) * 4;

        rgba[idx + 0] = (byte)(((visible >> 10) & 0x1F) * 255 / 31);
        rgba[idx + 1] = (byte)(((visible >> 5) & 0x1F) * 255 / 31);
        rgba[idx + 2] = (byte)((visible & 0x1F) * 255 / 31);
        rgba[idx + 3] = 255;
    }
}
