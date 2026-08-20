using Appraisal.Application.Configurations;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using MediatR;
using Parameter.Contracts.PricingParameters;
using DomainProject = Appraisal.Domain.Projects.Project;

namespace Appraisal.Application.Features.Project.CalculateProjectUnitPrices;

/// <summary>
/// Unified unit-price calculator for both Condo and LandAndBuilding projects.
/// All business logic (floor increment, location adjustments, rounding rules) lives in
/// <see cref="DomainProject.CalculateUnitPrices"/>. This handler is responsible only for
/// loading the aggregate, supplying the existing price map (upsert), and persisting results.
/// </summary>
public class CalculateProjectUnitPricesCommandHandler(
    IProjectRepository projectRepository,
    IPricingAnalysisRepository pricingAnalysisRepository,
    AppraisalDbContext dbContext,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalValuationSummaryService valuationSummaryService,
    ISender mediator
) : ICommandHandler<CalculateProjectUnitPricesCommand>
{
    public async Task<Unit> Handle(
        CalculateProjectUnitPricesCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetWithFullGraphAsync(command.AppraisalId, cancellationToken)
                      ?? throw new InvalidOperationException($"Project not found for appraisal {command.AppraisalId}");

        // Load existing unit prices keyed by ProjectUnitId for upsert
        var unitIds = project.Units.Select(u => u.Id).ToList();
        var existingPrices = await dbContext.ProjectUnitPrices
            .Where(p => unitIds.Contains(p.ProjectUnitId))
            .ToListAsync(cancellationToken);

        var existingPriceMap = existingPrices.ToDictionary(p => p.ProjectUnitId);

        // Fetch PricingAnalysis FinalAppraisedValue per model id (separate aggregate — no nav property).
        var modelIds = project.Models.Select(m => m.Id);
        var paSummaries = await pricingAnalysisRepository
            .GetProjectModelPricingSummariesAsync(modelIds, cancellationToken);
        var standardPriceByModelId = paSummaries.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.FinalAppraisedValue);

        // Fetch fire-insurance coverage rates (Parameter-module reference data; no kind filter —
        // both Condo and LandAndBuilding conditions are needed).
        var ratesResult = await mediator.Send(new GetFireInsuranceRatesQuery(), cancellationToken);
        var ratesByCondition = ratesResult.Rates.ToDictionary(
            r => r.Condition,
            r => r.RatePerSqm,
            StringComparer.Ordinal);

        // Domain method performs all type-specific calculations and returns the updated price rows
        var prices = project.CalculateUnitPrices(existingPriceMap, standardPriceByModelId, ratesByCondition);

        // Upsert: new rows get Added, existing rows were mutated in-place by the domain method
        foreach (var price in prices)
        {
            if (!existingPriceMap.ContainsKey(price.ProjectUnitId))
                dbContext.ProjectUnitPrices.Add(price);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Persist the block's appraisal-level total to ValuationAnalyses so collateral master / AS400 /
        // the workflow approval-tier switch (AppraisalValueChangedIntegrationEvent) see it. The unit
        // prices are now saved, so RecomputeAsync's block branch (EF sums on this DbContext) reads them.
        // CalculateProjectUnitPricesCommand is ITransactionalCommand, so this stays inside the
        // transaction; the staged ValuationAnalyses upsert + outbox message flush on commit. No
        // valuationDate override needed — the block appraisal's appointment already exists, so the
        // service derives ValuationDate from it.
        await valuationSummaryService.RecomputeAsync(command.AppraisalId, cancellationToken);

        return Unit.Value;
    }
}
