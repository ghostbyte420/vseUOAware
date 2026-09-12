using System.Buffers.Binary;

namespace vseUOAware.Services;

/// <summary>One speech keyword entry from speech.mul.</summary>
internal sealed record SpeechEntry(int Id, string Keyword);

/// <summary>
/// Reads speech.mul: the classic client's speech-command keyword table.
/// Unlike most UO files, its records are big-endian: repeated
/// [ushort id][ushort length][ASCII text], both 16-bit fields big-endian.
/// </summary>
internal static class SpeechReader
{
    public static List<SpeechEntry> ReadAll(string speechMulFilePath)
    {
        using var stream = File.OpenRead(speechMulFilePath);
        using var reader = new BinaryReader(stream);

        var results = new List<SpeechEntry>();

        while (stream.Position + 4 <= stream.Length)
        {
            int id = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            int length = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));

            if (stream.Position + length > stream.Length)
                break;

            var bytes = reader.ReadBytes(length);
            string keyword = System.Text.Encoding.ASCII.GetString(bytes);

            results.Add(new SpeechEntry(id, keyword));
        }

        return results;
    }

    /// <summary>Searches for speech keywords containing the given search text (case-insensitive).</summary>
    public static List<SpeechEntry> Search(string speechMulFilePath, string searchText)
    {
        var all = ReadAll(speechMulFilePath);
        return all.FindAll(e => e.Keyword.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
}
