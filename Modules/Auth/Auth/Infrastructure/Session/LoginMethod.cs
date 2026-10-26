using Microsoft.AspNetCore.WebUtilities;

namespace Auth.Infrastructure.Session;

/// <summary>
/// How a browser signs in: the <c>method</c> the SPA asks for (<c>/auth/login?method=</c>), and the one it used last.
/// Untrusted input that ends up in a redirect, so only the two known values pass.
/// </summary>
public static class LoginMethod
{
    public const string Parameter = "method";
    public const string Passkey = "passkey";
    public const string Password = "password";

    /// <summary>The cookie the SPA reads to sort its sign-in buttons. Same attributes as <c>cas-lang</c>.</summary>
    public const string CookieName = "cas-login-method";

    /// <summary>The value when it is one of the two methods (any case), else null.</summary>
    public static string? Sanitize(string? value)
    {
        var method = value?.Trim().ToLowerInvariant();
        return method is Passkey or Password ? method : null;
    }

    /// <summary>Reads the method back from the pending /connect/authorize URL the login page is given as its return URL.</summary>
    public static string? FromAuthorizeUrl(string? authorizeUrl)
    {
        var at = authorizeUrl?.IndexOf('?') ?? -1;
        if (at < 0) return null;
        var values = QueryHelpers.ParseQuery(authorizeUrl![at..]).GetValueOrDefault(Parameter);
        return values.Count == 1 ? Sanitize(values[0]) : null;
    }

    /// <summary>
    /// The pending authorize URL with its <c>method</c> set to <paramref name="method"/> (added if absent), every other
    /// parameter as it was. The IdP's pages pass this URL on from one to the next, so changing it once is how a choice
    /// ("use a password instead") holds for all of them.
    /// </summary>
    public static string WithMethod(string authorizeUrl, string method)
    {
        var at = authorizeUrl.IndexOf('?');
        if (at < 0) return QueryHelpers.AddQueryString(authorizeUrl, Parameter, method);

        var query = QueryHelpers.ParseQuery(authorizeUrl[at..]);
        query.Remove(Parameter);
        var parameters = query.SelectMany(entry => entry.Value.Select(value => KeyValuePair.Create(entry.Key, value)))
            .Append(KeyValuePair.Create<string, string?>(Parameter, method));
        return QueryHelpers.AddQueryString(authorizeUrl[..at], parameters);
    }
}
