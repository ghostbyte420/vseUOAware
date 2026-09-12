namespace vseUOAware.Services;

/// <summary>
/// One item's info from tiledata.mul: its ID number (called "TileID")
/// and its human-readable name (like "sandals").
/// </summary>
internal sealed record TileEntry(int TileId, string Name, bool IsStaticItem);

/// <summary>
/// Known tiledata.mul flag bits (classic UO client). Not every bit is used
/// by every client version, but this covers the well-documented ones.
/// </summary>
[Flags]
internal enum TileFlag : ulong
{
    None = 0,
    Background = 0x00000001,
    Weapon = 0x00000002,
    Transparent = 0x00000004,
    Translucent = 0x00000008,
    Wall = 0x00000010,
    Damaging = 0x00000020,
    Impassable = 0x00000040,
    Wet = 0x00000080,
    Unknown1 = 0x00000100,
    Surface = 0x00000200,
    Bridge = 0x00000400,
    Generic = 0x00000800,
    Window = 0x00001000,
    NoShoot = 0x00002000,
    ArticleA = 0x00004000,
    ArticleAn = 0x00008000,
    Internal = 0x00010000,
    Foliage = 0x00020000,
    PartialHue = 0x00040000,
    NoHouse = 0x00080000,
    Map = 0x00100000,
    Container = 0x00200000,
    Wearable = 0x00400000,
    LightSource = 0x00800000,
    Animation = 0x01000000,
    NoDiagonal = 0x02000000,
    Armor = 0x08000000,
    Roof = 0x10000000,
    Door = 0x20000000,
    StairBack = 0x40000000,
    StairRight = 0x80000000,
}

/// <summary>Full decoded record for a LAND tile (0x0000-0x3FFF).</summary>
internal sealed record LandTileDetail(int TileId, string Name, int TextureId, TileFlag Flags, IReadOnlyList<string> FlagNames);

/// <summary>
/// Full decoded record for an ITEM/static tile (0x4000+). The flags are
/// reliably decoded; the remaining "attribute" bytes (weight/quality/hue/
/// height/etc.) vary in exact byte order across tools and client versions,
/// so they're exposed as a raw hex blob rather than guessed field names.
/// </summary>
internal sealed record ItemTileDetail(int TileId, string Name, TileFlag Flags, IReadOnlyList<string> FlagNames, string RawAttributesHex);

/// <summary>
/// Reads tiledata.mul -- the file that maps item ID numbers to item NAMES.
/// This file changed shape twice over UO's history, so we AUTO-DETECT
/// which shape we're looking at by testing the file's total size.
/// (Old format = pre-2011ish clients. New/"High Seas" format = newer clients,
/// which is what version 7.0.9.0+ -- including your 7.0.50.0 -- use.)
/// </summary>
internal static class TileDataReader
{
    // There are ALWAYS exactly 512 groups of 32 "land" tiles (0x4000 land tiles total).
    // This never changed between old/new format, which is what makes auto-detection possible.
    private const int LandGroupCount = 512;
    private const int EntriesPerGroup = 32;
    private const int NameLength = 20; // Item names are always a fixed 20 bytes, padded with zeros.

    // Old format: 4-byte flags. New format: 8-byte flags (doubled to hold more flag bits).
    private const int OldLandEntrySize = 4 + 2 + NameLength;   // 26 bytes
    private const int NewLandEntrySize = 8 + 2 + NameLength;   // 30 bytes
    private const int OldStaticEntrySize = 4 + 1 + 1 + 2 + 1 + 2 + 1 + 1 + 2 + 2 + NameLength; // 37 bytes (classic layout)
    private const int NewStaticEntrySize = 8 + 1 + 1 + 2 + 1 + 2 + 1 + 1 + 2 + 2 + NameLength; // 41 bytes

    private const int GroupHeaderSize = 4; // Every group of 32 entries is preceded by a 4-byte "unused" header.

    /// <summary>
    /// Reads ALL land + static tile entries out of tiledata.mul.
    /// Automatically figures out old-vs-new format by checking the file size.
    /// </summary>
    public static List<TileEntry> ReadAll(string tileDataFilePath)
    {
        var fileInfo = new FileInfo(tileDataFilePath);
        bool useNewFormat = DetectNewFormat(fileInfo.Length);

        using var stream = File.OpenRead(tileDataFilePath);
        using var reader = new BinaryReader(stream);

        var results = new List<TileEntry>();

        // --- Part 1: read the 512 groups of LAND tiles ---
        int landEntrySize = useNewFormat ? NewLandEntrySize : OldLandEntrySize;
        int tileId = 0;
        for (int group = 0; group < LandGroupCount; group++)
        {
            stream.Seek(GroupHeaderSize, SeekOrigin.Current); // Skip the group's unused header.

            for (int i = 0; i < EntriesPerGroup; i++)
            {
                stream.Seek(useNewFormat ? 8 : 4, SeekOrigin.Current); // Skip flags.
                stream.Seek(2, SeekOrigin.Current); // Skip textureID.
                var name = ReadFixedName(reader);
                results.Add(new TileEntry(tileId, name, IsStaticItem: false));
                tileId++;
            }
        }

        // --- Part 2: read however many groups of STATIC (item) tiles remain ---
        int staticEntrySize = useNewFormat ? NewStaticEntrySize : OldStaticEntrySize;
        int staticGroupSize = GroupHeaderSize + EntriesPerGroup * staticEntrySize;
        long remainingBytes = stream.Length - stream.Position;
        int staticGroupCount = (int)(remainingBytes / staticGroupSize);

        int staticTileId = 0x4000; // Static (item) tile IDs conventionally start right after land tiles.
        for (int group = 0; group < staticGroupCount; group++)
        {
            stream.Seek(GroupHeaderSize, SeekOrigin.Current); // Skip the group's unused header.

            for (int i = 0; i < EntriesPerGroup; i++)
            {
                long entryStart = stream.Position;
                // Skip everything before the name field -- we only need flags-size + fixed
                // "middle" bytes (weight/quality/misc/etc.), whose exact meaning we don't need yet.
                int flagsSize = useNewFormat ? 8 : 4;
                int middleSize = staticEntrySize - flagsSize - NameLength;
                stream.Seek(flagsSize + middleSize, SeekOrigin.Current);

                var name = ReadFixedName(reader);
                results.Add(new TileEntry(staticTileId, name, IsStaticItem: true));

                staticTileId++;
                stream.Position = entryStart + staticEntrySize; // Make sure we land exactly on the next entry.
            }
        }

        return results;
    }

    /// <summary>Finds items whose name contains the given search text (case-insensitive).</summary>
    public static List<TileEntry> FindByName(string tileDataFilePath, string searchText)
    {
        var all = ReadAll(tileDataFilePath);
        return all.FindAll(e => e.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Reads the full decoded record (flags, texture ID, name) for one LAND tile.</summary>
    public static LandTileDetail GetLandDetail(string tileDataFilePath, int tileId)
    {
        if (tileId < 0 || tileId >= LandGroupCount * EntriesPerGroup)
            throw new ArgumentOutOfRangeException(nameof(tileId), $"Land tile ID must be between 0 and {LandGroupCount * EntriesPerGroup - 1}.");

        bool useNewFormat = DetectNewFormat(new FileInfo(tileDataFilePath).Length);
        int flagsSize = useNewFormat ? 8 : 4;
        int landEntrySize = useNewFormat ? NewLandEntrySize : OldLandEntrySize;

        int group = tileId / EntriesPerGroup;
        int indexInGroup = tileId % EntriesPerGroup;
        long entryOffset = (long)group * (GroupHeaderSize + EntriesPerGroup * landEntrySize)
            + GroupHeaderSize
            + (long)indexInGroup * landEntrySize;

        using var stream = File.OpenRead(tileDataFilePath);
        using var reader = new BinaryReader(stream);
        stream.Seek(entryOffset, SeekOrigin.Begin);

        ulong flagsValue = useNewFormat ? reader.ReadUInt64() : reader.ReadUInt32();
        int textureId = reader.ReadInt16();
        var name = ReadFixedName(reader);

        var flags = (TileFlag)flagsValue;
        return new LandTileDetail(tileId, name, textureId, flags, DescribeFlags(flags));
    }

    /// <summary>Reads the full decoded record (flags, name, raw attribute bytes) for one ITEM/static tile.</summary>
    public static ItemTileDetail GetItemDetail(string tileDataFilePath, int tileId)
    {
        if (tileId < 0x4000)
            throw new ArgumentOutOfRangeException(nameof(tileId), "Item/static tile IDs start at 0x4000 (16384).");

        var fileInfo = new FileInfo(tileDataFilePath);
        bool useNewFormat = DetectNewFormat(fileInfo.Length);
        int flagsSize = useNewFormat ? 8 : 4;
        int staticEntrySize = useNewFormat ? NewStaticEntrySize : OldStaticEntrySize;
        int middleSize = staticEntrySize - flagsSize - NameLength;

        long landSectionSize = LandGroupCount * (long)(GroupHeaderSize + EntriesPerGroup * (useNewFormat ? NewLandEntrySize : OldLandEntrySize));

        int staticIndex = tileId - 0x4000;
        int group = staticIndex / EntriesPerGroup;
        int indexInGroup = staticIndex % EntriesPerGroup;
        int staticGroupSize = GroupHeaderSize + EntriesPerGroup * staticEntrySize;

        long entryOffset = landSectionSize
            + (long)group * staticGroupSize
            + GroupHeaderSize
            + (long)indexInGroup * staticEntrySize;

        if (entryOffset + staticEntrySize > fileInfo.Length)
            throw new ArgumentOutOfRangeException(nameof(tileId), $"Item tile ID 0x{tileId:X4} is beyond the end of tiledata.mul.");

        using var stream = File.OpenRead(tileDataFilePath);
        using var reader = new BinaryReader(stream);
        stream.Seek(entryOffset, SeekOrigin.Begin);

        ulong flagsValue = useNewFormat ? reader.ReadUInt64() : reader.ReadUInt32();
        var middleBytes = reader.ReadBytes(middleSize);
        var name = ReadFixedName(reader);

        var flags = (TileFlag)flagsValue;
        return new ItemTileDetail(tileId, name, flags, DescribeFlags(flags), Convert.ToHexString(middleBytes));
    }

    private static IReadOnlyList<string> DescribeFlags(TileFlag flags)
    {
        if (flags == TileFlag.None)
            return [];

        return Enum.GetValues<TileFlag>()
            .Where(f => f != TileFlag.None && flags.HasFlag(f))
            .Select(f => f.ToString())
            .ToArray();
    }

    /// <summary>
    /// Guesses old-vs-new format by computing the expected file size both ways
    /// and seeing which one evenly divides the remaining (static) section.
    /// </summary>
    private static bool DetectNewFormat(long fileLength)
    {
        long oldLandSectionSize = LandGroupCount * (long)(GroupHeaderSize + EntriesPerGroup * OldLandEntrySize);
        long newLandSectionSize = LandGroupCount * (long)(GroupHeaderSize + EntriesPerGroup * NewLandEntrySize);

        long oldStaticGroupSize = GroupHeaderSize + EntriesPerGroup * OldStaticEntrySize;
        long newStaticGroupSize = GroupHeaderSize + EntriesPerGroup * NewStaticEntrySize;

        long oldRemaining = fileLength - oldLandSectionSize;
        long newRemaining = fileLength - newLandSectionSize;

        bool oldFits = oldRemaining > 0 && oldRemaining % oldStaticGroupSize == 0;
        bool newFits = newRemaining > 0 && newRemaining % newStaticGroupSize == 0;

        // If only one shape fits cleanly, trust it. If both (rare) or neither fit,
        // default to the NEW format since that matches modern clients like uoAvox 7.0.50.0.
        if (newFits && !oldFits) return true;
        if (oldFits && !newFits) return false;
        return true;
    }

    private static string ReadFixedName(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(NameLength);
        // Names are padded with zero bytes -- trim everything after the first zero.
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        int length = nullIndex >= 0 ? nullIndex : bytes.Length;
        return System.Text.Encoding.ASCII.GetString(bytes, 0, length);
    }
}