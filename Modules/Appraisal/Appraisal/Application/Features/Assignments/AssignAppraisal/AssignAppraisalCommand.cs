namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public record AssignAppraisalCommand(
    Guid AppraisalId,
    string AssignmentType,
    string? AssigneeUserId = null,
    string? AssigneeCompanyId = null,
    string AssignmentMethod = "Manual",
    string? InternalAppraiserId = null,
    string AssignedBy = default
) : ICommand<AssignAppraisalResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
