namespace vseUOAware.Services;

/// <summary>
/// Reads bodyconv.def: the file that says which server-side body IDs are
/// actually stored in Anim2.mul-Anim6.mul instead of the base Anim.mul,
/// and at what in-file index. Format per non-comment line:
/// "&lt;original&gt; &lt;anim2&gt; &lt;anim3&gt; &lt;anim4&gt; &lt;anim5&gt; [anim6]"
/// where any column can be -1 (not present in that file).
/// </summary>
internal static class BodyConverterReader
{
    /// <summary>
    /// Resolves which classic anim file (1-6) and in-file body index to use
    /// for a given server body ID, by scanning bodyconv.def. Returns
    /// (fileType: 1, body) unchanged if the body has no bodyconv.def entry
    /// (i.e. it lives directly in Anim.mul at its own body ID).
    /// </summary>
    public static (int FileType, int ResolvedBody) Resolve(string bodyConvDefPath, int body)
    {
        if (!File.Exists(bodyConvDefPath))
            return (1, body);

        foreach (var rawLine in File.ReadLines(bodyConvDefPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int original) || original != body)
                continue;

            // Columns: original, anim2, anim3, anim4, anim5, [anim6]. First
            // column (after original) with a value other than -1 wins, in
            // file-type order 2..6 - this matches the reference client's
            // per-table population order (Table2 checked before Table3, etc).
            for (int col = 1; col < parts.Length && col <= 5; col++)
            {
                if (int.TryParse(parts[col], out int inFileBody) && inFileBody != -1)
                    return (col + 1, inFileBody);
            }

            // Matched the body line but every column was -1: falls back to Anim.mul.
            return (1, body);
        }

        return (1, body);
    }

    /// <summary>Returns every server body ID that has a bodyconv.def entry (i.e. isn't in the base Anim.mul).</summary>
    public static IReadOnlyCollection<int> GetAllConvertedBodies(string bodyConvDefPath)
    {
        var results = new HashSet<int>();
        if (!File.Exists(bodyConvDefPath))
            return results;

        foreach (var rawLine in File.ReadLines(bodyConvDefPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int original))
                results.Add(original);
        }

        return results;
    }
}
