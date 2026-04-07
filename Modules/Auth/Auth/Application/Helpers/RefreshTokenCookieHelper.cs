using Microsoft.AspNetCore.Http;

namespace Auth.Application.Helpers;

public static class RefreshTokenCookieHelper
{
    private const string CookieName = "__Secure-refresh_token";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    public static void SetRefreshTokenCookie(HttpContext httpContext, string refreshToken)
    {
        httpContext.Response.Cookies.Append(CookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/auth",
            MaxAge = MaxAge
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
