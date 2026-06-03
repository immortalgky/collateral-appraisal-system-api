using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Appraisals.DeleteProperty;

public class DeletePropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    PricingReferenceCleanupService cleanupService
) : ICommandHandler<DeletePropertyCommand, DeletePropertyResult>
{
    public async Task<DeletePropertyResult> Handle(DeletePropertyCommand command, CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.appraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.appraisalId);

        var property = appraisal.GetProperty(command.propertyId)
            ?? throw new PropertyNotFoundException(command.propertyId);

        // Active cleanup: delete MachineryCostRef PricingAnalyses anchored to this property (DL10).
        await cleanupService.CleanupForPropertyAsync(command.propertyId, cancellationToken);

        appraisal.RemoveProperty(command.propertyId);

        await appraisalRepository.DeleteAsync(property.Id, cancellationToken);
        await appraisalRepository.SaveChangesAsync(cancellationToken);

        return new DeletePropertyResult(IsSuccess: true);
    }
}
