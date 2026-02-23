using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetVesselProperty;

public class GetVesselPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/vessel-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetVesselPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetVesselPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetVesselProperty")
            .Produces<GetVesselPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get vessel property detail")
            .WithDescription("Retrieves a vessel property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
