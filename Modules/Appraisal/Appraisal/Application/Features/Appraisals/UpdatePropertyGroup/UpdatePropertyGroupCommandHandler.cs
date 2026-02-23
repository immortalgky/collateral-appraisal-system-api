namespace Appraisal.Application.Features.Appraisals.UpdatePropertyGroup;

/// <summary>
/// Handler for updating a PropertyGroup
/// </summary>
public class UpdatePropertyGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdatePropertyGroupCommand, UpdatePropertyGroupResult>
{
    public async Task<UpdatePropertyGroupResult> Handle(
        UpdatePropertyGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        var group = appraisal.Groups.FirstOrDefault(g => g.Id == command.GroupId)
                    ?? throw new InvalidOperationException($"Property group {command.GroupId} not found");

        group.Update(command.GroupName, command.Description, command.UseSystemCalc);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new UpdatePropertyGroupResult(group.Id);
    }
}
