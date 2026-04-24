using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Assignments.AssignAppraisal;

public class AssignAppraisalCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAssignmentFeeService feeService)
    : ICommandHandler<AssignAppraisalCommand, AssignAppraisalResult>
{
    public async Task<AssignAppraisalResult> Handle(
        AssignAppraisalCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        AppraisalAssignment resolvedAssignment;

        var pendingAssignment =
            appraisal.Assignments.FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Pending);
        if (pendingAssignment is not null)
        {
            pendingAssignment.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                command.InternalFollowupAssignmentMethod,
                assignedBy: command.AssignedBy);
            resolvedAssignment = pendingAssignment;
        }
        else
        {
            resolvedAssignment = appraisal.Assign(
                command.AssignmentType,
                command.AssigneeUserId,
                command.AssigneeCompanyId,
                command.AssignmentMethod,
                command.InternalAppraiserId,
                command.InternalFollowupAssignmentMethod,
                assignedBy: command.AssignedBy);
        }

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: command.AppraisalId,
            assignmentId: resolvedAssignment.Id,
            source: new AssignmentFeeSource.TierBased(),
            ct: cancellationToken);

        return new AssignAppraisalResult(resolvedAssignment.Id);
    }
}
