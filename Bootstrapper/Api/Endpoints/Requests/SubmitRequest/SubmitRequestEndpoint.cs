using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Request.Application.Features.Requests.SubmitRequest;

namespace Api.Endpoints.Requests.SubmitRequest;

public class SubmitRequestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/requests/{id:guid}/submit",
                async (Guid id, SubmitRequestRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new SubmitRequestCommand(id);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<SubmitRequestResponse>();
                    return Results.Ok(response);
                })
            .WithName("SubmitRequest")
            .Produces<SubmitRequestResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Requests")
            .WithSummary("Submit a request")
            .WithDescription("Submits a request for processing.")
            .AllowAnonymous();
    }
}
