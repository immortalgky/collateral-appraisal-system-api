using Reporting.Contracts;
using Shared.Data;
using Shared.Pagination;

namespace Reporting.Application.OperationalReports.Shared;

/// <summary>
/// Turns a report's declared <see cref="FilterField"/>s into display-ready
/// <see cref="FilterCriterion"/>s for the export's "Applied filters" block: drops un-applied
/// filters, and resolves coded values to their <c>parameter.Parameters</c> descriptions.
/// </summary>
public interface IReportFilterResolver
{
    Task<IReadOnlyList<FilterCriterion>> ResolveAsync(
        IReadOnlyList<FilterField> fields, CancellationToken cancellationToken = default);
}

internal sealed class ReportFilterResolver(ISqlConnectionFactory connectionFactory) : IReportFilterResolver
{
    // Match the EN code->description resolution the RCAS views already use.
    private const string Language = "EN";

    public async Task<IReadOnlyList<FilterCriterion>> ResolveAsync(
        IReadOnlyList<FilterField> fields, CancellationToken cancellationToken = default)
    {
        // Keep only applied filters (a non-blank value was supplied).
        var applied = fields.Where(f => !string.IsNullOrWhiteSpace(f.RawValue)).ToList();
        if (applied.Count == 0) return [];

        // One lookup for every parameter group referenced by a coded filter.
        var groups = applied
            .Where(f => !string.IsNullOrWhiteSpace(f.ParameterGroup))
            .Select(f => f.ParameterGroup!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var descriptions = await LoadDescriptionsAsync(groups);
        return BuildCriteria(fields, descriptions);
    }

    /// <summary>
    /// Pure mapping (no DB): drops un-applied filters and turns each remaining field into a
    /// <see cref="FilterCriterion"/>, resolving coded values against <paramref name="descriptions"/>.
    /// </summary>
    internal static IReadOnlyList<FilterCriterion> BuildCriteria(
        IReadOnlyList<FilterField> fields,
        IReadOnlyDictionary<(string Group, string Code), string> descriptions)
    {
        var result = new List<FilterCriterion>();
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.RawValue)) continue;

            var value = string.IsNullOrWhiteSpace(field.ParameterGroup)
                ? field.RawValue.Trim()
                : ResolveCodes(field.RawValue, field.ParameterGroup, descriptions);
            result.Add(new FilterCriterion(field.Label, value));
        }

        return result;
    }

    private async Task<Dictionary<(string Group, string Code), string>> LoadDescriptionsAsync(
        IReadOnlyCollection<string> groups)
    {
        if (groups.Count == 0) return new Dictionary<(string, string), string>(KeyComparer);

        var rows = await connectionFactory.QueryAsync<ParameterRow>(
            """
            SELECT [Group], Code, Description
            FROM parameter.Parameters
            WHERE [Language] = @Language AND [Group] IN @Groups
            """,
            new { Language, Groups = groups });

        // Case-insensitive keys: SQL Server matches Code/Group under a CI collation and the report's
        // data filter is CI too, so a different-cased filter value must still resolve here (not fall
        // back to the raw code). Last write wins on a casing-only Code collision.
        var map = new Dictionary<(string, string), string>(KeyComparer);
        foreach (var row in rows)
            map[(row.Group, row.Code)] = row.Description;
        return map;
    }

    /// <summary>Case-insensitive (Group, Code) comparer — see <see cref="LoadDescriptionsAsync"/>.</summary>
    internal static readonly IEqualityComparer<(string Group, string Code)> KeyComparer =
        new GroupCodeComparer();

    private sealed class GroupCodeComparer : IEqualityComparer<(string Group, string Code)>
    {
        public bool Equals((string Group, string Code) x, (string Group, string Code) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Group, y.Group) &&
            StringComparer.OrdinalIgnoreCase.Equals(x.Code, y.Code);

        public int GetHashCode((string Group, string Code) obj) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Group),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Code));
    }

    // Comma-separated codes (mirrors ReportFilterSql.MultiValue) -> "Desc1, Desc2", falling back to
    // the raw code whenever a code has no description so nothing silently disappears.
    private static string ResolveCodes(
        string csv, string group, IReadOnlyDictionary<(string, string), string> descriptions)
    {
        var codes = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length == 0) return csv.Trim();

        var labels = codes.Select(code =>
            descriptions.TryGetValue((group, code), out var desc) ? desc : code);
        return string.Join(", ", labels);
    }

    private sealed record ParameterRow(string Group, string Code, string Description);
}
