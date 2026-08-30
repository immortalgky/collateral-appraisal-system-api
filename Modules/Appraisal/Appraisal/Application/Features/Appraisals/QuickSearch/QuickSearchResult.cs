namespace Appraisal.Application.Features.Appraisals.QuickSearch;

/// <summary>One field that matched, so the UI can say why a row is in the list.</summary>
public record SearchMatch(string Field, string Value);

/// <summary>
/// A matched appraisal. <see cref="NavigateTo"/> is built here rather than in the client because
/// the old contract let the client invent routes, and two of the three it invented did not exist.
/// </summary>
public record SearchAppraisal(
    Guid AppraisalId,
    string? AppraisalNumber,
    Guid RequestId,
    string? RequestNumber,
    string? CustomerName,
    string? Status,
    string? PropertyTypes,
    string? Province,
    string NavigateTo,
    IReadOnlyList<SearchMatch> MatchedOn);

/// <summary>
/// One matched value and every appraisal carrying it. A customer name on three appraisals is one
/// group of three, so the user picks the case they meant instead of guessing between look-alike rows.
/// </summary>
public record SearchGroup(
    string MatchKind,
    string MatchLabel,
    string MatchField,
    int AppraisalCount,
    IReadOnlyList<SearchAppraisal> Appraisals);

public record QuickSearchResult(
    IReadOnlyList<SearchGroup> Groups,
    bool HasMore,
    int TotalMatchedAppraisals);
