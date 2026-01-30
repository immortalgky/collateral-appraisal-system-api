using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

public class GetPropertyGroupByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId}/property-groups/{groupId}",
                async (
                    Guid appraisalId,
                    Guid groupId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPropertyGroupByIdQuery(appraisalId, groupId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPropertyGroupByIdResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPropertyGroupById")
            .Produces<GetPropertyGroupByIdResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get property group by ID")
            .WithDescription("Get a specific property group with all its properties.")
            .WithTags("Appraisal");
    }
}
