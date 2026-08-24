using Appraisal.Application.Services;
using Mapster;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValueManual;


public class UpdateFinalValueManualEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/pricing-analysis/{id:guid}/methods/{methodId:guid}/land-value",
                async (
                    Guid id,
                    Guid methodId,
                    UpdateFinalValueManualRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new UpdateFinalValueManualCommand(
                        id,
                        methodId,
                        request.LandValue
                    );

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UpdateFinalValueManualResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("UpdateFinalValueManual")
            .Produces<UpdateFinalValueManualResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update manual land value")
            .WithDescription("Persists the land value for a manual-mode pricing method, creating its final value row if one doesn't exist yet, and returns the resolved land area and computed building value.")
            .WithTags("PricingAnalysis");
    }
}
public record UpdateFinalValueManualRequest(
    decimal LandValue
);

public record UpdateFinalValueManualCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    decimal LandValue
) : ICommand<UpdateFinalValueManualResult>,
ITransactionalCommand<IAppraisalUnitOfWork>;

public class UpdateFinalValueManualCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<UpdateFinalValueManualCommand, UpdateFinalValueManualResult>
{
    public async Task<UpdateFinalValueManualResult> Handle(UpdateFinalValueManualCommand command, CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new NotFoundException("PricingAnalysisMethod", command.MethodId);

        if (method.FinalValue is null)
            method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m, 0m));

        decimal? totalLandAreaFromTitles = null;
        decimal buildingValue = 0m;
        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
        {
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);

            buildingValue = await propertyDataService.GetTotalBuildingValueAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);
        }

        var landArea = totalLandAreaFromTitles ?? 0m;

        // Unconditional — unlike UpdateFinalValue/SetFinalValue, this endpoint is only ever
        // reached for a manually-keyed lump-sum method, so there is no PricingUnit.IsPerUnitRate
        // gate to satisfy (a lump-sum method never has one).
        method.FinalValue!.SetLandAreaValues(landArea, command.LandValue);

        return new UpdateFinalValueManualResult(
            method.Id,
            command.LandValue,
            method.FinalValue.LandArea,
            buildingValue);
    }
}

public record UpdateFinalValueManualResult(
    Guid MethodId,
    decimal LandValue,
    decimal? LandArea,
    decimal BuildingValue
);

public record UpdateFinalValueManualResponse(
    Guid MethodId,
    decimal LandValue,
    decimal? LandArea,
    decimal BuildingValue
);
