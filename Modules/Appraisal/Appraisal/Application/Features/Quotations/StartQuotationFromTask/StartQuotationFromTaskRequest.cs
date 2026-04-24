namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

public record StartQuotationFromTaskRequest(
    Guid AppraisalId,
    Guid RequestId,
    Guid WorkflowInstanceId,
    Guid? TaskExecutionId,
    DateTime DueDate,
    string BankingSegment,
    List<Guid> InvitedCompanyIds,
    string AppraisalNumber,
    string PropertyType,
    string? PropertyLocation = null,
    decimal? EstimatedValue = null,
    string? SpecialRequirements = null,
    Guid? ExistingQuotationRequestId = null);
