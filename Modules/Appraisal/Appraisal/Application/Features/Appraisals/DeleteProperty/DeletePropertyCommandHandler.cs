namespace Appraisal.Application.Features.Appraisals.DeleteProperty;

public class DeletePropertyCommandHandler(
    IAppraisalRepository appraisalRepository

) : ICommandHandler<DeletePropertyCommand, DeletePropertyResult>
{
    public async Task<DeletePropertyResult> Handle(DeletePropertyCommand command, CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.appraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.appraisalId);
        
        var property = appraisal.GetProperty(command.propertyId)
            ?? throw new PropertyNotFoundException(command.propertyId);

        appraisal.RemoveProperty(command.propertyId);
        
        await appraisalRepository.DeleteAsync(property.Id, cancellationToken);
        await appraisalRepository.SaveChangesAsync(cancellationToken);

        return new DeletePropertyResult(IsSuccess: true);
    }
}
