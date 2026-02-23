namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public class AssignAppraisalCommandHandler(IAppraisalRepository appraisalRepository)
    : ICommandHandler<AssignAppraisalCommand, AssignAppraisalResult>
{
    public async Task<AssignAppraisalResult> Handle(
        AssignAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var assignment =
            appraisal.Assignments.FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Pending);
        if (assignment is not null)
            assignment.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                assignedBy: command.AssignedBy
            );
        else
            appraisal.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                assignedBy: command.AssignedBy);

        return new AssignAppraisalResult(assignment?.Id ?? Guid.Empty);
    }
}
