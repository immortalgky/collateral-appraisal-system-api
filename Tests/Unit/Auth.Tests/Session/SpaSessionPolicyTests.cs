using System.Security.Claims;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Identity;

namespace Auth.Tests.Session;

public sealed class SpaSessionPolicyTests : IDisposable
{
    // xUnit builds one instance per test, so each test gets its own captured log and one disposed provider.
    private readonly ListLoggerProvider _log = new();
    private readonly ServiceProvider _services;

    public SpaSessionPolicyTests() =>
        _services = new ServiceCollection().AddLogging(b => b.AddProvider(_log)).BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    private DefaultHttpContext Request(string method = "GET", string path = "/appraisals")
    {
        var http = new DefaultHttpContext();
        http.RequestServices = _services;
        http.Request.Method = method;
        http.Request.Path = path;
        return http;
    }

    private static ClaimsIdentity Identity(string? type, string sub = "u1") =>
        new([new Claim("sub", sub), new Claim("client_id", sub)], type);

    // What OpenIddict validation produces for a machine token (observed on a real client_credentials token).
    private static ClaimsIdentity OpenIddict() => Identity("AuthenticationTypes.Federation", "cls");

    private static bool Allowed(object? resource, params ClaimsIdentity[] identities) =>
        Allowed(resource, true, identities);

    private static bool Allowed(object? resource, bool allowDevBypass, params ClaimsIdentity[] identities) =>
        SpaSessionPolicy.IsBrowserSession(new AuthorizationHandlerContext(
            [new AssertionRequirement(_ => true)], new ClaimsPrincipal(identities), resource), allowDevBypass);

    [Theory]
    [InlineData(AuthSchemes.AppCookie, true)]
    [InlineData(AuthSchemes.AppCookie, false)]
    [InlineData(DevAuthenticationHandler.SchemeName, true)]
    public void A_cookie_session_or_an_allowed_dev_identity_passes(string type, bool allowDevBypass) =>
        Assert.True(Allowed(Request(), allowDevBypass, Identity(type)));

    [Theory]
    [InlineData("AuthenticationTypes.Federation")]
    [InlineData("Bearer")]
    [InlineData("ApiKey")]
    [InlineData("Test")]
    public void Any_other_authenticated_identity_is_refused_and_logged(string type)
    {
        Assert.False(Allowed(Request(), Identity(type, "cls")));
        Assert.Contains("[AUTH-SPA-REFUSED] cls GET /appraisals", _log.Messages);
    }

    [Fact]
    public void The_dev_identity_is_refused_when_the_bypass_is_off()
    {
        Assert.False(Allowed(Request(), false, Identity(DevAuthenticationHandler.SchemeName, "dev")));
        Assert.Contains("[AUTH-SPA-REFUSED] dev GET /appraisals", _log.Messages);
    }

    [Fact]
    public void A_cookie_merged_with_a_machine_identity_is_refused_and_logged()
    {
        // The cookie user (u1) is allowed; the log must name the machine client that got it refused.
        Assert.False(Allowed(Request(), Identity(AuthSchemes.AppCookie, "u1"), OpenIddict()));
        Assert.Equal("[AUTH-SPA-REFUSED] cls GET /appraisals", Assert.Single(_log.Messages));
    }

    [Fact]
    public void A_cookie_merged_with_a_dev_identity_is_refused_and_logged_when_the_bypass_is_off()
    {
        Assert.False(Allowed(Request(), false, Identity(AuthSchemes.AppCookie), Identity(DevAuthenticationHandler.SchemeName)));
        Assert.Single(_log.Messages);
    }

    [Fact]
    public void The_logged_path_is_encoded_so_it_cannot_inject_lines()
    {
        var http = Request();
        http.Request.Path = PathString.FromUriComponent("/appraisals%0D%0Ainjected");

        Assert.False(Allowed(http, OpenIddict()));
        var message = Assert.Single(_log.Messages);
        Assert.EndsWith("/appraisals%0D%0Ainjected", message);
        Assert.DoesNotContain('\r', message);
        Assert.DoesNotContain('\n', message);
    }

    [Fact]
    public void An_unauthenticated_caller_is_refused_and_not_logged()
    {
        Assert.False(Allowed(Request(), Identity(null)));
        Assert.Empty(_log.Messages);
    }

    [Fact]
    public void An_unauthenticated_first_identity_does_not_hide_a_cookie_session_second()
    {
        // The same view of "authenticated" as RequireAuthenticatedUser: any identity, not just the primary one.
        Assert.True(Allowed(Request(), Identity(null), Identity(AuthSchemes.AppCookie)));
    }

    [Fact]
    public void An_unauthenticated_identity_beside_a_machine_one_changes_nothing()
    {
        Assert.False(Allowed(Request(), Identity(null), OpenIddict()));
    }

    [Fact]
    public void A_null_or_unrelated_resource_decides_the_same_and_does_not_throw()
    {
        foreach (var resource in new object?[] { null, new object() })
        {
            Assert.True(Allowed(resource, Identity(AuthSchemes.AppCookie)));
            Assert.False(Allowed(resource, OpenIddict()));
        }
    }

    [Fact]
    public void A_request_with_no_service_provider_is_refused_without_throwing()
    {
        var http = new DefaultHttpContext { RequestServices = null! };

        Assert.False(Allowed(http, OpenIddict()));
    }

    private sealed class ListLoggerProvider : ILoggerProvider, ILogger
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
