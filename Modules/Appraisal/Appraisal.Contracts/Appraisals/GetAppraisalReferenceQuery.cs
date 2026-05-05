using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Returns the minimum reference data for a prior appraisal (number, value, completed date).
/// Used by the Request module to populate PrevAppraisalNumber/Value/Date at read time.
/// Returns null when the appraisal does not exist.
/// </summary>
public record GetAppraisalReferenceQuery(Guid AppraisalId)
    : IRequest<AppraisalReferenceResult?>;

public record AppraisalReferenceResult(
    string? AppraisalNumber,
    decimal? AppraisalValue,
    DateTime? CompletedDate);
