using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.FeeAppointment.SubmitPendingApproval;

/// <summary>
/// Submits all draft pending items (appointment + fee items with RequiresApproval=true and no ApprovalSubmittedAt)
/// for bank approval by stamping ApprovalSubmittedAt and publishing the integration event to the Workflow module.
///
/// Access is workflow-scoped: the workflow engine only assigns a task to the company that owns the
/// assignment, so no additional IDOR gate is needed here.
/// </summary>
public record SubmitPendingApprovalCommand(
    Guid AppraisalId,
    Guid AssignmentId,

    /// <summary>Approval routing source — Ext (external company) or Int (bank-internal), derived from the caller's claims.</summary>
    string RequestSource,

    string RequestedBy
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
