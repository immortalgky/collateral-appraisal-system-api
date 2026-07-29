using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Returns the minimum reference data for a prior appraisal (number, value, appraisal date, status).
/// Used by the Request module to populate PrevAppraisalNumber/Value/Date at read time, and to gate
/// Appeal/Progressive submission on the prior appraisal being Completed.
/// Returns null when the appraisal does not exist.
/// </summary>
public record GetAppraisalReferenceQuery(Guid AppraisalId)
    : IRequest<AppraisalReferenceResult?>;

/// <param name="AppraisalDate">
/// appraisal.ValuationAnalyses.ValuationDate — the valuation date, NOT the appointment/inspection
/// slot. Null until the prior appraisal has a ValuationAnalyses row.
/// </param>
public record AppraisalReferenceResult(
    string? AppraisalNumber,
    decimal? AppraisalValue,
    DateTime? AppraisalDate,
    string? Status,
    string? CustomerName = null);
