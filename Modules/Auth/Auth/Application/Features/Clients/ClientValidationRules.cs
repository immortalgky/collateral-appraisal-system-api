using FluentValidation;

namespace Auth.Application.Features.Clients;

/// <summary>
/// Validation predicates shared by the register and update client commands so the two paths
/// can never diverge.
/// </summary>
public static class ClientValidationRules
{
    public static bool IsKnownGrantType(string grantType) =>
        ClientPermissionMapper.AllGrantTypes.Contains(grantType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Redirect/post-logout URIs must be absolute http(s) — rejects relative and javascript:/data: schemes.</summary>
    public static bool IsAbsoluteHttpUri(Uri uri) =>
        uri is { IsAbsoluteUri: true }
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Bounds one per-client token lifetime. Null is valid and means "fall back to the server
    /// default" — callers also gate on HasValue, but the predicate stands on its own so a future
    /// caller that forgets cannot accidentally reject it. Rejecting out-of-range values here
    /// matters because OpenIddict ignores a setting it cannot use and silently falls back, so an
    /// unchecked value would look saved while changing nothing.
    /// </summary>
    public static bool IsValidLifetime(int? minutes, ClientPermissionMapper.TokenLifetimeKind kind) =>
        minutes is not { } value || (value >= kind.MinMinutes && value <= kind.MaxMinutes);

    private static string LifetimeMessage(string label, ClientPermissionMapper.TokenLifetimeKind kind) =>
        $"{label} must be between {kind.MinMinutes} and {kind.MaxMinutes} minutes, or empty to use the server default.";

    /// <summary>
    /// Bounds one lifetime field. An extension rather than three copied rule blocks per validator:
    /// register and update need the identical set, and copying them left six near-identical blocks
    /// that read as noise and drift independently.
    /// </summary>
    public static IRuleBuilderOptions<T, int?> ValidTokenLifetime<T>(
        this IRuleBuilder<T, int?> rule,
        ClientPermissionMapper.TokenLifetimeKind kind,
        string label) =>
        rule.Must(minutes => IsValidLifetime(minutes, kind)).WithMessage(LifetimeMessage(label, kind));
}
