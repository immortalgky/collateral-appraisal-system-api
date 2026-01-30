namespace Appraisal.Application.Features.Appraisals.DeletePropertyGroup;

/// <summary>
/// Handler for deleting a PropertyGroup
/// </summary>
public class DeletePropertyGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<DeletePropertyGroupCommand, DeletePropertyGroupResult>
{
    public async Task<DeletePropertyGroupResult> Handle(
        DeletePropertyGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        appraisal.DeleteGroup(command.GroupId);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new DeletePropertyGroupResult(true);
    }
}
