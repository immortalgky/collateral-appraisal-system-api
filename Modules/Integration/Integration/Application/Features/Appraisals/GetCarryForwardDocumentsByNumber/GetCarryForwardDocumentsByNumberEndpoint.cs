using Appraisal.Contracts.Appraisals;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Appraisals.GetCarryForwardDocumentsByNumber;

public class GetCarryForwardDocumentsByNumberEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisals/{appraisalNumber}/carry-forward-documents", async (
            string appraisalNumber,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetCarryForwardDocumentsByNumberQuery(appraisalNumber), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        })
        .WithName("GetCarryForwardDocumentsIntegration")
        .WithTags("Integration - Appraisals")
        .Produces<CarryForwardDocumentsResult>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization("Integration");
    }
}
