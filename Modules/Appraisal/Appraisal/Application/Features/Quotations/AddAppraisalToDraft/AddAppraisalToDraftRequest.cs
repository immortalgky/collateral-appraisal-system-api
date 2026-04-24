namespace Appraisal.Application.Features.Quotations.AddAppraisalToDraft;

public record AddAppraisalToDraftRequest(
    Guid AppraisalId,
    string AppraisalNumber,
    string PropertyType,
    string? PropertyLocation = null,
    decimal? EstimatedValue = null);
