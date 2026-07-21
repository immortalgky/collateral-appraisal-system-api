using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Returns the minimum reference data for a prior appraisal (number, value, appointment date, status).
/// Used by the Request module to populate PrevAppraisalNumber/Value/Date at read time, and to gate
/// Appeal/Progressive submission on the prior appraisal being Completed.
/// Returns null when the appraisal does not exist.
/// </summary>
public record GetAppraisalReferenceQuery(Guid AppraisalId)
    : IRequest<AppraisalReferenceResult?>;

public record AppraisalReferenceResult(
    string? AppraisalNumber,
    decimal? AppraisalValue,
    DateTime? AppointmentDate,
    string? Status,
    string? CustomerName = null);
