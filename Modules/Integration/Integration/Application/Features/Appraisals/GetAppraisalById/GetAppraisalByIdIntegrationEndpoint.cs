using Appraisal.Application.Features.Appraisals.GetAppraisalById;
using Carter;
using Microsoft.Extensions.Options;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// GET /api/v1/appraisals/{appraisalId}?include=request,documents - LOS's way to read an appraisal's header and,
/// opt in, its request data and files (e.g. the previous appraisal when it sends a new request). The same query,
/// response and rules as the Appraisal module's GET /appraisals/{id}; only the route and the policy differ, and the
/// /api/v1 prefix is what puts it in the integration request log.
///
/// An Integration client token is not a user: it carries no company, so no company scope applies, and it holds none
/// of the APPRAISAL_VIEW / tracking permissions, so it gets the download ids, paths and the appraised value (also in
/// the plain header, <c>StrictRelease</c>) once the appraisal is Completed - the case for a previous appraisal - and
/// not while it is in progress.
/// </summary>
public class GetAppraisalByIdIntegrationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals/{appraisalId:guid}", async (
            Guid appraisalId,
            [FromQuery] string? include,
            ISender sender,
            IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
            CancellationToken cancellationToken) =>
        {
            var parts = AppraisalIncludeParser.Parse(include);
            var result = await sender.Send(
                new GetAppraisalByIdQuery(appraisalId, parts, StrictRelease: true), cancellationToken);

            return Results.Ok(AppraisalByIdResponseWriter.Build(result, parts, json.Value.SerializerOptions));
        })
        .WithName("GetAppraisalByIdIntegration")
        .WithTags("Integration - Appraisals")
        .Produces<GetAppraisalByIdResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("Integration");
    }
}
