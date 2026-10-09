using Shared.Security;

namespace Integration.Application.Features.WebhookSubscriptions;

/// <summary>
/// Admin projection of a webhook subscription. Secrets (HMAC <c>SecretKey</c>, TokenBearer
/// <c>ClientSecret</c>) are stored encrypted and never returned — only whether one is set. Reading a
/// value takes the separate, audited reveal endpoint.
/// </summary>
public class WebhookSubscriptionDto
{
    public Guid Id { get; set; }
    public string SystemCode { get; set; } = default!;

    /// <summary>Null = catch-all (matches any event for the SystemCode).</summary>
    public string? EventType { get; set; }

    public string CallbackUrl { get; set; } = default!;
    public string HttpMethod { get; set; } = default!;
    public string AuthType { get; set; } = default!;
    public string? TokenEndpoint { get; set; }
    public string? ClientId { get; set; }
    public bool HasSecretKey { get; set; }
    public bool HasClientSecret { get; set; }

    /// <summary>False for a secret saved before encryption — the admin should re-enter it.</summary>
    public bool SecretKeyEncrypted { get; set; }

    public bool ClientSecretEncrypted { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>The one SELECT list for <see cref="WebhookSubscriptionDto"/>, shared by the list and detail queries.</summary>
internal static class WebhookSubscriptionSql
{
    /// <summary>
    /// Columns from alias <c>s</c>. The encrypted flags compare the ColumnSecretCipher prefix
    /// case-sensitively (BIN2) to agree with SecretProtector.IsProtected (Ordinal) — the database
    /// collation is case-insensitive, so a plaintext secret starting "enc:v1:" must not read as encrypted.
    /// </summary>
    public const string Columns = $"""
        s.Id,
        s.SystemCode,
        s.EventType,
        s.CallbackUrl,
        s.HttpMethod,
        s.AuthType,
        s.TokenEndpoint,
        s.ClientId,
        -- Set = has a character other than space/tab/CR/LF — the SQL side of IsNullOrWhiteSpace, which
        -- is how the domain, the delivery path and the reveal decide a secret is missing.
        CAST(CASE WHEN s.SecretKey LIKE N'%[^ ' + CHAR(9) + CHAR(10) + CHAR(13) + N']%' THEN 1 ELSE 0 END AS bit) AS HasSecretKey,
        CAST(CASE WHEN s.ClientSecret LIKE N'%[^ ' + CHAR(9) + CHAR(10) + CHAR(13) + N']%' THEN 1 ELSE 0 END AS bit) AS HasClientSecret,
        CAST(CASE WHEN s.SecretKey LIKE N'{SecretProtector.Prefix}%' COLLATE Latin1_General_BIN2
                  THEN 1 ELSE 0 END AS bit) AS SecretKeyEncrypted,
        CAST(CASE WHEN s.ClientSecret LIKE N'{SecretProtector.Prefix}%' COLLATE Latin1_General_BIN2
                  THEN 1 ELSE 0 END AS bit) AS ClientSecretEncrypted,
        s.IsActive,
        s.LastDeliveryAt,
        s.CreatedAt
        """;

}
