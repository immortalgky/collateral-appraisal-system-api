namespace Appraisal.Application.Features.Appraisals.AddPropertyToGroup;

/// <summary>
/// Handler for adding a property to a group
/// </summary>
public class AddPropertyToGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<AddPropertyToGroupCommand, AddPropertyToGroupResult>
{
    public async Task<AddPropertyToGroupResult> Handle(
        AddPropertyToGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        appraisal.AddPropertyToGroup(command.GroupId, command.PropertyId);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new AddPropertyToGroupResult(true);
    }
}
