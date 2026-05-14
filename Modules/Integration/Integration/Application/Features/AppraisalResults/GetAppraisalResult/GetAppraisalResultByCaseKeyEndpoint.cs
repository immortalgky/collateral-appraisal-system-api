using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByCaseKeyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals/result", async (
            [Microsoft.AspNetCore.Mvc.FromQuery] string externalCaseKey,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(externalCaseKey))
                return Results.BadRequest("externalCaseKey is required");

            var result = await sender.Send(
                new GetAppraisalResultsByCaseKeyQuery(externalCaseKey),
                cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetAppraisalResultByCaseKey")
        .WithTags("Integration - Appraisal Results")
        .RequireAuthorization("Integration")
        .Produces<IReadOnlyList<GetAppraisalResultResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
