using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Appraisals.GetAppraisalStatus;

public class GetAppraisalStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals/{appraisalNumber}/status", async (
            string appraisalNumber,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(appraisalNumber))
                return Results.BadRequest("appraisalNumber is required");

            var result = await sender.Send(
                new GetAppraisalStatusQuery(appraisalNumber), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        })
        .WithName("GetAppraisalStatus")
        .WithTags("Integration - Appraisals")
        .Produces<GetAppraisalStatusResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("Integration");
    }
}
