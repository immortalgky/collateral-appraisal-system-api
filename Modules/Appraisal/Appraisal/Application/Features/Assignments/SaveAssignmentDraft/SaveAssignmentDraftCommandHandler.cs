namespace Appraisal.Application.Features.Assignments.SaveAssignmentDraft;

public class SaveAssignmentDraftCommandHandler(IAppraisalRepository appraisalRepository)
    : ICommandHandler<SaveAssignmentDraftCommand>
{
    public async Task<Unit> Handle(
        SaveAssignmentDraftCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        var pendingAssignment = appraisal.Assignments
            .FirstOrDefault(a => a.AssignmentStatus == AssignmentStatus.Pending)
            ?? throw new BadRequestException(
                $"No pending assignment found for appraisal '{command.AppraisalId}'. " +
                "The workflow task must be in Pending status to accept a draft.");

        pendingAssignment.SaveDraft(
            command.AssignmentType,
            command.AssigneeUserId,
            command.AssigneeCompanyId,
            command.AssignmentMethod,
            command.InternalAppraiserId,
            command.InternalFollowupAssignmentMethod,
            command.Remark);

        return Unit.Value;
    }
}
