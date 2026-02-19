namespace Appraisal.Application.Features.Appraisals.ReorderPropertiesInGroup;

public class ReorderPropertiesInGroupCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<ReorderPropertiesInGroupCommand, ReorderPropertiesInGroupResult>
{
    public async Task<ReorderPropertiesInGroupResult> Handle(
        ReorderPropertiesInGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {command.AppraisalId} not found");

        appraisal.ReorderPropertiesInGroup(command.GroupId, command.OrderedPropertyIds);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return new ReorderPropertiesInGroupResult(true);
    }
}
