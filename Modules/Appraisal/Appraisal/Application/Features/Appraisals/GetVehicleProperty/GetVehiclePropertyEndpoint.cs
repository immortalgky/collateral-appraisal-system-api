using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetVehicleProperty;

public class GetVehiclePropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/vehicle-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetVehiclePropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetVehiclePropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetVehicleProperty")
            .Produces<GetVehiclePropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get vehicle property detail")
            .WithDescription("Retrieves a vehicle property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
