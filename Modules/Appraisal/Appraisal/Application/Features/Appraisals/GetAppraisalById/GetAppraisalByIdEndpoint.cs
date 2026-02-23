using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

public class GetAppraisalByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalByIdQuery(id);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetAppraisalByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetAppraisalById")
            .Produces<GetAppraisalByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get appraisal by ID")
            .WithDescription("Retrieves a single appraisal by its ID.")
            .WithTags("Appraisal");
    }
}