using Dapper;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>
/// Small parameterised SQL helpers shared by every report's filter builder: date ranges,
/// comma-separated multi-value IN clauses, exact match, and an allow-listed ORDER BY.
/// All values bind through <see cref="DynamicParameters"/> — never string-concatenated.
/// </summary>
internal static class ReportFilterSql
{
    public static void DateRange(
        List<string> conditions, DynamicParameters p,
        DateTime? from, DateTime? to, string column, string name)
    {
        if (from.HasValue)
        {
            conditions.Add($"{column} >= @{name}From");
            p.Add($"{name}From", from.Value);
        }
        if (to.HasValue)
        {
            conditions.Add($"{column} < DATEADD(day, 1, @{name}To)");
            p.Add($"{name}To", to.Value);
        }
    }

    /// <summary>Comma-separated value → <c>= @x</c> (single) or <c>IN @x</c> (many). No-op when blank.</summary>
    public static void MultiValue(
        List<string> conditions, DynamicParameters p, string? csv, string column, string name)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;

        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0) return;

        if (values.Length == 1)
        {
            conditions.Add($"{column} = @{name}");
            p.Add(name, values[0]);
        }
        else
        {
            conditions.Add($"{column} IN @{name}");
            p.Add(name, values);
        }
    }

    public static void Exact(
        List<string> conditions, DynamicParameters p, string? value, string column, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        conditions.Add($"{column} = @{name}");
        p.Add(name, value);
    }

    public static void Contains(
        List<string> conditions, DynamicParameters p, string? value, string column, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        conditions.Add($"{column} LIKE '%' + @{name} + '%'");
        p.Add(name, value.Trim());
    }

    public static string Where(List<string> conditions) =>
        conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";

    public static string OrderBy(
        string? sortBy, string? sortDir, HashSet<string> allowed, string defaultField)
    {
        var field = !string.IsNullOrWhiteSpace(sortBy) && allowed.Contains(sortBy) ? sortBy : defaultField;
        var dir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        return $"{field} {dir}";
    }

    /// <summary>
    /// Allow-listed ORDER BY with a COMPOSITE default (e.g. FSD "Review Type, Remaining Day").
    /// A caller-supplied <paramref name="sortBy"/> that is on the allow-list wins as a single column;
    /// otherwise the report's fixed <paramref name="defaultFields"/> are emitted in order. Those
    /// fields are report-defined constants (never user input), so they are safe to concatenate; the
    /// allow-list still guards the only user-controlled value (<paramref name="sortBy"/>).
    /// </summary>
    public static string OrderBy(
        string? sortBy, string? sortDir, HashSet<string> allowed, IReadOnlyList<string> defaultFields)
    {
        var dir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (!string.IsNullOrWhiteSpace(sortBy) && allowed.Contains(sortBy))
            return $"{sortBy} {dir}";
        return string.Join(", ", defaultFields.Select(f => $"{f} {dir}"));
    }
}
