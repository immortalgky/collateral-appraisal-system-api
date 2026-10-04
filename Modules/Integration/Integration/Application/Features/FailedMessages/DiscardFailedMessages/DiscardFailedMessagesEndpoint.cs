using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.FailedMessages.DiscardFailedMessages;

public record DiscardFailedMessagesRequest(IReadOnlyList<Guid> Ids, string Reason);

public class DiscardFailedMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/failed-messages/discard", async (
                DiscardFailedMessagesRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DiscardFailedMessagesCommand(
                    request.Ids, request.Reason, httpContext.Connection.RemoteIpAddress?.ToString());

                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("DiscardFailedMessages")
            .WithTags("Admin - Failed Messages")
            .Produces<FailedMessageActionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("FailedMessageManage");
    }
}
