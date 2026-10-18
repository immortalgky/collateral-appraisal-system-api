using Appraisal.Application.Features.Appraisals.GetAppraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalGroups;

public class GetAppraisalGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/groups",
                async (
                    [FromQuery] string? groupBy,
                    [AsParameters] AppraisalListQueryParams queryParams,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new GetAppraisalGroupsQuery(groupBy, queryParams.ToFilterRequest()), cancellationToken);

                    return Results.Ok(result);
                }
            )
            .WithName("GetAppraisalGroups")
            .Produces<GetAppraisalGroupsResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Count appraisals by group")
            .WithDescription(
                "Counts the appraisals GET /appraisals would return for the same filters, grouped by " +
                "?groupBy=status|slaStatus|priority|province|assignmentType (anything else is a 400). " +
                "Takes exactly the list's filter and search parameters; pagination and sort are ignored. " +
                "Each group carries its Breached and AtRisk counts (SLAStatus). Ordered by count descending, " +
                "then key. A null or empty stored value is the group with key \"\".")
            .WithTags("Appraisal");
    }
}
