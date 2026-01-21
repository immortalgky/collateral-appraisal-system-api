using Carter;
using Integration.Application.Features.AppraisalRequests.CreateAppraisalRequest;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalRequests.UpdateAppraisalRequest;

public record UpdateAppraisalRequestRequest(
    string? Priority,
    AppraisalRequestContact? Contact,
    List<AppraisalRequestCustomer>? Customers,
    List<AppraisalRequestProperty>? Properties
);

public class UpdateAppraisalRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/appraisal-requests/{id:guid}", async (
            Guid id,
            UpdateAppraisalRequestRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateAppraisalRequestCommand(
                id,
                request.Priority,
                request.Contact,
                request.Customers,
                request.Properties
            );

            await sender.Send(command, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateAppraisalRequest")
        .WithTags("Integration - Appraisal Requests")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
