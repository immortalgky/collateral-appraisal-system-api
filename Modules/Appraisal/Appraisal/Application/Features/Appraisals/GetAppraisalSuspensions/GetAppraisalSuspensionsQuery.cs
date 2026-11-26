using Shared.Pagination;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalSuspensions;

/// <summary>
/// Completed appraisals for the Appraisal Suspension screen. <see cref="AppraisalNumber"/> (substring) and
/// <see cref="Status"/> combine. Without a number, only <see cref="SuspensionFilter.Suspended"/> returns rows,
/// so the screen never lists every Completed appraisal.
/// </summary>
public record GetAppraisalSuspensionsQuery(
    string? AppraisalNumber,
    SuspensionFilter Status,
    PaginationRequest Pagination
) : IQuery<PaginatedResult<AppraisalSuspensionRow>>;

public enum SuspensionFilter
{
    All,
    Active,
    Suspended
}

public record AppraisalSuspensionRow(
    Guid AppraisalId,
    string AppraisalNumber,
    string? CustomerName,
    decimal? AppraisedValue,
    string AppraisalType,
    DateTime? CompletedAt,
    bool IsSuspended
);
