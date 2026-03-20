namespace Appraisal.Application.Features.Appraisals.CopyPropertyToGroup;

public class CopyPropertyToGroupCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CopyPropertyToGroupCommand, CopyPropertyToGroupResult>
{
    public async Task<CopyPropertyToGroupResult> Handle(
        CopyPropertyToGroupCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // Step 1: Copy property and save so EF Core generates the ID
        var newProperty = appraisal.CopyProperty(command.SourcePropertyId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 2: Now newProperty.Id is valid — assign to group and save
        appraisal.AddPropertyToGroup(command.TargetGroupId, newProperty.Id);

        return new CopyPropertyToGroupResult(newProperty.Id);
    }
}