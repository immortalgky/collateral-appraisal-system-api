using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetBuildingProperty;

public class GetBuildingPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/building-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetBuildingPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetBuildingPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetBuildingProperty")
            .Produces<GetBuildingPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get building property detail")
            .WithDescription("Retrieves a building property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
