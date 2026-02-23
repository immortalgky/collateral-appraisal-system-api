using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetLandAndBuildingProperty;

public class GetLandAndBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-and-building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLandAndBuildingPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLandAndBuildingPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLandAndBuildingProperty")
            .Produces<GetLandAndBuildingPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get land and building property detail")
            .WithDescription("Retrieves a land and building property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
