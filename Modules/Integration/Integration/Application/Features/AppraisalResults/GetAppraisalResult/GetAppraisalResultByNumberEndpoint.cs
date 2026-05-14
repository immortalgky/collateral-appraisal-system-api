using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByNumberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals/{appraisalNumber}/result", async (
            string appraisalNumber,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetAppraisalResultByNumberQuery(appraisalNumber),
                cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetAppraisalResultByNumber")
        .WithTags("Integration - Appraisal Results")
        .RequireAuthorization("Integration")
        .Produces<GetAppraisalResultResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
