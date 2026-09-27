using System.Data;
using System.Text.Json;
using Dapper;

namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Segment Coverage rule (see CONTEXT.md): a company covers a quotation when its Company Loan Types
/// include every Banking Segment in the quotation's Segment Set. Extra loan types are fine and the
/// comparison is case-insensitive. Lives here once so the read side and the Send guard cannot drift.
/// </summary>
public static class SegmentCoverage
{
    public const string MismatchCode = "SEGMENT_COVERAGE_MISMATCH";

    /// <summary>Distinct, non-blank segments (first spelling wins), in first-seen order.</summary>
    public static string[] BuildSegmentSet(IEnumerable<string?> appraisalSegments) =>
        appraisalSegments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Segments in the set the company cannot appraise. Empty means the company covers the set.</summary>
    public static string[] MissingSegments(IEnumerable<string> segmentSet, IEnumerable<string>? companyLoanTypes)
    {
        var owned = new HashSet<string>(
            (companyLoanTypes ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return segmentSet.Where(s => !owned.Contains(s)).ToArray();
    }

    /// <summary>
    /// Reads Company.LoanTypes (stored as a JSON array in auth.Companies) for the given companies.
    /// Same cross-schema Dapper pattern GetQuotationById already uses for company names.
    /// </summary>
    public static async Task<Dictionary<Guid, string[]>> LoadCompanyLoanTypesAsync(
        IDbConnection connection,
        Guid[] companyIds)
    {
        if (companyIds.Length == 0)
            return new Dictionary<Guid, string[]>();

        var rows = await connection.QueryAsync<(Guid Id, string? LoanTypes)>(
            "SELECT c.Id, c.LoanTypes FROM [auth].[Companies] c WHERE c.Id IN @CompanyIds",
            new { CompanyIds = companyIds });

        return rows.ToDictionary(r => r.Id, r => ParseLoanTypes(r.LoanTypes));
    }

    private static string[] ParseLoanTypes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
