namespace Integration.Application.Services;

public interface IWebhookService
{
    /// <summary>
    /// Sends a webhook delivery. When <paramref name="wrapInEnvelope"/> is true (default),
    /// <paramref name="data"/> is nested under the standard CAS envelope
    /// (<c>{ eventId, eventType, occurredAt, externalCaseKey, data }</c>) and routes to the
    /// SystemCode's catch-all subscription if no exact (SystemCode, EventType) match exists. Set to
    /// false when the receiving system needs its own fields at the top level (e.g. LOS) — in that
    /// case <paramref name="data"/> is serialized directly as the delivery payload with a cleared
    /// field sent as an explicit JSON <c>null</c> (not omitted), and the lookup requires an EXACT
    /// (SystemCode, EventType) subscription match — it will NOT fall back to the catch-all, since
    /// that row has the wrong shape/auth/method for a bare payload.
    /// </summary>
    Task<WebhookDeliveryOutcome> SendAsync(
        Guid eventId,
        string systemCode,
        string eventType,
        string externalCaseKey,
        DateTime occurredAt,
        object data,
        CancellationToken cancellationToken = default,
        bool wrapInEnvelope = true);

    Task ResendAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a single webhook delivery attempt. Existing fire-and-forget callers (CREATED /
/// STATUS_CHANGED consumers) ignore the return value; the PMA consumer uses it to aggregate a
/// per-property Delivered/Failed status once all of a property's title deliveries are attempted.
/// </summary>
public sealed record WebhookDeliveryOutcome(bool Success, string? Error);
