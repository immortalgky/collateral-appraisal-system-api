using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public class CreateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/requests", async (
                CreateRequestRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = request.Adapt<CreateRequestCommand>();

                var requestId = await sender.Send(command, cancellationToken);

                return Results.Created(
                    $"/api/v1/requests/{requestId}",
                    new CreateRequestResponse(requestId)
                );
            })
            .WithName("API - CreateRequest")
            .WithTags("Integration - Appraisal Requests")
            .WithSummary("Creates a new appraisal request with the provided data.")
            .WithDescription(
                "Creates a new appraisal request with the provided data. All fields are required except those marked as optional. Returns the ID of the created request.")
            .Produces<CreateRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}