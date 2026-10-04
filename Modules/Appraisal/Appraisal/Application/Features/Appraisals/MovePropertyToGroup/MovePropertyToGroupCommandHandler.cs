namespace Appraisal.Application.Features.Appraisals.MovePropertyToGroup;

public class MovePropertyToGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<MovePropertyToGroupCommand, MovePropertyToGroupResult>
{
    public async Task<MovePropertyToGroupResult> Handle(
        MovePropertyToGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        appraisal.MovePropertyToGroup(command.PropertyId, command.TargetGroupId, command.TargetPosition);

        return new MovePropertyToGroupResult(true);
    }
}
