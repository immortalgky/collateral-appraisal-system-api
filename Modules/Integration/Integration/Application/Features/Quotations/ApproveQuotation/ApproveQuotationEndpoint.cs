using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.Quotations.ApproveQuotation;

public record ApproveQuotationRequest(string? ApprovalReason);

public class ApproveQuotationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/quotations/{id:guid}/approve", async (
            Guid id,
            ApproveQuotationRequest? request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ApproveQuotationCommand(id, request?.ApprovalReason);
            await sender.Send(command, cancellationToken);

            return Results.NoContent();
        })
        .WithName("ApproveQuotation")
        .WithTags("Integration - Quotations")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
