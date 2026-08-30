using System.Text.Json;

namespace Auth.Domain.Auth.Features.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResult>
{
    /// <summary>
    /// Suppresses the token value in logs. The global <c>LoggingBehavior</c> logs the whole request
    /// object at Information level, and a positional record's generated ToString() would print the
    /// refresh token verbatim. These are reference tokens, so a logged value is a replayable
    /// credential for the remainder of its lifetime to anyone who can read the logs.
    /// </summary>
    public override string ToString() => $"{nameof(RefreshTokenCommand)} {{ RefreshToken = *** }}";
}

public record RefreshTokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope,
    string IdToken,
    string RefreshToken);

/// <summary>
/// The token endpoint refused the grant: the refresh token is expired, revoked, or was already
/// rotated. This is the ordinary end of a session rather than a fault, so callers translate it to
/// 401 instead of letting it reach the global exception handler — which would log an error with a
/// stack trace and answer 500 for something that happens to every user on every idle timeout.
/// </summary>
public class InvalidRefreshTokenException(string message) : Exception(message);

public class RefreshTokenHandler(
    IHttpClientFactory clientFactory,
    ILogger<RefreshTokenHandler> logger) : ICommandHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient("CAS");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "spa" },
            { "refresh_token", request.RefreshToken }
        });

        var response = await client.PostAsync("/connect/token", content, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return JsonSerializer.Deserialize<RefreshTokenResult>(responseContent,
                       new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
                   ?? throw new Exception("Failed to deserialize refresh token response.");

        // Only invalid_grant means "this session is over". Matching on the status code alone would
        // sweep in unauthorized_client / invalid_client (someone edited the `spa` client and dropped
        // the refresh_token grant) and 429/408 (transient) — turning an estate-wide outage into a
        // silent, unlogged redirect to the login screen for every user. Everything else stays an
        // unhandled exception so it is logged and surfaced as 500.
        var oauthError = TryReadOAuthError(responseContent);
        if (oauthError == OpenIddictConstants.Errors.InvalidGrant)
            throw new InvalidRefreshTokenException("Refresh token rejected: invalid_grant.");

        logger.LogWarning(
            "Refresh token request failed with status {StatusCode} and error {OAuthError}. This is not " +
            "an expired session — check the `spa` client configuration and the token endpoint.",
            (int)response.StatusCode, oauthError ?? "(none)");

        throw new Exception($"Refresh token request failed with status {response.StatusCode}");
    }

    /// <summary>
    /// Reads the `error` field out of an RFC 6749 error response. Returns null when the body is not
    /// JSON or carries no error — callers treat that as "not a session-end signal".
    /// </summary>
    private static string? TryReadOAuthError(string responseContent)
    {
        try
        {
            using var document = JsonDocument.Parse(responseContent);
            return document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
