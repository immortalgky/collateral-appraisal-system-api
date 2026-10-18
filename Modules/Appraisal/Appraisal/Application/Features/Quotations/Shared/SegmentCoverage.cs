using System.Data;
using Dapper;

namespace Appraisal.Application.Features.Quotations.Shared;

/// <summary>
/// Segment Coverage rule: a company covers a quotation when its Company Loan Types
/// include every Banking Segment in the quotation's Segment Set (the distinct Banking Segments of the
/// appraisals currently on it, always computed live). Extra loan types are fine and the
/// comparison is case-insensitive. Lives here once so the read side and the Send guard cannot drift.
/// </summary>
public static class SegmentCoverage
{
    public const string MismatchCode = "SEGMENT_COVERAGE_MISMATCH";

    /// <summary>
    /// Trimmed, non-blank segments, deduplicated case-insensitively and sorted (OrdinalIgnoreCase) so the
    /// result is deterministic.
    /// </summary>
    public static List<string> BuildSegmentSet(IEnumerable<string?> appraisalSegments) =>
        appraisalSegments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Segments in the set the company cannot appraise. Empty means the company covers the set.</summary>
    public static List<string> MissingSegments(IEnumerable<string> segmentSet, IEnumerable<string>? companyLoanTypes)
    {
        var owned = new HashSet<string>(
            (companyLoanTypes ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return segmentSet.Where(s => !owned.Contains(s)).ToList();
    }

    public static async Task<List<string>> LoadAppraisalSegmentSetAsync(IDbConnection connection, Guid[] appraisalIds)
    {
        if (appraisalIds.Length == 0)
            return [];
        var segments = await connection.QueryAsync<string?>(
            "SELECT a.BankingSegment FROM [appraisal].[Appraisals] a WHERE a.Id IN @AppraisalIds",
            new { AppraisalIds = appraisalIds });
        return BuildSegmentSet(segments);
    }
}
