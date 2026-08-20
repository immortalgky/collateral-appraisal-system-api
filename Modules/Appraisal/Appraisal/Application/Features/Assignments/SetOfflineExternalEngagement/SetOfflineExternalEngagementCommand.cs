using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// Records the external company that appraised the collateral OUTSIDE the system, plus the
/// appraisal date printed on that company's book, while an internal appraiser keys the book in
/// at the int-offline-book-keyin activity.
///
/// Unlike the normal external path there is no CompanySelectionActivity to run — the bank
/// engaged the company off-system, so nothing upstream knows which company it was. This command
/// therefore does what CompanyAssignedIntegrationEventHandler does for the in-system path:
/// promotes the assignment to External/Assigned and materialises the fee items. It stays in the
/// Appraisal module, so it calls IAssignmentFeeService directly rather than round-tripping
/// through an integration event.
/// </summary>
public record SetOfflineExternalEngagementCommand(
    Guid AppraisalId,
    Guid CompanyId,
    DateTime BookDate,
    string? ExternalAppraiserName = null,
    string AssignedBy = ""
) : ICommand<SetOfflineExternalEngagementResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
