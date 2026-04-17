using Carter;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalViews;

public class GetAppraisalViewsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/views",
                async (ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetAppraisalViewsQuery(), cancellationToken);
                    return Results.Ok(result);
                }
            )
            .WithName("GetAppraisalViews")
            .Produces<GetAppraisalViewsResult>()
            .WithSummary("Get smart view presets")
            .WithDescription(
                "Returns pre-built filter combinations for the appraisals list. " +
                "Each view's Filters dictionary contains query parameter names that can be " +
                "applied directly to GET /appraisals. User-specific views (My Assignments, " +
                "My Company Queue) populate filter values from the current user's identity claims.")
            .WithTags("Appraisal");
    }
}
