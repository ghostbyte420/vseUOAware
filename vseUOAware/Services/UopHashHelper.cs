namespace vseUOAware.Services;

/// <summary>
/// Implements Bob Jenkins' "hashlittle2" (lookup3.c) hash, which the UO
/// client uses to compute the 64-bit hash of each virtual filename stored
/// inside a UOP archive (e.g. "build/artlegacymul/00001234.tga"). This lets
/// us look up a UOP entry by numeric tile/gump ID instead of guessing.
/// </summary>
internal static class UopHashHelper
{
    /// <summary>Computes the UOP-style 64-bit hash for a virtual file path.</summary>
    public static ulong HashFileName(string fileName)
    {
        var data = System.Text.Encoding.ASCII.GetBytes(fileName);
        return HashLittle2(data);
    }

    /// <summary>Builds the virtual filename used for an art tile entry (land or item), e.g. "build/artlegacymul/00001234.tga".</summary>
    public static string ArtFileName(int tileId) => $"build/artlegacymul/{tileId:D8}.tga";

    /// <summary>Builds the virtual filename used for a gump entry, e.g. "build/gumpartlegacymul/00001234.tga".</summary>
    public static string GumpFileName(int gumpId) => $"build/gumpartlegacymul/{gumpId:D8}.tga";

    /// <summary>Builds the virtual filename used for a sound entry, e.g. "build/soundlegacymul/00001234.dat".</summary>
    public static string SoundFileName(int soundId) => $"build/soundlegacymul/{soundId:D8}.dat";

    private static uint Rot(uint x, int k) => (x << k) | (x >> (32 - k));

    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= c; a ^= Rot(c, 4); c += b;
        b -= a; b ^= Rot(a, 6); a += c;
        c -= b; c ^= Rot(b, 8); b += a;
        a -= c; a ^= Rot(c, 16); c += b;
        b -= a; b ^= Rot(a, 19); a += c;
        c -= b; c ^= Rot(b, 4); b += a;
    }

    private static void Final(ref uint a, ref uint b, ref uint c)
    {
        c ^= b; c -= Rot(b, 14);
        a ^= c; a -= Rot(c, 11);
        b ^= a; b -= Rot(a, 25);
        c ^= b; c -= Rot(b, 16);
        a ^= c; a -= Rot(c, 4);
        b ^= a; b -= Rot(a, 14);
        c ^= b; c -= Rot(b, 24);
    }

    /// <summary>
    /// Canonical Bob Jenkins "hashlittle2" (lookup3.c) implementation used by
    /// the UO client to hash UOP virtual filenames, returning (b &lt;&lt; 32) | c
    /// as the 64-bit hash (verified against a known-good reference implementation).
    /// </summary>
    private static ulong HashLittle2(byte[] key)
    {
        int length = key.Length;
        uint a, b, c;
        a = b = c = 0xDEADBEEF + (uint)length;

        int offset = 0;
        int remaining = length;

        while (remaining > 12)
        {
            a += ReadUInt32(key, offset);
            b += ReadUInt32(key, offset + 4);
            c += ReadUInt32(key, offset + 8);
            Mix(ref a, ref b, ref c);
            remaining -= 12;
            offset += 12;
        }

        switch (remaining)
        {
            case 12: c += (uint)key[offset + 11] << 24; goto case 11;
            case 11: c += (uint)key[offset + 10] << 16; goto case 10;
            case 10: c += (uint)key[offset + 9] << 8; goto case 9;
            case 9: c += key[offset + 8]; goto case 8;
            case 8: b += (uint)key[offset + 7] << 24; goto case 7;
            case 7: b += (uint)key[offset + 6] << 16; goto case 6;
            case 6: b += (uint)key[offset + 5] << 8; goto case 5;
            case 5: b += key[offset + 4]; goto case 4;
            case 4: a += (uint)key[offset + 3] << 24; goto case 3;
            case 3: a += (uint)key[offset + 2] << 16; goto case 2;
            case 2: a += (uint)key[offset + 1] << 8; goto case 1;
            case 1: a += key[offset]; break;
            case 0: return (ulong)c << 32; // empty input: no mixing, low word is 0
        }

        Final(ref a, ref b, ref c);
        return ((ulong)b << 32) | c;
    }

    private static uint ReadUInt32(byte[] data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
