namespace Integration.Application.Features.WebhookSubscriptions;

/// <summary>
/// Admin projection of a webhook subscription. The HMAC <c>SecretKey</c> is a shared secret with the
/// receiving system and is never returned in full — only the last four characters, for identification.
/// </summary>
public class WebhookSubscriptionDto
{
    public Guid Id { get; set; }
    public string SystemCode { get; set; } = default!;

    /// <summary>Null = catch-all (matches any event for the SystemCode).</summary>
    public string? EventType { get; set; }

    public string CallbackUrl { get; set; } = default!;
    public bool IsActive { get; set; }
    public string? SecretLast4 { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
