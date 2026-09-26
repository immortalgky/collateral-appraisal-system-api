namespace Integration.Domain.WebhookSubscriptions;

/// <summary>
/// Append-only record of every time an admin decrypted a webhook secret on screen. Holds who, when,
/// from where and which field — never the value.
/// </summary>
public class WebhookSecretRevealLog
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }

    /// <summary><see cref="WebhookSecretField"/> value.</summary>
    public string Field { get; private set; } = default!;

    /// <summary>User code (AspNetUsers.UserName) of the admin who revealed it.</summary>
    public string RevealedBy { get; private set; } = default!;

    public DateTime RevealedAt { get; private set; }
    public string? IpAddress { get; private set; }

    private WebhookSecretRevealLog()
    {
    }

    public static WebhookSecretRevealLog Create(
        Guid subscriptionId, string field, string revealedBy, DateTime revealedAt, string? ipAddress) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SubscriptionId = subscriptionId,
            Field = field,
            RevealedBy = revealedBy,
            RevealedAt = revealedAt,
            IpAddress = ipAddress
        };
}

/// <summary>The two secret columns of <see cref="WebhookSubscription"/>.</summary>
public static class WebhookSecretField
{
    public const string SecretKey = "SecretKey";
    public const string ClientSecret = "ClientSecret";
}
