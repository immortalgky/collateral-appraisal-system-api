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

        var assignment = appraisal.Assign(
            command.AssignmentMode,
            command.AssigneeUserId,
            command.AssigneeCompanyId,
            command.AssignmentSource,
            assignedBy: command.AssignedBy);

        return new AssignAppraisalResult(assignment.Id);
    }
}
