using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.ResetPricingMethod;

public class ResetPricingMethodCommandHandler(
    IPricingAnalysisRepository repository,
    PricingReferenceCleanupService cleanupService
) : ICommandHandler<ResetPricingMethodCommand, ResetPricingMethodResult>
{
    public async Task<ResetPricingMethodResult> Handle(
        ResetPricingMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException(
                $"Pricing analysis with ID {command.PricingAnalysisId} not found.");

        var approach = pricingAnalysis.Approaches
            .FirstOrDefault(a => a.Methods.Any(m => m.Id == command.MethodId));

        if (approach is null)
            throw new InvalidOperationException(
                $"Pricing method with ID {command.MethodId} not found.");

        var method = approach.Methods.First(m => m.Id == command.MethodId);

        // Active cleanup: delete all reference PricingAnalyses whose HostMethodId == this
        // method (DL10) — covers RoomIncomeRef/IncomeLandRef left behind by Income methods,
        // and is a safe no-op for method types that never create host-scoped references
        // (WQS, SaleGrid, DirectComparison, MachineCost). Mirrors RemoveMethodCommandHandler.
        await cleanupService.CleanupForMethodRemovalAsync(command.MethodId, cancellationToken);

        method.Reset();
        approach.ClearValue();
        approach.Unselect();
        pricingAnalysis.ClearFinalValues();

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new ResetPricingMethodResult(command.MethodId, true);
    }
}
