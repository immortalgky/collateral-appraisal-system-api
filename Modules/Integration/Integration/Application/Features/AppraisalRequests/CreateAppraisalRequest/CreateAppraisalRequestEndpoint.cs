using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalRequests.CreateAppraisalRequest;

public record CreateAppraisalRequestRequest(
    string ExternalCaseKey,
    string Purpose,
    string Channel,
    string Priority,
    AppraisalRequestLoanDetail? LoanDetail,
    AppraisalRequestContact? Contact,
    List<AppraisalRequestCustomer>? Customers,
    List<AppraisalRequestProperty>? Properties,
    List<Guid>? DocumentIds
);

public record CreateAppraisalRequestResponse(
    Guid RequestId,
    string? RequestNumber
);

public class CreateAppraisalRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/appraisal-requests", async (
            CreateAppraisalRequestRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateAppraisalRequestCommand(
                request.ExternalCaseKey,
                request.Purpose,
                request.Channel,
                request.Priority,
                request.LoanDetail,
                request.Contact,
                request.Customers,
                request.Properties,
                request.DocumentIds
            );

            var result = await sender.Send(command, cancellationToken);

            return Results.Created(
                $"/api/v1/appraisal-requests/{result.RequestId}",
                new CreateAppraisalRequestResponse(result.RequestId, result.RequestNumber)
            );
        })
        .WithName("CreateAppraisalRequest")
        .WithTags("Integration - Appraisal Requests")
        .Produces<CreateAppraisalRequestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
