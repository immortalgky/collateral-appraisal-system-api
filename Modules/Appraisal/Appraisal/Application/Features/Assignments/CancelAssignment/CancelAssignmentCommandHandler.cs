namespace Appraisal.Application.Features.Assignments.CancelAssignment;

public class CancelAssignmentCommandHandler(IAppraisalRepository appraisalRepository)
    : ICommandHandler<CancelAssignmentCommand>
{
    public async Task<Unit> Handle(
        CancelAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var assignment = appraisal.Assignments.FirstOrDefault(a => a.Id == command.AssignmentId)
                         ?? throw new NotFoundException("Assignment", command.AssignmentId);

        assignment.Cancel(command.Reason);

        return Unit.Value;
    }
}
