namespace Appraisal.Application.Features.Project.SaveProjectPricingAssumptions;

public class SaveProjectPricingAssumptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/appraisals/{appraisalId:guid}/project/pricing-assumptions",
                async (
                    Guid appraisalId,
                    SaveProjectPricingAssumptionsRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SaveProjectPricingAssumptionsCommand(
                        appraisalId,
                        request.LocationMethod,
                        request.CornerAdjustment,
                        request.EdgeAdjustment,
                        request.OtherAdjustment,
                        request.ForceSalePercentage,
                        request.PoolViewAdjustment,
                        request.SouthAdjustment,
                        request.FloorIncrementEveryXFloor,
                        request.FloorIncrementAmount,
                        request.NearGardenAdjustment,
                        request.LandIncreaseDecreaseRate,
                        request.ModelAssumptions);

                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(new SaveProjectPricingAssumptionsResponse(result.Id));
                }
            )
            .WithName("SaveProjectPricingAssumptions")
            .Produces<SaveProjectPricingAssumptionsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save project pricing assumptions")
            .WithDescription("Saves pricing assumptions for a project. Automatically routes to the correct domain method (Condo or LandAndBuilding) based on the project type. ModelAssumptions null = no change; empty array = clear all.")
            .WithTags("Project");
    }
}
