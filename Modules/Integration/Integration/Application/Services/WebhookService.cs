using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Integration.Application.Services;

public class WebhookService(
    IWebhookSubscriptionRepository subscriptionRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger
) : IWebhookService
{
    public async Task SendAsync(
        string systemCode,
        string eventType,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetBySystemCodeAsync(systemCode, cancellationToken);

        if (subscription is null || !subscription.IsActive)
        {
            logger.LogWarning("No active webhook subscription found for system {SystemCode}", systemCode);
            return;
        }

        var payloadJson = JsonSerializer.Serialize(payload);
        var delivery = WebhookDelivery.Create(subscription.Id, eventType, payloadJson);

        await deliveryRepository.AddAsync(delivery, cancellationToken);
        await deliveryRepository.SaveChangesAsync(cancellationToken);

        await DeliverWebhookAsync(subscription, delivery, cancellationToken);
    }

    private async Task DeliverWebhookAsync(
        WebhookSubscription subscription,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Webhook");

            var signature = GenerateSignature(delivery.Payload, subscription.SecretKey);

            var request = new HttpRequestMessage(HttpMethod.Post, subscription.CallbackUrl)
            {
                Content = JsonContent.Create(new
                {
                    eventType = delivery.EventType,
                    payload = JsonSerializer.Deserialize<object>(delivery.Payload),
                    timestamp = DateTime.UtcNow
                })
            };

            request.Headers.Add("X-Signature", signature);
            request.Headers.Add("X-Event-Type", delivery.EventType);

            var response = await client.SendAsync(request, cancellationToken);

            delivery.RecordAttempt((int)response.StatusCode);
            subscription.RecordDelivery(DateTime.UtcNow);

            await deliveryRepository.SaveChangesAsync(cancellationToken);
            await subscriptionRepository.SaveChangesAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Webhook delivered successfully to {Url} for event {EventType}",
                    subscription.CallbackUrl,
                    delivery.EventType);
            }
            else
            {
                logger.LogWarning(
                    "Webhook delivery failed with status {StatusCode} to {Url} for event {EventType}",
                    (int)response.StatusCode,
                    subscription.CallbackUrl,
                    delivery.EventType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception during webhook delivery to {Url} for event {EventType}",
                subscription.CallbackUrl,
                delivery.EventType);

            delivery.RecordAttempt(0, ex.Message);
            await deliveryRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
