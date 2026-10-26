using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Integration.Fixtures;

namespace Integration.Auth.Integration.Tests.Bff;

[Collection("Integration")]
public class CsrfIntegrationTests(IntegrationTestFixture fixture)
{
    private async Task<(HttpClient Browser, string Cookie)> SignedInAsync()
    {
        var browser = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        var user = await BffTestSupport.CreateUserAsync(fixture.BffWebApplicationFactory.Services, "RequestMaker");
        return (browser, await BffTestSupport.SignInAsync(browser, user));
    }

    [Fact]
    public async Task A_post_from_an_allowed_origin_goes_through()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;

        using var response = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/test/echo"), cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_post_with_neither_origin_nor_referer_is_a_403_problem()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;

        using var response = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/test/echo", origin: ""), cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("CSRF check failed");
    }

    [Fact]
    public async Task A_post_from_a_foreign_origin_is_a_403()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;

        using var response = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/test/echo", origin: "https://evil.example"), cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_form_post_a_foreign_page_could_make_is_a_403()
    {
        // The classic CSRF shape: a plain cross-site form post — no custom header, foreign Origin.
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;
        var request = new HttpRequestMessage(HttpMethod.Post, "/test/echo")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["x"] = "1" })
        };
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await BffTestSupport.SendAsync(browser, request, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bearer_requests_are_not_affected_even_with_a_stray_cookie()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;
        var token = await BffTestSupport.ClientCredentialsTokenAsync(browser);
        var request = new HttpRequestMessage(HttpMethod.Post, "/test/machine/echo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await BffTestSupport.SendAsync(browser, request, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_websocket_upgrade_from_a_foreign_origin_is_refused_before_the_hub_sees_it()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;
        var request = new HttpRequestMessage(HttpMethod.Get, "/notificationHub");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await BffTestSupport.SendAsync(browser, request, cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("CSRF check failed");
    }

    [Fact]
    public async Task A_websocket_upgrade_from_the_spa_origin_reaches_the_hub()
    {
        var (browser, cookie) = await SignedInAsync();
        using var _ = browser;
        var request = new HttpRequestMessage(HttpMethod.Get, "/notificationHub");
        request.Headers.TryAddWithoutValidation("Upgrade", "websocket");
        request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
        request.Headers.Add("Origin", BffTestSupport.Spa);
        request.Headers.Add("Cookie", cookie);

        // The in-memory server cannot complete a real upgrade, so the hub may answer with any status — or
        // hold the request open. The only claim here is that the CSRF check let it through.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await browser.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            (await response.Content.ReadAsStringAsync(timeout.Token)).Should().NotContain("CSRF check failed");
        }
        catch (OperationCanceledException)
        {
            // Held open by the hub: it was reached.
        }
    }
}
