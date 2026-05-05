using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.CreateProjectModelPricingAnalysis;

/// <summary>
/// Creates a new PricingAnalysis bound to a ProjectModel.
/// Mirrors CreatePricingAnalysisCommandHandler for the PropertyGroup path.
/// </summary>
public class CreateProjectModelPricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<CreateProjectModelPricingAnalysisCommand, CreateProjectModelPricingAnalysisResult>
{
    public async Task<CreateProjectModelPricingAnalysisResult> Handle(
        CreateProjectModelPricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // Pre-check: throws ConflictException (409) if already exists.
        // The filtered unique index on ProjectModelId is the DB-level race guard for concurrent requests.
        var exists = await pricingAnalysisRepository.ExistsByProjectModelIdAsync(
            command.ProjectModelId,
            cancellationToken);

        if (exists)
            throw new ConflictException(
                "PricingAnalysis", command.ProjectModelId);

        var pricingAnalysis = Domain.Appraisals.PricingAnalysis.CreateForProjectModel(command.ProjectModelId);

        await pricingAnalysisRepository.AddAsync(pricingAnalysis, cancellationToken);

        return new CreateProjectModelPricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
