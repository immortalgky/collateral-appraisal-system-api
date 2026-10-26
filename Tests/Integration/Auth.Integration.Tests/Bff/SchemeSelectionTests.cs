using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Auth.Infrastructure.Session;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// One policy scheme routes every request: dev header (non-production), then Bearer, then the app
/// cookie. Machine clients (LOS/CLS) keep working and never get a login redirect.
/// </summary>
[Collection("Integration")]
public class SchemeSelectionTests(IntegrationTestFixture fixture)
{
    private HttpClient Browser() => fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);

    private static HttpRequestMessage Send(string? bearer = null, string? cookie = null,
        string path = "/test/whoami", HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return request;
    }

    [Fact]
    public async Task No_credentials_is_a_401_not_a_redirect_to_login()
    {
        using var browser = Browser();
        using var response = await browser.SendAsync(Send());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task A_cookie_the_server_cannot_read_is_a_401()
    {
        using var browser = Browser();
        using var response = await browser.SendAsync(Send(cookie: "__Host-cas-session-7111=not-a-real-ticket"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task A_machine_client_token_authenticates_on_its_own()
    {
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token, path: "/test/machine/whoami"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(response);
        json.RootElement.GetProperty("authenticationType").GetString().Should().NotBe("CasSession");
    }

    [Fact]
    public async Task Bearer_wins_when_a_session_cookie_is_present_too()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "Admin");
        var cookie = await BffTestSupport.SignInAsync(browser, user);
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token, cookie: cookie, path: "/test/machine/whoami"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(response);
        var sub = json.RootElement.GetProperty("claims").EnumerateArray()
            .First(c => c.GetProperty("type").GetString() == "sub").GetProperty("value").GetString();
        sub.Should().Be("cls").And.NotBe(user.Id.ToString());
    }

    [Fact]
    public async Task A_machine_client_token_is_refused_on_an_endpoint_with_no_policy_of_its_own()
    {
        // /test/whoami names no policy, so the "Spa" fallback applies: authenticated, but not a browser session.
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_machine_client_token_is_refused_on_a_legacy_minimal_api_route_too()
    {
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token, path: "/appraisals"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_client_with_the_integration_scope_reaches_an_integration_route_but_not_a_legacy_one()
    {
        // The seeded cls client is granted `integration` (AuthDataSeed). With no query the checklist route answers
        // 200 with empty lists, whatever the Parameter tables hold.
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser, scope: "integration");

        using var allowed = await browser.SendAsync(Send(bearer: token, path: "/api/v1/document-checklist"));
        using var refused = await browser.SendAsync(Send(bearer: token, path: "/appraisals"));

        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("appraisal.read", HttpStatusCode.Forbidden)]
    [InlineData("integration", HttpStatusCode.NotFound)]
    public async Task The_integration_document_route_needs_the_integration_scope(string scope, HttpStatusCode expected)
    {
        // GET /api/v1/documents/{id} used to be plain RequireAuthorization(); it now names "Integration".
        // A random id is a 404 for a client allowed in.
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser, scope);

        using var response = await browser.SendAsync(
            Send(bearer: token, path: $"/api/v1/documents/{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task A_machine_client_token_is_refused_at_the_signalr_hub()
    {
        // The hub has no policy of its own, so connecting is authorized by the fallback over plain HTTP.
        using var browser = Browser();
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token,
            path: "/notificationHub/negotiate?negotiateVersion=1", method: HttpMethod.Post));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_session_cookie_passes_the_spa_fallback()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "Admin");
        var cookie = await BffTestSupport.SignInAsync(browser, user);

        using var response = await browser.SendAsync(Send(cookie: cookie));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_stray_cookie_does_not_rescue_a_bearer()
    {
        // The selector picks OpenIddict whenever a Bearer is present, so the cookie is never authenticated: the
        // caller is the machine client, and the Spa fallback refuses it.
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "Admin");
        var cookie = await BffTestSupport.SignInAsync(browser, user);
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);

        using var response = await browser.SendAsync(Send(bearer: token, cookie: cookie));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_bad_bearer_is_not_rescued_by_a_valid_cookie()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "Admin");
        var cookie = await BffTestSupport.SignInAsync(browser, user);

        using var response = await browser.SendAsync(Send(bearer: "not-a-token", cookie: cookie));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_dev_header_still_works_outside_production()
    {
        using var browser = Browser();
        var request = Send();
        request.Headers.Add("X-Dev-Auth", "dev-bypass");

        using var response = await browser.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(response);
        json.RootElement.GetProperty("userCode").GetString().Should().Be("dev-user");
    }

    [Fact]
    public async Task A_forbidden_call_is_a_403_not_a_redirect_to_an_access_denied_page()
    {
        // ChangePasswordEndpoint answers Results.Forbid() for someone else's password without the permission.
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "RequestMaker");
        var cookie = await BffTestSupport.SignInAsync(browser, user);
        var request = BffTestSupport.SpaRequest(HttpMethod.Post, $"/auth/users/{Guid.NewGuid()}/change-password");
        request.Content = JsonContent.Create(new { currentPassword = "x", newPassword = "y", confirmPassword = "y" });

        using var response = await BffTestSupport.SendAsync(browser, request, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task The_default_and_fallback_policies_are_not_pinned_to_any_scheme()
    {
        // Production used to pin both to OpenIddict, which made every cookie request fail there and only
        // there. They must follow the selector in every environment.
        await using var scope = fixture.BffWebApplicationFactory.Services.CreateAsyncScope();
        var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        (await policies.GetDefaultPolicyAsync()).AuthenticationSchemes.Should().BeEmpty();
        (await policies.GetFallbackPolicyAsync())!.AuthenticationSchemes.Should().BeEmpty();
    }

    [Fact]
    public async Task The_named_Spa_policy_is_the_default_and_the_fallback()
    {
        await using var scope = fixture.BffWebApplicationFactory.Services.CreateAsyncScope();
        var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var spa = await policies.GetPolicyAsync("Spa");

        spa.Should().NotBeNull();
        (await policies.GetDefaultPolicyAsync()).Should().BeSameAs(spa);
        (await policies.GetFallbackPolicyAsync()).Should().BeSameAs(spa);
    }

    [Fact]
    public async Task The_hangfire_dashboard_policy_runs_on_the_app_session_and_the_permission_claim()
    {
        await using var scope = fixture.BffWebApplicationFactory.Services.CreateAsyncScope();
        var policy = (await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync("HangfireDashboard"))!;

        policy.AuthenticationSchemes.Should().BeEquivalentTo([AuthSchemes.AppCookie]);
        policy.Requirements.OfType<ClaimsAuthorizationRequirement>().Should().ContainSingle(r =>
            r.ClaimType == "permissions" && r.AllowedValues!.Contains("JOB_SCHEDULE_MANAGE"));
    }

    [Fact]
    public void An_unsubstituted_cookie_suffix_token_stops_the_host_instead_of_naming_a_cookie_after_it()
    {
        using var broken = fixture.BffWebApplicationFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:Session:CookieSuffix", "#{SESSION_COOKIE_SUFFIX}#"));

        var failure = Record.Exception(() => broken.Services);

        failure.Should().NotBeNull();
        failure!.ToString().Should().Contain("Auth:Session:CookieSuffix").And.Contain("did not substitute");
    }

    [Fact]
    public async Task A_dashboard_xhr_poll_without_a_session_gets_a_401_and_starts_no_sign_in()
    {
        // Each challenge would plant correlation + nonce cookies for an hour; a forgotten tab polling every 2 s
        // would bury the browser in them.
        using var browser = Browser();
        var poll = new HttpRequestMessage(HttpMethod.Post, "/hangfire/probe")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["metrics[]"] = "servers:count" })
        };
        poll.Headers.Add("Sec-Fetch-Mode", "same-origin");
        poll.Headers.Add("Sec-Fetch-Dest", "empty");
        poll.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await browser.SendAsync(poll);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task A_dashboard_xhr_poll_without_fetch_metadata_is_still_not_a_navigation()
    {
        using var browser = Browser();
        var poll = new HttpRequestMessage(HttpMethod.Get, "/hangfire/probe");
        poll.Headers.Add("Accept", "text/html, */*; q=0.01");
        poll.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await browser.SendAsync(poll);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Theory]
    [InlineData("navigate", "document")]
    [InlineData("navigate", "iframe")]
    public async Task Navigating_to_the_dashboard_without_a_session_starts_sign_in(string mode, string dest)
    {
        using var browser = Browser();
        var navigation = new HttpRequestMessage(HttpMethod.Get, "/hangfire/probe");
        navigation.Headers.Add("Sec-Fetch-Mode", mode);
        navigation.Headers.Add("Sec-Fetch-Dest", dest);
        navigation.Headers.Add("Accept", "text/html,application/xhtml+xml");

        using var response = await browser.SendAsync(navigation);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().StartWith("https://localhost:7111/connect/authorize?");
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.Contains("Correlation"));
    }

    [Fact]
    public async Task With_no_suffix_configured_the_cookie_names_carry_the_app_base_url_port()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "RequestMaker");
        using var response = await browser.GetAsync($"/test/signin?userId={user.Id}&name={user.UserName}");

        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("__Host-cas-session-7111="));
        var names = fixture.BffWebApplicationFactory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<BrowserSessionOptions>>().Value;
        names.IdpCookieName.Should().Be("__Host-cas-idp-7111");
        names.RetryCookieName.Should().Be("cas-oidc-retry-7111");
    }

    [Fact]
    public void A_configured_suffix_overrides_the_port()
    {
        using var overridden = fixture.BffWebApplicationFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:Session:CookieSuffix", "-sit9"));

        var names = overridden.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<BrowserSessionOptions>>().Value;

        names.AppCookieName.Should().Be("__Host-cas-session-sit9");
        names.IdpCookieName.Should().Be("__Host-cas-idp-sit9");
    }
}
