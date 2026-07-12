using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;
using Shared.Time;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Integration.Application.Services;

public class WebhookService(
    IWebhookSubscriptionRepository subscriptionRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IHttpClientFactory httpClientFactory,
    IWebhookTokenProvider tokenProvider,
    ILogger<WebhookService> logger,
    IDateTimeProvider dateTimeProvider
) : IWebhookService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Used for wrapInEnvelope=false (e.g. LOS) — the receiving system needs its own fields at the
    // top level with no CAS envelope. No WhenWritingNull here (unlike the envelope options): a
    // legitimately-cleared field (e.g. BuildingInsurance reset to null) must be sent as explicit
    // JSON null so LOS can distinguish "cleared" from "field absent", not omitted. Land/condo
    // no longer share one collateral record (see LosPmaTitleDetails), so there is no cross-type
    // null leakage to guard against either.
    private static readonly JsonSerializerOptions _rawPayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<WebhookDeliveryOutcome> SendAsync(
        Guid eventId,
        string systemCode,
        string eventType,
        string externalCaseKey,
        DateTime occurredAt,
        object data,
        CancellationToken cancellationToken = default,
        bool wrapInEnvelope = true)
    {
        // Bare-payload sends (wrapInEnvelope == false, e.g. LOS PMA) target a dedicated per-event
        // subscription and must NOT fall back to the envelope catch-all — that row has the wrong
        // auth model/callback/method for a bare payload. The 9 envelope consumers (wrapInEnvelope
        // defaults true) keep the catch-all fallback unchanged.
        var subscription = await subscriptionRepository.GetBySubscriptionAsync(
            systemCode, eventType, exactMatchOnly: !wrapInEnvelope, cancellationToken: cancellationToken);

        if (subscription is null || !subscription.IsActive)
        {
            logger.LogWarning(
                "No active webhook subscription found for system {SystemCode} event {EventType}",
                systemCode, eventType);
            return new WebhookDeliveryOutcome(false, $"No active webhook subscription for system '{systemCode}'.");
        }

        string payloadJson;
        if (wrapInEnvelope)
        {
            var envelope = new
            {
                eventId,
                eventType,
                occurredAt = occurredAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
                externalCaseKey,
                data
            };

            payloadJson = JsonSerializer.Serialize(envelope, _jsonOptions);
        }
        else
        {
            // Bare payload — the receiving system's fields go at the top level, no CAS envelope.
            payloadJson = JsonSerializer.Serialize(data, _rawPayloadJsonOptions);
        }

        var delivery = WebhookDelivery.Create(subscription.Id, eventType, payloadJson);

        await deliveryRepository.AddAsync(delivery, cancellationToken);
        await deliveryRepository.SaveChangesAsync(cancellationToken);

        return await DeliverWebhookAsync(subscription, delivery, cancellationToken);
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

    private async Task<WebhookDeliveryOutcome> DeliverWebhookAsync(
        WebhookSubscription subscription,
        WebhookDelivery delivery,
        CancellationToken cancellationToken)
    {
        var httpMethod = new HttpMethod(
            string.IsNullOrWhiteSpace(subscription.HttpMethod) ? "POST" : subscription.HttpMethod);

        var request = new HttpRequestMessage(httpMethod, subscription.CallbackUrl)
        {
            Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Event-Type", delivery.EventType);

        try
        {
            // Auth header setup lives INSIDE the try: a token-fetch failure (bad config, LOS token
            // endpoint down) or a missing HMAC secret must resolve to a normal delivery Failure
            // (caught below, RecordFailure + WebhookDeliveryOutcome(false, ...)) rather than throw
            // out of DeliverWebhookAsync — otherwise the caller (e.g. WebhookDispatchConsumer's
            // per-title loop) would see an unhandled exception instead of a clean failed outcome,
            // and a property could get stuck at "Pending" instead of resolving to Failed.
            if (subscription.AuthType == WebhookAuthType.TokenBearer)
            {
                var (tokenType, accessToken) = await tokenProvider.GetTokenAsync(subscription, cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken);
            }
            else
            {
                if (string.IsNullOrEmpty(subscription.SecretKey))
                    throw new InvalidOperationException(
                        $"HMAC subscription {subscription.Id} ({subscription.SystemCode}) is missing SecretKey.");

                var unixTimestamp = ((DateTimeOffset)dateTimeProvider.ApplicationNow.ToUniversalTime()).ToUnixTimeSeconds();
                var signedPayload = $"{unixTimestamp}.{delivery.Payload}";
                var signature = GenerateSignature(signedPayload, subscription.SecretKey);

                request.Headers.Add("X-Timestamp", unixTimestamp.ToString(CultureInfo.InvariantCulture));
                request.Headers.Add("X-Signature", $"sha256={signature}");
            }

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

                return new WebhookDeliveryOutcome(true, null);
            }
            else
            {
                // A 401 on a token-bearer subscription means the cached token was rejected
                // (expired/revoked) — invalidate it. AddStandardResilienceHandler does NOT retry
                // 401 (it's not a transient status), so this does not trigger an in-attempt retry;
                // it just ensures the NEXT delivery attempt (the following PMA save, or a manual
                // RetryWebhookDelivery) fetches a fresh token instead of reusing the rejected one.
                if (subscription.AuthType == WebhookAuthType.TokenBearer &&
                    response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    tokenProvider.InvalidateToken(subscription.Id);
                    logger.LogWarning(
                        "Webhook token rejected (401) for subscription {SubscriptionId} ({SystemCode}); cached token invalidated",
                        subscription.Id, subscription.SystemCode);
                }

                delivery.RecordFailure((int)response.StatusCode, attempts, null);

                await PersistDeliveryAsync(delivery, cancellationToken);
                await subscriptionRepository.SaveChangesAsync(cancellationToken);

                logger.LogWarning(
                    "Webhook delivery failed with status {StatusCode} to {Url} for event {EventType}",
                    (int)response.StatusCode,
                    subscription.CallbackUrl,
                    delivery.EventType);

                return new WebhookDeliveryOutcome(false, $"HTTP {(int)response.StatusCode}");
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

            return new WebhookDeliveryOutcome(false, ex.Message);
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
