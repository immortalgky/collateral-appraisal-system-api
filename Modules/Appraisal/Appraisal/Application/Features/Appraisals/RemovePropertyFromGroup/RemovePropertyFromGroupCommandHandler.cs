namespace Appraisal.Application.Features.Appraisals.RemovePropertyFromGroup;

/// <summary>
/// Handler for removing a property from a group
/// </summary>
public class RemovePropertyFromGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<RemovePropertyFromGroupCommand, RemovePropertyFromGroupResult>
{
    public async Task<RemovePropertyFromGroupResult> Handle(
        RemovePropertyFromGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        appraisal.RemovePropertyFromGroup(command.GroupId, command.PropertyId);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new RemovePropertyFromGroupResult(true);
    }
}
