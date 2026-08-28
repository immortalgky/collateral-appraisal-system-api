using Microsoft.AspNetCore.Http;

namespace Auth.Application.Helpers;

public static class RefreshTokenCookieHelper
{
    private const string CookieName = "__Secure-refresh_token";

    /// <summary>
    /// Writes the refresh token as a SESSION cookie — deliberately no MaxAge/Expires.
    /// </summary>
    /// <remarks>
    /// Do NOT add MaxAge or Expires here. A persistent cookie is written to disk and survives a
    /// browser restart, which let the SPA silently re-authenticate on the next launch via
    /// POST /auth/refresh. Without one the browser keeps the cookie in memory only: a page reload
    /// (F5) or a new tab still refreshes successfully, but closing the browser drops it and forces
    /// an interactive login.
    ///
    /// The effective session length is owned by the server instead — see
    /// <c>SetRefreshTokenLifetime</c> in AuthModule. Duplicating it here as a cookie MaxAge only
    /// created two values that silently drifted apart.
    /// </remarks>
    public static void SetRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        httpContext.Response.Cookies.Append(CookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/auth"
        });
    }

    public static void ClearRefreshTokenCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/auth"
        });
    }

    public static string? GetRefreshTokenFromCookie(HttpContext httpContext)
    {
        return httpContext.Request.Cookies[CookieName];
    }
}
