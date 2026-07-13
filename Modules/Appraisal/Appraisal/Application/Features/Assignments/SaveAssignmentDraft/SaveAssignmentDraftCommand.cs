using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.SaveAssignmentDraft;

/// <summary>
/// Persists the admin's in-progress assignment decision (selections + remark) as a draft onto the
/// existing Pending AppraisalAssignment row. Transactional — the mutation is committed by the pipeline.
/// Does NOT resume the workflow; the row stays Pending until the admin clicks Assign.
/// </summary>
public record SaveAssignmentDraftCommand(
    Guid AppraisalId,
    string AssignmentType,
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,
    string AssignmentMethod = "Manual",
    string? InternalAppraiserId = null,
    string? InternalFollowupAssignmentMethod = null,
    string? Remark = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
