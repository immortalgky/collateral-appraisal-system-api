using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.RejectAssignment;

public record RejectAssignmentCommand(
    Guid AppraisalId,
    Guid AssignmentId,
    string Reason
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
