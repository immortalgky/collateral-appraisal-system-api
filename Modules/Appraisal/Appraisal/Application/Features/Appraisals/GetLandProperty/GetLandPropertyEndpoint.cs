using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetLandProperty;

public class GetLandPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/land-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetLandPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetLandPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetLandProperty")
            .Produces<GetLandPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get land property detail")
            .WithDescription("Retrieves a land property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
