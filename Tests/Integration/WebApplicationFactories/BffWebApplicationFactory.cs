using System.Reflection;
using Auth.Application.Services;
using Auth.Infrastructure.Session;
using Integration.Auth.Integration.Tests.Bff;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.WebApplicationFactories;

/// <summary>
/// The test host with the application's REAL authentication — app cookie, OIDC handler, OpenIddict
/// Bearer, dev header — instead of the "Test" scheme every other factory swaps in. Used to prove the
/// browser-session (BFF) behaviour end to end.
/// <list type="bullet">
/// <item>The OIDC back-channel (discovery, token redemption) is routed into the in-memory test server, so
/// a full sign-in runs without a network.</item>
/// <item><c>Auth:InternalAuthBaseUrl</c> is deliberately a different host from <c>AppBaseUrl</c>, which
/// reproduces production: the discovery document then names endpoints on the internal host while the
/// issuer stays the public one.</item>
/// <item>A test-only controller ("/test/...") signs in a chosen user and echoes the principal.</item>
/// </list>
/// </summary>
public sealed class BffWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    public const string InternalAuthority = "https://internal.test";

    /// <summary>
    /// https, so the Secure session cookies a real browser would hold are kept and replayed; and the host and port of
    /// <c>AppBaseUrl</c>, because <c>/auth/login</c> refuses a request that arrives under any other.
    /// </summary>
    public static readonly Uri BaseAddress = new("https://localhost:7111");

    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        // Intentionally empty: keep the schemes AddAuthModule registered.
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Reporting warms up a real Chromium in the background of every host (downloading ~150 MB on a fresh box),
        // and disposing a host that raced it throws. Nothing here renders a report: point it at a browser that
        // does not exist, so the warm-up fails fast and quietly and nothing is left to leak or throw on dispose.
        const string NoBrowser = "/nonexistent/chromium-for-tests";
        builder.UseSetting("Auth:InternalAuthBaseUrl", InternalAuthority);
        // LDAP accounts sign in against FakeLdapAuthenticationService below; the flag only lets the login page try.
        builder.UseSetting("Ldap:Enabled", "true");
        builder.UseSetting("Reporting:ChromiumExecutablePath", NoBrowser);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:InternalAuthBaseUrl"] = InternalAuthority,
                ["Ldap:Enabled"] = "true",
                ["Reporting:ChromiumExecutablePath"] = NoBrowser
            }));

        builder.ConfigureServices(services =>
        {
            // Configure, not PostConfigure: the handler's own post-configuration turns BackchannelHttpHandler
            // into the Backchannel HttpClient and builds the metadata manager on it, and runs first.
            services.Configure<OpenIdConnectOptions>(AuthSchemes.Oidc, options =>
                options.BackchannelHttpHandler = new LazyHandler(() => Server.CreateHandler()));

            services.AddControllers().AddApplicationPart(typeof(BffTestController).Assembly);

            // No directory in a test run: the bind succeeds for BffTestSupport.Password and for nothing else.
            services.AddSingleton<ILdapAuthenticationService, FakeLdapAuthenticationService>();

            // Development keeps OpenIddict's keys as certificates in the OS key store. On macOS reading the
            // private key back (decrypting an authorization code) raises a keychain prompt nobody can
            // answer in a test run, and the request hangs. Throw-away in-memory keys behave identically.
            services.AddOpenIddict().AddServer(options =>
            {
                options.Configure(o =>
                {
                    o.SigningCredentials.Clear();
                    o.EncryptionCredentials.Clear();
                });
                options.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
            });
        });
    }

    public HttpClient CreateBrowser(bool handleCookies = true, Uri? baseAddress = null)
    {
        var browser = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = baseAddress ?? BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = handleCookies
        });
        // A request that never answers should fail the test that made it, not hang the whole run.
        browser.Timeout = TimeSpan.FromSeconds(30);
        return browser;
    }

    /// <summary>Defers building the handler until the first request, when the test server exists.</summary>
    private sealed class LazyHandler(Func<HttpMessageHandler> create) : DelegatingHandler
    {
        private readonly Lazy<HttpMessageHandler> _inner = new(create);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var invoker = new HttpMessageInvoker(_inner.Value, disposeHandler: false);
            return invoker.SendAsync(request, cancellationToken);
        }
    }

    private sealed class FakeLdapAuthenticationService : ILdapAuthenticationService
    {
        public Task<LdapAuthResult> AuthenticateAsync(string username, string password) =>
            Task.FromResult(password == BffTestSupport.Password
                ? new LdapAuthResult(true, new LdapUserInfo(username, null, null, null, null, null, null))
                : new LdapAuthResult(false, ErrorMessage: "bad password"));

        // The service bind: what the directory says about the account, as FakeDirectory has it (null = unreachable).
        public Task<LdapUserInfo?> GetUserInfoAsync(string username) =>
            Task.FromResult(FakeDirectory.Lookup(username) is { } account
                ? new LdapUserInfo(username, null, null, null, null, null, null, account.UserAccountControl, account.AccountExpires, account.Computed)
                : null);

        public Task CheckConnectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
