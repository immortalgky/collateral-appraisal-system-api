namespace Appraisal.Application.Features.Quotations.AddAppraisalToDraft;

public record AddAppraisalToDraftRequest(
    Guid AppraisalId,
    int? MaxAppraisalDays = null);
