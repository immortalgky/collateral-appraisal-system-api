using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Assignments.CancelAssignment;

public record CancelAssignmentCommand(
    Guid AppraisalId,
    Guid AssignmentId,
    string Reason
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
