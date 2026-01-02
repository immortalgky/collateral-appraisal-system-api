using Mapster;
using MediatR;
using Request.Application.Features.Requests.CreateRequest;

namespace Api.Endpoints.Requests.CreateRequest;

public class CreateRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/requests",
                async (CreateRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateRequestCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<CreateRequestResponse>();
                    return Results.Created($"/requests/{response.Id}", response);
                })
            .WithName("CreateRequest")
            .Produces<CreateRequestResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Requests")
            .WithSummary("Create a new request")
            .WithDescription("Creates a new request in the system.")
            .AllowAnonymous();
    }
}