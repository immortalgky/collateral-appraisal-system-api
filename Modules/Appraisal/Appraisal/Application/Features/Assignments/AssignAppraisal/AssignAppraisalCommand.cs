using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public record AssignAppraisalCommand(
    Guid AppraisalId,
    string AssignmentMode,
    Guid? AssigneeUserId = null,
    Guid? AssigneeCompanyId = null,
    string AssignmentSource = "Manual",
    Guid AssignedBy = default
) : ICommand<AssignAppraisalResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
