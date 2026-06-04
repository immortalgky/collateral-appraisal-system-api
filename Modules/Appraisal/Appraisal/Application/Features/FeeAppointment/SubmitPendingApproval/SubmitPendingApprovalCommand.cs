using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.FeeAppointment.SubmitPendingApproval;

/// <summary>
/// Submits all draft pending items (appointment + fee items with RequiresApproval=true and no ApprovalSubmittedAt)
/// for bank approval by stamping ApprovalSubmittedAt and publishing the integration event to the Workflow module.
///
/// Security: the company_id claim must own the named assignment (IDOR gate).
/// </summary>
public record SubmitPendingApprovalCommand(
    Guid AppraisalId,
    Guid AssignmentId,

    /// <summary>The external company's authenticated company_id claim.</summary>
    string RequestedByCompanyId,

    string RequestedBy
) : ICommand<Unit>, ITransactionalCommand<IAppraisalUnitOfWork>;
