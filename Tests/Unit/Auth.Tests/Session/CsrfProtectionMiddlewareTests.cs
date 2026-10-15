using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Identity;

namespace Auth.Tests.Session;

public class CsrfProtectionMiddlewareTests
{
    private const string Spa = "https://spa.example.test:3000";
    private const string Api = "https://api.example.test:7111";
    private static readonly BrowserSessionOptions Options = new();

    private static (CsrfProtectionMiddleware Middleware, Func<bool> Reached) Create(bool allowDevBypass = false)
    {
        var reached = false;
        var middleware = new CsrfProtectionMiddleware(
            _ =>
            {
                reached = true;
                return Task.CompletedTask;
            },
            new BffUrls(Api, [Spa]),
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<CsrfProtectionMiddleware>.Instance,
            allowDevBypass);
        return (middleware, () => reached);
    }

    private static DefaultHttpContext Request(string method, string path = "/requests", bool cookie = true,
        string? origin = null)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        context.Request.Method = method;
        context.Request.Path = path;
        if (cookie) context.Request.Headers.Cookie = $"{Options.AppCookieName}=abc";
        if (origin is not null) context.Request.Headers.Origin = origin;
        return context;
    }

    private static async Task<(bool Reached, int Status, string Body)> Send(
        string method, string path = "/requests", bool cookie = true, string? origin = null,
        Action<HttpContext>? configure = null, bool allowDevBypass = false)
    {
        var (middleware, reached) = Create(allowDevBypass);
        var context = Request(method, path, cookie, origin);
        configure?.Invoke(context);
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        return (reached(), context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Theory]
    [InlineData(Spa)]
    [InlineData(Api)] // the API's own pages (Razor login form, Hangfire dashboard)
    public async Task Unsafe_request_from_a_trusted_origin_passes_without_any_custom_header(string origin)
    {
        foreach (var method in new[] { "POST", "PUT", "PATCH", "DELETE" })
            Assert.True((await Send(method, origin: origin)).Reached, method);
    }

    [Fact]
    public async Task Referer_stands_in_when_the_origin_header_is_absent() =>
        Assert.True((await Send("DELETE", configure: c => c.Request.Headers.Referer = $"{Spa}/appraisal/1")).Reached);

    [Fact]
    public async Task A_foreign_referer_is_not_rescued_by_anything()
    {
        var result = await Send("POST", configure: c => c.Request.Headers.Referer = "https://evil.example/page");
        Assert.False(result.Reached);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Status);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://spa.example.test:30000")]
    [InlineData("null")] // opaque origin: sandboxed frame, cross-origin redirect
    public async Task Unsafe_request_from_a_foreign_origin_is_rejected(string origin)
    {
        var result = await Send("POST", origin: origin);

        Assert.False(result.Reached);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Status);
        Assert.Contains("CSRF check failed", result.Body);
    }

    [Fact]
    public async Task Unsafe_request_with_neither_origin_nor_referer_is_rejected()
    {
        var result = await Send("POST");
        Assert.False(result.Reached);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Status);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Safe_methods_are_not_checked(string method) =>
        Assert.True((await Send(method)).Reached);

    [Fact]
    public async Task A_request_without_the_session_cookie_is_not_checked() =>
        Assert.True((await Send("POST", cookie: false)).Reached);

    [Fact]
    public async Task Bearer_requests_are_not_affected_even_if_a_cookie_rides_along() =>
        Assert.True((await Send("POST", configure: c => c.Request.Headers.Authorization = "Bearer abc")).Reached);

    [Theory]
    [InlineData(true, true)] // non-production: the dev header skips the cookie path
    [InlineData(false, false)] // production: the header means nothing, the cookie rules apply
    public async Task Dev_header_only_skips_the_check_outside_production(bool allowDevBypass, bool expectedReached)
    {
        var result = await Send("POST", allowDevBypass: allowDevBypass,
            configure: c => c.Request.Headers[DevAuthenticationHandler.DevHeaderName] = DevAuthenticationHandler.DevHeaderValue);
        Assert.Equal(expectedReached, result.Reached);
    }

    // No path is special: the same rule covers the IdP-side pages, Hangfire, SignalR negotiate and uploads.
    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/connect/logout")]
    [InlineData("/hangfire/stats")]
    [InlineData("/notificationHub/negotiate")]
    [InlineData("/documents/upload")]
    public async Task Every_path_is_held_to_the_same_origin_rule(string path)
    {
        Assert.True((await Send("POST", path, origin: Api)).Reached);
        Assert.False((await Send("POST", path, origin: "https://evil.example")).Reached);
        Assert.False((await Send("POST", path)).Reached);
    }

    [Fact]
    public async Task The_razor_login_form_posts_from_the_api_origin_with_an_app_cookie_present() =>
        Assert.True((await Send("POST", "/Account/Login", origin: Api)).Reached);

    [Fact]
    public async Task Websocket_upgrade_from_a_foreign_origin_is_rejected()
    {
        var result = await Send("GET", "/notificationHub", origin: "https://evil.example",
            configure: c => c.Request.Headers.Upgrade = "websocket");
        Assert.False(result.Reached);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Status);
    }

    [Fact]
    public async Task Websocket_upgrade_from_an_allowed_origin_passes() =>
        Assert.True((await Send("GET", "/notificationHub", origin: Spa,
            configure: c => c.Request.Headers.Upgrade = "websocket")).Reached);

    [Fact]
    public async Task Websocket_upgrade_with_no_origin_is_rejected() =>
        Assert.False((await Send("GET", "/notificationHub", configure: c => c.Request.Headers.Upgrade = "websocket")).Reached);

    // Chromium's WebSocket over HTTP/2 (RFC 8441): CONNECT with :protocol=websocket and no Upgrade header.
    private sealed class FakeExtendedConnect : IHttpExtendedConnectFeature
    {
        public bool IsExtendedConnect => true;
        public string? Protocol => "websocket";
        public ValueTask<Stream> AcceptAsync() => throw new NotSupportedException();
    }

    private static void ExtendedConnect(HttpContext context) =>
        context.Features.Set<IHttpExtendedConnectFeature>(new FakeExtendedConnect());

    [Fact]
    public async Task Http2_extended_connect_websocket_from_an_allowed_origin_passes() =>
        Assert.True((await Send("CONNECT", "/notificationHub", origin: Spa, configure: ExtendedConnect)).Reached);

    [Fact]
    public async Task Http2_extended_connect_websocket_from_a_foreign_origin_is_rejected()
    {
        var result = await Send("CONNECT", "/notificationHub", origin: "https://evil.example", configure: ExtendedConnect);
        Assert.False(result.Reached);
        Assert.Equal(StatusCodes.Status403Forbidden, result.Status);
    }

    [Fact]
    public async Task A_connect_request_is_held_to_the_origin_rule_even_if_the_feature_is_not_populated() =>
        Assert.False((await Send("CONNECT", "/notificationHub", origin: "https://evil.example")).Reached);
}
