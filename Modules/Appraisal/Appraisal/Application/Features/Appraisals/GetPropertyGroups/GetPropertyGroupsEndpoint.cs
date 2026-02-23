using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroups;

public class GetPropertyGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{appraisalId}/property-groups",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetPropertyGroupsQuery(appraisalId);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetPropertyGroupsResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetPropertyGroups")
            .Produces<GetPropertyGroupsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get property groups")
            .WithDescription("Get all property groups for an appraisal.")
            .WithTags("Appraisal");
    }
}
