namespace Appraisal.Application.Features.Assignments.RejectAssignment;

public class RejectAssignmentCommandHandler(IAppraisalRepository appraisalRepository)
    : ICommandHandler<RejectAssignmentCommand>
{
    public async Task<Unit> Handle(
        RejectAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var assignment = appraisal.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId)
                         ?? throw new NotFoundException("Assignment", command.AssignmentId);

        assignment.Reject(command.Reason);

        return Unit.Value;
    }
}
