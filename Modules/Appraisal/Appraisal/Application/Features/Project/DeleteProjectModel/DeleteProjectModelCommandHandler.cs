using Appraisal.Application.Services;

namespace Appraisal.Application.Features.Project.DeleteProjectModel;

/// <summary>Handler for deleting a project model.</summary>
/// <remarks>
/// The former DB cascade FK (PricingAnalysis.ProjectModelId → ProjectModels.Id) was dropped
/// when ProjectModelId was merged into the generic AnchorId column. App-level cleanup here
/// replaces that cascade, deleting the model's PricingAnalysis and any reference analyses
/// hosted by its methods (DL10).
/// </remarks>
public class DeleteProjectModelCommandHandler(
    IAppraisalUnitOfWork unitOfWork,
    IProjectRepository projectRepository,
    PricingReferenceCleanupService cleanupService
) : ICommandHandler<DeleteProjectModelCommand>
{
    public async Task<Unit> Handle(
        DeleteProjectModelCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // App-level FK guard: delete subject PA + hosted reference PAs before removing the model (DL10).
        await cleanupService.CleanupForProjectModelAsync(command.ModelId, cancellationToken);

        project.RemoveModel(command.ModelId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
