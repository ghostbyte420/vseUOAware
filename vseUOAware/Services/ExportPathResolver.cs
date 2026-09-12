namespace vseUOAware.Services;

/// <summary>
/// Resolves where MCP tool exports (rendered PNGs, exported WAVs, etc.) should
/// be written. Rather than always writing to a global scratch folder, exports
/// are placed in a "uoaExport" folder that lives beside the solution/workspace
/// root (next to the .slnx/.sln and .mcp.json files), separate from any
/// individual project's source folder, since exported UO data assets aren't
/// source code.
/// </summary>
internal static class ExportPathResolver
{
    private const string ExportFolderName = "uoaExport";

    /// <summary>
    /// Resolves a full output path for an export.
    /// - If <paramref name="outputPath"/> is a rooted (absolute) path, it is used as-is.
    /// - If it is a relative filename (or null/empty), it is placed inside the
    ///   "uoaExport" folder found near the MCP server project/solution, under
    ///   a per-asset-type <paramref name="category"/> subfolder (e.g. "Art",
    ///   "Gump", "Sound", "Animation") so exports stay organized by type.
    /// The containing directory is created if it doesn't already exist.
    /// </summary>
    public static string Resolve(string? outputPath, string defaultFileName, string? category = null)
    {
        if (!string.IsNullOrWhiteSpace(outputPath) && Path.IsPathRooted(outputPath))
        {
            EnsureDirectory(outputPath);
            return outputPath;
        }

        string fileName = string.IsNullOrWhiteSpace(outputPath) ? defaultFileName : outputPath;
        string exportDir = GetExportDirectory();
        if (!string.IsNullOrWhiteSpace(category))
            exportDir = Path.Combine(exportDir, category);
        Directory.CreateDirectory(exportDir);
        return Path.Combine(exportDir, fileName);
    }

    /// <summary>
    /// Finds (or creates) the "uoaExport" folder. Searches upward from the
    /// running server's base directory for the solution/workspace root
    /// (identified by a ".mcp.json" file, ".slnx", or ".sln" file) so exports
    /// land next to the solution rather than inside a specific project
    /// folder. If no such root is found, falls back to the nearest project
    /// folder (a ".mcp" folder or ".csproj" file), and finally to the base
    /// directory itself.
    /// </summary>
    private static string GetExportDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        string? projectFallback = null;

        for (int i = 0; i < 8 && dir is not null; i++)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0 ||
                Directory.GetFiles(dir, "*.sln").Length > 0 ||
                File.Exists(Path.Combine(dir, ".mcp.json")))
            {
                return Path.Combine(dir, ExportFolderName);
            }

            if (projectFallback is null &&
                (Directory.Exists(Path.Combine(dir, ".mcp")) ||
                 Directory.GetFiles(dir, "*.csproj").Length > 0))
            {
                projectFallback = Path.Combine(dir, ExportFolderName);
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return projectFallback ?? Path.Combine(AppContext.BaseDirectory, ExportFolderName);
    }

    private static void EnsureDirectory(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
