using Appraisal.Domain.Appraisals;
using Appraisal.Domain.MarketComparables;
using Appraisal.Domain.Services;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public class LinkComparableCommandHandler(
    IPricingAnalysisRepository repository,
    IMarketComparableRepository marketComparableRepository
) : ICommandHandler<LinkComparableCommand, LinkComparableResult>
{
    public async Task<LinkComparableResult> Handle(
        LinkComparableCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
        {
            throw new InvalidOperationException($"Pricing analysis with ID {command.PricingAnalysisId} not found.");
        }

        // Find the method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
        {
            throw new InvalidOperationException($"Pricing method with ID {command.MethodId} not found.");
        }

        // Link the comparable
        var link = method.LinkComparable(
            command.MarketComparableId,
            command.DisplaySequence,
            command.Weight);

        // Create a calculation for this comparable
        var calculation = method.AddCalculation(command.MarketComparableId);

        // Seed calculation with pricing data from MarketComparable
        var comparable = await marketComparableRepository.GetByIdAsync(command.MarketComparableId, cancellationToken);

        if (comparable is not null)
        {
            if (comparable.OfferPrice.HasValue)
            {
                calculation.SetOfferingPrice(
                    comparable.OfferPrice.Value,
                    "PerUnit",
                    comparable.OfferPriceAdjustmentPercent,
                    comparable.OfferPriceAdjustmentAmount);
            }

            if (comparable.SalePrice.HasValue)
            {
                calculation.SetSellingPrice(comparable.SalePrice.Value);
            }

            if (comparable.SaleDate.HasValue)
            {
                var (years, months) = PricingCalculationHelper.ComputeTimeFromSaleDate(comparable.SaleDate.Value);
                calculation.SetTimeAdjustment(years, months, null, null);
            }
        }

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new LinkComparableResult(
            link.Id,
            calculation.Id,
            comparable?.OfferPrice,
            comparable?.OfferPriceAdjustmentPercent,
            comparable?.OfferPriceAdjustmentAmount,
            comparable?.SalePrice,
            comparable?.SaleDate);
    }
}
