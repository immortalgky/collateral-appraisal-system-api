using Microsoft.AspNetCore.WebUtilities;

namespace Auth.Infrastructure.Session;

/// <summary>
/// The OIDC <c>login_hint</c> the SPA's "Continue as ..." button sends, so the sign-in page can show the username
/// already filled in. It is untrusted input that ends up in a redirect and in the page, so only a plausible
/// username passes; anything else is dropped without an error.
/// </summary>
public static class LoginHint
{
    public const string Parameter = "login_hint";
    private const int MaxLength = 256;

    /// <param name="allowedChars">Identity's <c>User.AllowedUserNameCharacters</c>: a hint no account can have is no hint.
    /// Null or empty means "any" to Identity, so only control characters are refused then.</param>
    public static string? Sanitize(string? value, string? allowedChars)
    {
        var hint = value?.Trim();
        return hint is { Length: > 0 and <= MaxLength }
               && hint.All(c => !char.IsControl(c) && (string.IsNullOrEmpty(allowedChars) || allowedChars.Contains(c)))
            ? hint
            : null;
    }

    /// <summary>Reads the hint back from the pending /connect/authorize URL the login page is given as its return URL.</summary>
    public static string? FromAuthorizeUrl(string? authorizeUrl, string? allowedChars)
    {
        var at = authorizeUrl?.IndexOf('?') ?? -1;
        if (at < 0) return null;
        var values = QueryHelpers.ParseQuery(authorizeUrl![at..]).GetValueOrDefault(Parameter);
        return values.Count == 1 ? Sanitize(values[0], allowedChars) : null;
    }
}
