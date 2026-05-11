using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;
using Shared.Time;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Integration.Application.Services;

public class WebhookService(
    IWebhookSubscriptionRepository subscriptionRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger,
    IDateTimeProvider dateTimeProvider
) : IWebhookService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SendAsync(
        Guid eventId,
        string systemCode,
        string eventType,
        string externalCaseKey,
        DateTime occurredAt,
        object data,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetBySystemCodeAsync(systemCode, cancellationToken);

        if (subscription is null || !subscription.IsActive)
        {
            logger.LogWarning("No active webhook subscription found for system {SystemCode}", systemCode);
            return;
        }

        var envelope = new
        {
            eventId,
            eventType,
            occurredAt = occurredAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
            externalCaseKey,
            data
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, _jsonOptions);
        var delivery = WebhookDelivery.Create(subscription.Id, eventType, envelopeJson);

        await deliveryRepository.AddAsync(delivery, cancellationToken);
        await deliveryRepository.SaveChangesAsync(cancellationToken);

        await DeliverWebhookAsync(subscription, delivery, cancellationToken);
    }

    public async Task ResendAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await deliveryRepository.GetByIdAsync(deliveryId, cancellationToken);
        if (delivery is null)
            throw new NotFoundException(nameof(WebhookDelivery), deliveryId);

        if (delivery.Status != DeliveryStatus.Failed)
            throw new BadRequestException(
                $"Only failed deliveries can be retried. Current status: {delivery.Status}.");

        var subscription = await subscriptionRepository.GetByIdAsync(delivery.SubscriptionId, cancellationToken);
        if (subscription is null || !subscription.IsActive)
            throw new BadRequestException(
                "Cannot retry delivery: the associated webhook subscription is inactive or missing.");

        delivery.BeginRetry();
        await deliveryRepository.SaveChangesAsync(cancellationToken);

        await DeliverWebhookAsync(subscription, delivery, cancellationToken);
    }

    private async Task DeliverWebhookAsync(
        WebhookSubscription subscription,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        var unixTimestamp = ((DateTimeOffset)dateTimeProvider.ApplicationNow.ToUniversalTime()).ToUnixTimeSeconds();
        var signedPayload = $"{unixTimestamp}.{delivery.Payload}";
        var signature = GenerateSignature(signedPayload, subscription.SecretKey);

        var request = new HttpRequestMessage(HttpMethod.Post, subscription.CallbackUrl)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Timestamp", unixTimestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("X-Signature", $"sha256={signature}");
        request.Headers.Add("X-Event-Type", delivery.EventType);

        try
        {
            var client = httpClientFactory.CreateClient("Webhook");

            // The resilience pipeline retries internally; when SendAsync returns we have the final outcome.
            // WebhookAttemptCounterHandler increments the count on each wire attempt.
            var response = await client.SendAsync(request, cancellationToken);

            var attempts = ReadAttemptCount(request);
            subscription.RecordDelivery(dateTimeProvider.ApplicationNow);

            if (response.IsSuccessStatusCode)
            {
                delivery.RecordSuccess((int)response.StatusCode, attempts, dateTimeProvider.ApplicationNow);

                await PersistDeliveryAsync(delivery, cancellationToken);
                await subscriptionRepository.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Webhook delivered successfully to {Url} for event {EventType}",
                    subscription.CallbackUrl,
                    delivery.EventType);
            }
            else
            {
                delivery.RecordFailure((int)response.StatusCode, attempts, null);

                await PersistDeliveryAsync(delivery, cancellationToken);
                await subscriptionRepository.SaveChangesAsync(cancellationToken);

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

            delivery.RecordFailure(0, ReadAttemptCount(request), ex.Message);
            await PersistDeliveryAsync(delivery, cancellationToken);
        }
    }

    private static int ReadAttemptCount(HttpRequestMessage request)
    {
        request.Options.TryGetValue(WebhookAttemptCounterHandler.AttemptCountKey, out var attempts);
        return attempts;
    }

    // Guards against permanently stranding the row at Status='Pending'.
    // Without an out-of-process retry drainer, a thrown SaveChangesAsync would otherwise be invisible.
    private async Task PersistDeliveryAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        try
        {
            await deliveryRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveEx)
        {
            logger.LogCritical(saveEx,
                "Failed to persist webhook delivery outcome for delivery {DeliveryId} (Status={Status}, AttemptCount={AttemptCount}). Row may remain stuck in its prior state.",
                delivery.Id,
                delivery.Status,
                delivery.AttemptCount);
            throw;
        }
    }

    private static string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
