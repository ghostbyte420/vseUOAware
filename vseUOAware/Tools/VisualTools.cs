using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools for UO's visual/semantic reference data: hue color palettes and
/// the skill list.
/// </summary>
internal class VisualTools
{
    [McpServerTool]
    [Description("Gets a complete 32-color hue palette (as RGB values) plus its display name, from hues.mul.")]
    public string GetHue(
        [Description("Full path to hues.mul.")] string huesFilePath,
        [Description("1-based hue ID (0 means 'no hue' and is not stored in the file).")] int hueId)
    {
        if (!File.Exists(huesFilePath))
            return $"File not found: {huesFilePath}";

        try
        {
            var hue = HuesReader.GetHue(huesFilePath, hueId);
            var sb = new StringBuilder();
            sb.AppendLine($"Hue {hue.HueId}: \"{hue.Name}\" (tableStart={hue.TableStart}, tableEnd={hue.TableEnd})");
            for (int i = 0; i < hue.Colors.Count; i++)
            {
                var c = hue.Colors[i];
                sb.AppendLine($"  [{i}] #{c.R:X2}{c.G:X2}{c.B:X2}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to read hue {hueId}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches hue names in hues.mul and returns each match's ID and a representative RGB color.")]
    public string SearchHues(
        [Description("Full path to hues.mul.")] string huesFilePath,
        [Description("Text to search for in hue names.")] string searchText)
    {
        if (!File.Exists(huesFilePath))
            return $"File not found: {huesFilePath}";

        var matches = HuesReader.FindByName(huesFilePath, searchText);
        if (matches.Count == 0)
            return $"No hues found matching '{searchText}'.";

        var sb = new StringBuilder();
        foreach (var hue in matches)
        {
            var mid = hue.Colors[hue.Colors.Count / 2];
            sb.AppendLine($"Hue {hue.HueId}: \"{hue.Name}\" (mid color #{mid.R:X2}{mid.G:X2}{mid.B:X2})");
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Lists every client skill name and whether it has an action button, from Skills.idx + skills.mul.")]
    public string ListSkills(
        [Description("Full path to Skills.idx.")] string skillsIdxFilePath,
        [Description("Full path to skills.mul.")] string skillsMulFilePath)
    {
        if (!File.Exists(skillsIdxFilePath))
            return $"File not found: {skillsIdxFilePath}";
        if (!File.Exists(skillsMulFilePath))
            return $"File not found: {skillsMulFilePath}";

        var skills = SkillsReader.ReadAll(skillsIdxFilePath, skillsMulFilePath);
        var sb = new StringBuilder();
        sb.AppendLine($"Total skills: {skills.Count}");
        foreach (var skill in skills)
            sb.AppendLine($"#{skill.SkillId}: \"{skill.Name}\" (hasAction={skill.HasAction})");

        return sb.ToString();
    }
}
