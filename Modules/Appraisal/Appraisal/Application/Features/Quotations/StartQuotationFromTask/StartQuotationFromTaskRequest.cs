namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

public record StartQuotationFromTaskRequest(
    Guid AppraisalId,
    Guid RequestId,
    Guid WorkflowInstanceId,
    Guid? TaskExecutionId,
    DateTime DueDate,
    string BankingSegment,
    List<Guid> InvitedCompanyIds,
    string? SpecialRequirements = null,
    Guid? ExistingQuotationRequestId = null,
    int? MaxAppraisalDays = null,
    List<Guid>? ExcludedCompanyIds = null);
