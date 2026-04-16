using Parameter.PricingParameters.Models;

namespace Parameter.PricingParameters.Features.GetPricingParameters;

public class GetPricingParametersQueryHandler(
    ParameterDbContext context
) : IQueryHandler<GetPricingParametersQuery, GetPricingParametersResult>
{
    public async Task<GetPricingParametersResult> Handle(
        GetPricingParametersQuery query,
        CancellationToken cancellationToken)
    {
        var roomTypes = await context.PricingParameterRoomTypes
            .OrderBy(r => r.DisplaySeq)
            .Select(r => new RoomTypeDto(r.Code, r.Name, r.DisplaySeq))
            .ToListAsync(cancellationToken);

        var jobPositions = await context.PricingParameterJobPositions
            .OrderBy(j => j.DisplaySeq)
            .Select(j => new JobPositionDto(j.Code, j.Name, j.DisplaySeq))
            .ToListAsync(cancellationToken);

        var taxBrackets = await context.PricingParameterTaxBrackets
            .OrderBy(t => t.Tier)
            .Select(t => new TaxBracketDto(t.Tier, t.TaxRate, t.MinValue, t.MaxValue))
            .ToListAsync(cancellationToken);

        var assumptionTypes = await context.PricingParameterAssumptionTypes
            .OrderBy(a => a.DisplaySeq)
            .Select(a => new AssumptionTypeDto(a.Code, a.Name, a.Category, a.DisplaySeq))
            .ToListAsync(cancellationToken);

        var methodRows = await context.PricingParameterAssumptionMethods
            .OrderBy(am => am.AssumptionType)
            .ThenBy(am => am.MethodTypeCode)
            .ToListAsync(cancellationToken);

        var assumptionMethodMatrix = methodRows
            .GroupBy(am => am.AssumptionType)
            .Select(g => new AssumptionMethodMatrixDto(
                g.Key,
                g.Select(am => am.MethodTypeCode).ToList()))
            .ToList();

        return new GetPricingParametersResult(
            roomTypes,
            jobPositions,
            taxBrackets,
            assumptionTypes,
            assumptionMethodMatrix);
    }
}
