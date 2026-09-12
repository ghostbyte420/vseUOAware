using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>
/// Tools that expose UO sound assets (soundLegacyMUL.uop) by exporting
/// them to local WAV files, since MCP tool results are text-only.
/// </summary>
internal class SoundTools
{
    [McpServerTool]
    [Description("Exports a UO sound by ID to a local WAV file and returns its name, byte length, and saved path. If outputWavPath is omitted or a bare filename, the file is written to a 'uoaExport' folder beside the MCP server project.")]
    public string ExportSound(
        [Description("Full path to soundLegacyMUL.uop.")] string soundUopFilePath,
        [Description("Sound ID to export.")] int soundId,
        [Description("Full path, or bare filename, for the output WAV. Defaults to 'uoaExport/sound<soundId>.wav' if omitted.")] string? outputWavPath = null)
    {
        if (!File.Exists(soundUopFilePath))
            return $"File not found: {soundUopFilePath}";

        string resolvedPath = ExportPathResolver.Resolve(outputWavPath, $"sound{soundId}.wav", "Sound");

        try
        {
            var sound = SoundReader.GetSound(soundUopFilePath, soundId);
            if (sound is null)
                return $"No sound entry found for sound ID {soundId}.";

            File.WriteAllBytes(resolvedPath, sound.WavData);
            return $"Exported sound {soundId} (\"{sound.Name}\", {sound.WavData.Length} bytes) to: {resolvedPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to export sound {soundId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches sound names within soundLegacyMUL.uop and returns matching IDs and names (without exporting audio).")]
    public string SearchSounds(
        [Description("Full path to soundLegacyMUL.uop.")] string soundUopFilePath,
        [Description("Text to search for within sound names.")] string searchText)
    {
        if (!File.Exists(soundUopFilePath))
            return $"File not found: {soundUopFilePath}";

        var matches = SoundReader.SearchNames(soundUopFilePath, searchText);
        if (matches.Count == 0)
            return $"No sounds found matching \"{searchText}\".";

        return string.Join(Environment.NewLine, matches.Select(m => $"{m.Id}: {m.Name}"));
    }
}
