using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.FailedMessages.RetryFailedMessages;

public record RetryFailedMessagesRequest(IReadOnlyList<Guid> Ids, string? Reason);

public class RetryFailedMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/failed-messages/retry", async (
                RetryFailedMessagesRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new RetryFailedMessagesCommand(
                    request.Ids, request.Reason, httpContext.Connection.RemoteIpAddress?.ToString());

                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("RetryFailedMessages")
            .WithTags("Admin - Failed Messages")
            .Produces<FailedMessageActionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization("FailedMessageManage");
    }
}
