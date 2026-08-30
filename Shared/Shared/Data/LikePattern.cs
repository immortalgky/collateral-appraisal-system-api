namespace Shared.Data;

/// <summary>
/// Builds SQL Server <c>LIKE</c> patterns from user-typed search text.
///
/// Lives in Shared because both the task list and global search need the identical convention:
/// a term the user types is a <b>prefix</b> search by default, which an index can seek, and
/// substring matching is opt-in via <c>*</c>. Two implementations would drift, and the drift would
/// be invisible — the wrong one still returns rows, just fewer or slower.
/// </summary>
public static class LikePattern
{
    /// <summary>
    /// Escapes SQL Server LIKE wildcards (<c>%</c>, <c>_</c>, <c>[</c>) so user-supplied search
    /// text is matched literally. Pair with an <c>ESCAPE '\'</c> clause. The escape character
    /// <c>\</c> itself is escaped first to avoid double-escaping.
    /// </summary>
    public static string Escape(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

    /// <summary>
    /// Builds a LIKE pattern with glob semantics: <c>*</c> is the user wildcard (translated to
    /// <c>%</c>); all real LIKE metacharacters (<c>% _ [ \</c>) are escaped to literal via
    /// <see cref="Escape"/>. When the term contains no <c>*</c>, a trailing <c>%</c> is appended so
    /// the default is a seekable <b>prefix</b> search (<c>term%</c>) — fast and flat under load.
    /// Users opt into substring/suffix matching with <c>*</c> (e.g. <c>*somchai*</c>), which
    /// produces a leading wildcard and falls back to a scan. Pair with <c>ESCAPE '\'</c>.
    /// </summary>
    public static string Build(string value)
    {
        var escaped = Escape(value);              // % _ [ \ -> literal; leaves * untouched
        var hasGlob = escaped.Contains('*');
        var pattern = escaped.Replace('*', '%');
        return hasGlob ? pattern : pattern + "%"; // no * => prefix search
    }

    /// <summary>
    /// True when the pattern starts with a wildcard, i.e. no index can seek it. Callers use this to
    /// decide whether a query is cheap enough to run across every column or should be narrowed.
    /// </summary>
    public static bool IsLeadingWildcard(string pattern) => pattern.StartsWith('%');
}
