namespace Workflow.Contracts.FeeAppointmentApprovals;

/// <summary>
/// Resolves (approves or rejects) a FeeAppointmentApproval with per-component decisions.
/// Called by the approver task completion in the child approval workflow.
/// </summary>
public record ResolveFeeAppointmentApprovalCommand(
    Guid ApprovalId,
    string Actor,
    FeeApprovalComponentDecision AppointmentDecision,
    FeeApprovalComponentDecision FeeDecision)
    : ICommand<Unit>, ITransactionalCommand<IWorkflowUnitOfWork>;

/// <summary>
/// A single per-component decision within a FeeAppointmentApproval resolution.
/// </summary>
public record FeeApprovalComponentDecision(
    /// <summary>"approve" or "reject"</summary>
    string Decision,
    string? Reason = null);
