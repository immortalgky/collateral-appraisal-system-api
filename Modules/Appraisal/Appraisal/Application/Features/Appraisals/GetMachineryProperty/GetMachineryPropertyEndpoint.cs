using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetMachineryProperty;

public class GetMachineryPropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/machinery-detail",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetMachineryPropertyQuery(appraisalId, propertyId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetMachineryPropertyResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetMachineryProperty")
            .Produces<GetMachineryPropertyResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get machinery property detail")
            .WithDescription("Retrieves a machinery property with its detail by property ID.")
            .WithTags("Appraisal Properties");
    }
}
