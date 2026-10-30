using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// Records the external company that appraised the collateral OUTSIDE the system, plus the
/// appraisal date printed on that company's book, while an internal appraiser keys the book in
/// at the int-offline-book-keyin activity.
///
/// Unlike the normal external path there is no CompanySelectionActivity to run. The admin names the
/// company at assignment (AssignAppraisalCommandHandler, decision EXTO), and here it is locked; only a
/// case that reached key-in before that existed still takes the company from this command. This command
/// does what CompanyAssignedIntegrationEventHandler does for the in-system path:
/// promotes the assignment to External/Assigned and materialises the fee items. It stays in the
/// Appraisal module, so it calls IAssignmentFeeService directly rather than round-tripping
/// through an integration event.
/// </summary>
public record SetOfflineExternalEngagementCommand(
    Guid AppraisalId,
    /// <summary>
    /// Only for a case whose assignment holds no company (reached key-in before the admin picked it at assignment).
    /// Once the admin recorded one it is locked here: omit it or send the same id; a wrong one is fixed by
    /// routing back to appraisal-assignment.
    /// </summary>
    Guid? CompanyId,
    DateTime BookDate,
    string? ExternalAppraiserName = null
) : ICommand<SetOfflineExternalEngagementResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
