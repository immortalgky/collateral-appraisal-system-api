using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.OutboxMessages.GetOutboxMessage;

public class GetOutboxMessageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/outbox-messages/{module}/{id:guid}", async (
                string module,
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetOutboxMessageQuery(module, id), cancellationToken);

                return Results.Ok(result);
            })
            .WithName("GetOutboxMessage")
            .WithTags("Admin - Outbox Messages")
            .Produces<OutboxMessageDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("FailedMessageView");
    }
}
