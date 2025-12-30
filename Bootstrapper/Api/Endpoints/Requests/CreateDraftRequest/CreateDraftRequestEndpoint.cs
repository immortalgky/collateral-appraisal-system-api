using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.CreateDraftRequest;

namespace Api.Endpoints.Requests.CreateDraftRequest;

public class CreateDraftRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests/draft",
                async (CreateDraftRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateDraftRequestCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateDraftRequestResponse>();
                    return Results.Created($"/requests/{response.Id}", response);
                })
            .WithName("CreateDraftRequest")
            .Produces<CreateDraftRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Requests")
            .WithSummary("Create a draft request")
            .WithDescription("Creates a draft request that can be completed later.")
            .AllowAnonymous();
    }
}
