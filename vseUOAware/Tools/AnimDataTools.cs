using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>Tools for animdata.mul (animated static item timing/frame-sequence metadata).</summary>
internal class AnimDataTools
{
    [McpServerTool]
    [Description("Gets animdata.mul timing and frame-sequence metadata for an animated static item tile (e.g. torches, water), such as frame count/interval/start and the relative art-tile frame offsets.")]
    public string GetItemAnimationData(
        [Description("Full path to animdata.mul.")] string animDataMulFilePath,
        [Description("Item tile ID to look up (same ID space as static art tiles).")] int itemId)
    {
        if (!File.Exists(animDataMulFilePath))
            return $"File not found: {animDataMulFilePath}";

        try
        {
            var entry = AnimDataReader.GetAnimData(animDataMulFilePath, itemId);
            if (entry is null)
                return $"No animdata.mul entry found for item {itemId}.";

            var sb = new StringBuilder();
            sb.AppendLine($"Item {itemId}: frameCount={entry.FrameCount}, frameInterval={entry.FrameInterval}, frameStart={entry.FrameStart}, unknown={entry.Unknown}");
            sb.AppendLine($"Frame offsets (relative art tile deltas, first {Math.Min(entry.FrameCount, entry.FrameData.Length)}): {string.Join(", ", entry.FrameData.Take(entry.FrameCount))}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read animdata for item {itemId}: {ex.Message}";
        }
    }
}
