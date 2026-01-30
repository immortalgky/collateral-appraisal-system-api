namespace Appraisal.Application.Features.Appraisals.CreatePropertyGroup;

/// <summary>
/// Handler for creating a new PropertyGroup
/// </summary>
public class CreatePropertyGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreatePropertyGroupCommand, CreatePropertyGroupResult>
{
    public async Task<CreatePropertyGroupResult> Handle(
        CreatePropertyGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        var group = appraisal.CreateGroup(command.GroupName, command.Description);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new CreatePropertyGroupResult(group.Id, group.GroupNumber);
    }
}
