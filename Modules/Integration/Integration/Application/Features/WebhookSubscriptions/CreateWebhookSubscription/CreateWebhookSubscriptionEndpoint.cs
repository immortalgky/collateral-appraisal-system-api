using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Integration.Application.Features.WebhookSubscriptions.CreateWebhookSubscription;

public record CreateWebhookSubscriptionRequest(string SystemCode, string CallbackUrl, string SecretKey);

public class CreateWebhookSubscriptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook-subscriptions", async (
                CreateWebhookSubscriptionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateWebhookSubscriptionCommand(
                    request.SystemCode, request.CallbackUrl, request.SecretKey);
                var result = await sender.Send(command, cancellationToken);
                return Results.Created($"/webhook-subscriptions/{result.Id}", result);
            })
            .WithName("CreateWebhookSubscription")
            .WithTags("Admin - Webhook Subscriptions")
            .Produces<CreateWebhookSubscriptionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization("WebhookSubscriptionsManage");
    }
}
