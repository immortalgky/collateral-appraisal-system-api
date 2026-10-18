using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.OutboxMessages.ResendOutboxMessages;

public record ResendOutboxMessagesRequest(IReadOnlyList<OutboxMessageRef> Items, string? Reason);

public class ResendOutboxMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/outbox-messages/resend", async (
                ResendOutboxMessagesRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new ResendOutboxMessagesCommand(
                    request.Items, request.Reason, httpContext.Connection.RemoteIpAddress?.ToString());

                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("ResendOutboxMessages")
            .WithTags("Admin - Outbox Messages")
            .Produces<OutboxResendResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("FailedMessageManage");
    }
}
