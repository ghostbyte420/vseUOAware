namespace vseUOAware.Services;

/// <summary>One decoded UO sound: a name plus synthesized RIFF/WAV bytes.</summary>
internal sealed record SoundEntry(int Id, string Name, byte[] WavData);

/// <summary>
/// Reads UO sounds from soundLegacyMUL.uop (this client has no classic
/// sound.idx/sound.mul pair). Each UOP entry starts with a 0x28-byte
/// null-padded ASCII name header, followed by raw 16-bit mono 44100Hz PCM
/// samples with no RIFF header of its own -- the WAV header must be
/// synthesized, matching the reference client's UOSound_loadEntry behavior.
/// </summary>
internal static class SoundReader
{
    private const int NameHeaderLength = 0x28;

    public static SoundEntry? GetSound(string soundUopFilePath, int soundId)
    {
        var entries = uopFileReader.ReadEntries(soundUopFilePath);
        ulong hash = UopHashHelper.HashFileName(UopHashHelper.SoundFileName(soundId));
        var entry = uopFileReader.FindEntryByHash(entries, hash);
        if (entry is null)
            return null;

        var data = uopFileReader.ReadEntryData(soundUopFilePath, entry);
        if (data.Length < NameHeaderLength)
            return null;

        string name = ReadNullTerminatedName(data, 0, NameHeaderLength);
        int pcmLength = data.Length - NameHeaderLength;
        var wav = BuildWav(data, NameHeaderLength, pcmLength);
        return new SoundEntry(soundId, name, wav);
    }

    /// <summary>Searches every entry's name header (without decoding full PCM payloads) for the given text.</summary>
    public static List<(int Id, string Name)> SearchNames(string soundUopFilePath, string searchText)
    {
        var entries = uopFileReader.ReadEntries(soundUopFilePath);
        var results = new List<(int, string)>();

        var hashSet = new HashSet<ulong>(entries.Select(e => e.Hash));

        for (int id = 0; id <= 0xFFF; id++)
        {
            ulong hash = UopHashHelper.HashFileName(UopHashHelper.SoundFileName(id));
            if (!hashSet.Contains(hash))
                continue;

            var entry = entries.First(e => e.Hash == hash);
            var data = uopFileReader.ReadEntryData(soundUopFilePath, entry);
            if (data.Length < NameHeaderLength)
                continue;

            string name = ReadNullTerminatedName(data, 0, NameHeaderLength);
            if (name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                results.Add((id, name));
        }

        return results;
    }

    private static string ReadNullTerminatedName(byte[] data, int offset, int maxLength)
    {
        string raw = System.Text.Encoding.ASCII.GetString(data, offset, maxLength);
        int nullIndex = raw.IndexOf('\0');
        return nullIndex >= 0 ? raw[..nullIndex] : raw;
    }

    private static byte[] BuildWav(byte[] source, int pcmOffset, int pcmLength)
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bitsPerSample = 16;
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        var wav = new byte[44 + pcmLength];
        using (var stream = new MemoryStream(wav))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmLength);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmLength);
        }

        Array.Copy(source, pcmOffset, wav, 44, pcmLength);
        return wav;
    }
}
