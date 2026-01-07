using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetCondoProperty;

public class GetCondoPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/condo-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCondoPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetCondoPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCondoProperty")
            .Produces<GetCondoPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get condo property detail")
            .WithDescription("Retrieves a condo property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
