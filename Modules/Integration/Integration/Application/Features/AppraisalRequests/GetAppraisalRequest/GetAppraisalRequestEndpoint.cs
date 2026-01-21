using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalRequests.GetAppraisalRequest;

public record GetAppraisalRequestResponse(
    Guid Id,
    string? RequestNumber,
    string Status,
    string? ExternalCaseKey,
    string? Purpose,
    string? Channel,
    string? Priority,
    DateTime? RequestedAt,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public class GetAppraisalRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/appraisal-requests/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAppraisalRequestQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(new GetAppraisalRequestResponse(
                result.Id,
                result.RequestNumber,
                result.Status,
                result.ExternalCaseKey,
                result.Purpose,
                result.Channel,
                result.Priority,
                result.RequestedAt,
                result.CreatedAt,
                result.CompletedAt
            ));
        })
        .WithName("GetAppraisalRequest")
        .WithTags("Integration - Appraisal Requests")
        .Produces<GetAppraisalRequestResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
