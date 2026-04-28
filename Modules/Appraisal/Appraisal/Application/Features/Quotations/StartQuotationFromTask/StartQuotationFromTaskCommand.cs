using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

/// <summary>
/// Creates a new QuotationRequest (Draft) linked to an appraisal-assignment workflow task (IBG segment),
/// OR adds an appraisal to an existing Draft QuotationRequest.
///
/// If ExistingQuotationRequestId is provided:
///   - The existing Draft must belong to this admin (RequestedBy = currentUser) and be in Draft status.
///   - The appraisal is added via AddAppraisal; companies and dates are NOT changed.
///   - AppraisalAddedToQuotationIntegrationEvent is emitted.
///
/// If ExistingQuotationRequestId is null:
///   - A new Draft is created with the given DueDate, BankingSegment, invited companies, appraisal.
///   - QuotationStartedIntegrationEvent is NOT emitted at creation — it fires when Send() is called.
///
/// RequestedBy (username) and RmUserId are resolved server-side — not accepted from the request body.
/// </summary>
public record StartQuotationFromTaskCommand(
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
    /// <summary>
    /// When set, adds the appraisal to this existing Draft quotation instead of creating a new one.
    /// </summary>
    Guid? ExistingQuotationRequestId = null,
    int? MaxAppraisalDays = null,
    string? AssignmentType = null,
    string? AssignmentMethod = null,
    string? InternalFollowupAssignmentMethod = null
) : ICommand<StartQuotationFromTaskResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
