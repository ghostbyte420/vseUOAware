namespace vseUOAware.Services;

/// <summary>
/// Decodes UO body animations from the classic Anim.mul/Anim2.mul-Anim6.mul
/// .idx/.mul pairs (pre-UOP animation storage). This covers the vast
/// majority of bodies (humans, most monsters/animals) that predate the
/// AnimationFrame*.uop format. Mirrors the reference client's per-file
/// index-math (body/action/direction -> record index) and per-frame decode
/// (256-color palette + pointer-relative RLE), reusing the same frame
/// decoder as the UOP path.
/// </summary>
internal static class ClassicAnimationReader
{
    private const int PaletteCapacity = 0x100;

    /// <summary>
    /// Decodes every stored frame for one body/action/direction from the
    /// classic Anim*.mul files. Automatically resolves which Anim*.idx/.mul
    /// pair to use via bodyconv.def (falling back to the base Anim.mul).
    /// Returns null if the body/action/direction record doesn't exist.
    /// </summary>
    public static List<AnimationFrameImage?>? GetFrames(
        string uoDirectoryPath,
        string bodyConvDefPath,
        int body,
        int action,
        int direction)
    {
        if (direction < 0 || direction > 7)
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be 0-7.");

        var (fileType, resolvedBody) = BodyConverterReader.Resolve(bodyConvDefPath, body);

        string idxFileName = fileType == 1 ? "anim.idx" : $"anim{fileType}.idx";
        string mulFileName = fileType == 1 ? "anim.mul" : $"anim{fileType}.mul";
        string idxPath = ResolveCaseInsensitive(uoDirectoryPath, idxFileName);
        string mulPath = ResolveCaseInsensitive(uoDirectoryPath, mulFileName);

        if (idxPath is null || mulPath is null)
            throw new FileNotFoundException($"Could not find {idxFileName}/{mulFileName} under {uoDirectoryPath}.");

        int recordIndex = ComputeRecordIndex(fileType, resolvedBody, action, direction);

        var entry = MulIdxReader.ReadEntryAt(idxPath, recordIndex);
        if (entry is null)
            return null;

        var data = MulIdxReader.ReadEntryData(mulPath, entry);

        bool flip = direction > 4;
        return ParseFrames(data, flip);
    }

    /// <summary>
    /// Reproduces the reference client's GetFileIndex body/action/direction
    /// -> record-index math for classic Anim*.mul files. Each record covers
    /// one direction of one action; every action reserves 5 stored
    /// directions (0-4), with 5-7 mirrored client-side from 3-1.
    /// </summary>
    private static int ComputeRecordIndex(int fileType, int body, int action, int direction)
    {
        int index;

        switch (fileType)
        {
            case 1:
            case 2:
                if (body < 200)
                    index = body * 110;
                else if (body < 400)
                    index = 22000 + (body - 200) * 65;
                else
                    index = 35000 + (body - 400) * 175;
                break;

            case 3:
                if (body < 300)
                    index = body * 65;
                else if (body < 400)
                    index = 33000 + (body - 300) * 110;
                else
                    index = 35000 + (body - 400) * 175;
                break;

            case 4:
            case 6:
                if (body < 200)
                    index = body * 110;
                else if (body < 400)
                    index = 22000 + (body - 200) * 65;
                else
                    index = 35000 + (body - 400) * 175;
                break;

            case 5:
                if (body < 200 && body != 34) // matches the reference client's documented oddity
                    index = body * 110;
                else if (body < 400)
                    index = 22000 + (body - 200) * 65;
                else
                    index = 35000 + (body - 400) * 175;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(fileType), $"Unknown classic animation file type {fileType}.");
        }

        index += action * 5;

        index += direction <= 4 ? direction : direction - (direction - 4) * 2;

        return index;
    }

    private static List<AnimationFrameImage?> ParseFrames(byte[] data, bool flip)
    {
        if (data.Length < PaletteCapacity * 2 + 4)
            throw new InvalidDataException("Classic animation record too short to contain a palette + frame table.");

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var palette = new ushort[PaletteCapacity];
        for (int i = 0; i < PaletteCapacity; i++)
        {
            ushort raw = reader.ReadUInt16();
            palette[i] = raw == 0 ? (ushort)0 : (ushort)(raw ^ 0x8000);
        }

        int start = (int)stream.Position;
        int frameCount = reader.ReadInt32();

        if (frameCount <= 0 || frameCount > 512)
            throw new InvalidDataException($"Implausible classic animation frame count ({frameCount}); refusing to decode.");

        var lookups = new int[frameCount];
        for (int i = 0; i < frameCount; i++)
            lookups[i] = start + reader.ReadInt32();

        var result = new List<AnimationFrameImage?>(frameCount);
        for (int i = 0; i < frameCount; i++)
        {
            if (lookups[i] < 0 || lookups[i] >= data.Length)
            {
                result.Add(null);
                continue;
            }

            stream.Seek(lookups[i], SeekOrigin.Begin);
            result.Add(AnimationReader.DecodeFrame(reader, palette, flip));
        }

        return result;
    }

    private static string? ResolveCaseInsensitive(string directoryPath, string fileName)
    {
        string direct = Path.Combine(directoryPath, fileName);
        if (File.Exists(direct))
            return direct;

        return Directory.EnumerateFiles(directoryPath)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    }
}
