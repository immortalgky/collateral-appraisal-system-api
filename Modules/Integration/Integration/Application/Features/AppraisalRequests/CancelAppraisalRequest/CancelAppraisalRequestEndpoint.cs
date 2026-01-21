using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalRequests.CancelAppraisalRequest;

public record CancelAppraisalRequestRequest(string? Reason);

public class CancelAppraisalRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/appraisal-requests/{id:guid}/cancel", async (
            Guid id,
            CancelAppraisalRequestRequest? request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CancelAppraisalRequestCommand(id, request?.Reason);
            await sender.Send(command, cancellationToken);

            return Results.NoContent();
        })
        .WithName("CancelAppraisalRequest")
        .WithTags("Integration - Appraisal Requests")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
