using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.RevealWebhookSecret;

public record RevealWebhookSecretRequest(string Field);

public class RevealWebhookSecretEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST, not GET: the response carries a plaintext secret and must never land in a cache,
        // browser history or an access log's query string.
        app.MapPost("/webhook-subscriptions/{id:guid}/secret/reveal", async (
                Guid id,
                RevealWebhookSecretRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RevealWebhookSecretCommand(
                        id, request.Field, httpContext.Connection.RemoteIpAddress?.ToString()),
                    cancellationToken);

                httpContext.Response.Headers.CacheControl = "no-store";
                return Results.Ok(result);
            })
            .WithName("RevealWebhookSecret")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces<RevealWebhookSecretResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("WebhookSubscriptionsManage")
            .RequireAuthorization("WebhookSecretReveal");
    }
}
