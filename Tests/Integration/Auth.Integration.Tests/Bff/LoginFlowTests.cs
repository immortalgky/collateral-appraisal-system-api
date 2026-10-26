using System.Net;
using System.Text.RegularExpressions;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Sessions;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// The whole browser sign-in against our own OpenIddict, run in-process: /auth/login -> /connect/authorize
/// -> the Razor login page -> /signin-oidc -> app cookie. Authority is a different host from the issuer, as
/// behind the load balancer.
/// </summary>
[Collection("Integration")]
public partial class LoginFlowTests(IntegrationTestFixture fixture)
{
    private const string PublicBase = "https://localhost:7111";
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private static DateTime Now(IServiceProvider services) =>
        services.GetRequiredService<Shared.Time.IDateTimeProvider>().ApplicationNow;

    private HttpClient Browser() => fixture.BffWebApplicationFactory.CreateBrowser();

    private static string Pending(Uri location) => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;

    private static string Query(string url, string key) =>
        QueryHelpers.ParseQuery(new Uri("https://x" + url).Query)[key].ToString();

    private OpenIdConnectOptions OidcOptions() =>
        Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("oidc");

    /// <summary>Runs /auth/login and the authorize hop, ending on the Razor login page's URL.</summary>
    private static async Task<(string AuthorizeUrl, string LoginPageUrl)> StartAsync(HttpClient browser, string returnUrl,
        string? loginHint = null)
    {
        var hint = loginHint is null ? "" : "&login_hint=" + Uri.EscapeDataString(loginHint);
        using var login = await browser.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl) + hint,
            TestContext.Current.CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.Found, await login.Content.ReadAsStringAsync());
        var authorize = login.Headers.Location!;
        authorize.GetLeftPart(UriPartial.Authority).Should().Be(PublicBase); // public origin, not internal.test

        using var hop = await browser.GetAsync(Pending(authorize), TestContext.Current.CancellationToken);
        hop.StatusCode.Should().Be(HttpStatusCode.Found);
        return (authorize.ToString(), Pending(hop.Headers.Location!));
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpClient browser, string loginPageUrl)
    {
        using var page = await browser.GetAsync(loginPageUrl, TestContext.Current.CancellationToken);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        return TokenPattern().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;
    }

    // A browser sends Origin on a same-origin form POST; the CSRF middleware needs it.
    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient browser, string token, TestUser user,
        string returnUrl, string? password = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = user.UserName,
                ["Password"] = password ?? user.Password,
                ["ReturnUrl"] = returnUrl,
                ["__RequestVerificationToken"] = token
            })
        };
        request.Headers.Add("Origin", PublicBase);
        return browser.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Login_hint_reaches_the_authorize_request_and_prefills_the_username()
    {
        using var browser = Browser();
        var (authorize, loginPage) = await StartAsync(browser, $"{BffTestSupport.Spa}/", "int.staff1");

        Query(Pending(new Uri(authorize)), "login_hint").Should().Be("int.staff1");
        using var page = await browser.GetAsync(loginPage, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("name=\"Username\" value=\"int.staff1\"");
        // The language switch must remember an emptied field too, or the prefill comes back after it.
        html.Should().Contain("sessionStorage.setItem('login-username'").And.NotContain("if (username)");
    }

    /// <summary>Starts sign-in with a hint, signs in as <paramref name="signInAs"/> and returns where the callback sends the browser.</summary>
    private async Task<string> SignInWithHintAsync(string hint, TestUser signInAs, string returnUrl)
    {
        using var browser = Browser();
        var (_, loginPage) = await StartAsync(browser, returnUrl, hint);
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, signInAs, Query(loginPage, "ReturnUrl"));
        using var code = await browser.GetAsync(Pending(posted.Headers.Location!), TestContext.Current.CancellationToken);
        using var signedIn = await browser.GetAsync(Pending(code.Headers.Location!), TestContext.Current.CancellationToken);
        signedIn.StatusCode.Should().Be(HttpStatusCode.Found);
        return signedIn.Headers.Location!.ToString();
    }

    [Fact]
    public async Task Signing_in_as_the_hinted_user_lands_on_the_return_url_whatever_the_hints_case()
    {
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var returnUrl = $"{BffTestSupport.Spa}/appraisal/5?tab=2";

        (await SignInWithHintAsync(user.UserName.ToUpperInvariant(), user, returnUrl)).Should().Be(returnUrl);
    }

    [Fact]
    public async Task Signing_in_as_someone_else_than_the_hinted_user_lands_on_the_spa_home()
    {
        var hinted = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var other = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");

        (await SignInWithHintAsync(hinted.UserName, other, $"{BffTestSupport.Spa}/appraisal/5?tab=2"))
            .Should().Be($"{BffTestSupport.Spa}/");
    }

    [Fact]
    public async Task The_retry_after_a_stale_state_drops_the_login_hint()
    {
        using var browser = Browser();
        using var response = await browser.GetAsync(
            "/auth/login?retry=1&login_hint=int.staff1&returnUrl=" + Uri.EscapeDataString($"{BffTestSupport.Spa}/"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Contain("/connect/authorize").And.NotContain("login_hint");
    }

    [Theory]
    [InlineData("<script>")]
    [InlineData("a b")]
    public async Task An_invalid_login_hint_is_ignored(string hint)
    {
        using var browser = Browser();
        var (authorize, loginPage) = await StartAsync(browser, $"{BffTestSupport.Spa}/", hint);

        authorize.Should().NotContain("login_hint");
        using var page = await browser.GetAsync(loginPage, TestContext.Current.CancellationToken);
        (await page.Content.ReadAsStringAsync()).Should().Contain("name=\"Username\" value=\"\"");
    }

    [Fact]
    public async Task Challenge_goes_to_the_public_authorize_endpoint_with_prompt_login_and_pkce()
    {
        using var browser = Browser();
        using var response = await browser.GetAsync(
            "/auth/login?returnUrl=" + Uri.EscapeDataString($"{BffTestSupport.Spa}/appraisal/5?tab=2"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found, await response.Content.ReadAsStringAsync());
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith($"{PublicBase}/connect/authorize?");
        Query(location[PublicBase.Length..], "client_id").Should().Be("spa");
        Query(location[PublicBase.Length..], "response_type").Should().Be("code");
        Query(location[PublicBase.Length..], "prompt").Should().Be("login");
        Query(location[PublicBase.Length..], "scope").Should().Be("openid profile");
        Query(location[PublicBase.Length..], "redirect_uri").Should().Be($"{PublicBase}/signin-oidc");
        Query(location[PublicBase.Length..], "code_challenge_method").Should().Be("S256");
        location.Should().NotContain("client_secret").And.NotContain("offline_access");
    }

    [Theory]
    [InlineData("https://localhost:3000/appraisal/5?tab=2", "https://localhost:3000/appraisal/5?tab=2")]
    [InlineData("https://evil.example/phish", "https://localhost:3000/")]
    [InlineData("https://localhost:3000.evil.example/", "https://localhost:3000/")]
    [InlineData("/hangfire/recurring", "/hangfire/recurring")] // a path on the API's own origin stays
    [InlineData("//evil.example/x", "https://localhost:3000/")]
    public async Task Return_url_must_be_on_an_allowed_origin(string requested, string expected)
    {
        using var browser = Browser();
        using var response = await browser.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString(requested),
            TestContext.Current.CancellationToken);

        var state = Query(response.Headers.Location!.PathAndQuery, "state");
        OidcOptions().StateDataFormat.Unprotect(state)!.RedirectUri.Should().Be(expected);
    }

    [Fact]
    public async Task Full_sign_in_lands_on_the_return_url_with_a_session_and_logout_ends_it()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var returnUrl = $"{BffTestSupport.Spa}/appraisal/5?tab=2";
        var (_, loginPage) = await StartAsync(browser, returnUrl);
        var authorizeReturn = Query(loginPage, "ReturnUrl");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, authorizeReturn);
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        var resume = Pending(posted.Headers.Location!);
        resume.Should().StartWith("/connect/authorize");

        using var code = await browser.GetAsync(resume, TestContext.Current.CancellationToken);
        code.StatusCode.Should().Be(HttpStatusCode.Found);
        var callback = code.Headers.Location!;
        callback.GetLeftPart(UriPartial.Path).Should().Be($"{PublicBase}/signin-oidc");
        using var signedIn = await browser.GetAsync(Pending(callback), TestContext.Current.CancellationToken);
        signedIn.StatusCode.Should().Be(HttpStatusCode.Found);
        signedIn.Headers.Location!.ToString().Should().Be(returnUrl);

        // The browser now holds a session, and nothing else.
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        session.EndedAt.Should().BeNull();

        using var me = await browser.GetAsync("/auth/me", TestContext.Current.CancellationToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var meJson = await BffTestSupport.ReadJsonAsync(me);
        meJson.RootElement.GetProperty("username").GetString().Should().Be(user.UserName);

        using var whoAmI = await browser.GetAsync("/test/whoami", TestContext.Current.CancellationToken);
        using var whoJson = await BffTestSupport.ReadJsonAsync(whoAmI);
        whoJson.RootElement.GetProperty("userCode").GetString().Should().Be(user.UserName);

        // Logout: local session ends, and the IdP end-session URL carries the id_token_hint.
        using var logout = await browser.SendAsync(BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"),
            TestContext.Current.CancellationToken);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        using var logoutJson = await BffTestSupport.ReadJsonAsync(logout);
        var logoutUrl = logoutJson.RootElement.GetProperty("url").GetString()!;
        logoutUrl.Should().StartWith($"{PublicBase}/connect/logout").And.Contain("id_token_hint=");

        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Logout);
        using var after = await browser.GetAsync("/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The IdP accepts that URL and sends the browser back through /signout-callback-oidc to the SPA.
        using var idp = await browser.GetAsync(Pending(new Uri(logoutUrl)), TestContext.Current.CancellationToken);
        idp.StatusCode.Should().Be(HttpStatusCode.Found);
        var back = idp.Headers.Location!;
        back.GetLeftPart(UriPartial.Path).Should().Be($"{PublicBase}/signout-callback-oidc");
        using var spa = await browser.GetAsync(Pending(back), TestContext.Current.CancellationToken);
        spa.StatusCode.Should().Be(HttpStatusCode.Found, logoutUrl + " -> " + back + " :: " + await spa.Content.ReadAsStringAsync());
        spa.Headers.Location!.ToString().Should().Be("https://localhost:3000/");

        // The IdP end-session endpoint audits the SSO cookie's user. It used to read the default scheme,
        // which a browser navigation never satisfies, so LoggedOut never fired.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        (await db.AuthAuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityId == user.Id && a.Action == AuditAction.LoggedOut)).Should().BeTrue();
    }

    [Fact]
    public async Task A_deactivated_user_cannot_complete_the_flow()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));
        using var code = await browser.GetAsync(Pending(posted.Headers.Location!), TestContext.Current.CancellationToken);

        // Deactivated between the password check and the callback.
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);
        using var callback = await browser.GetAsync(Pending(code.Headers.Location!), TestContext.Current.CancellationToken);

        // A rejection that would repeat is not retried (the user would type the password twice for nothing), and
        // it leaves no half-open session.
        callback.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Sign-in failed");
        (await BffTestSupport.GetSessionsAsync(Services, user.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Starting_a_login_changes_nothing_and_completing_it_replaces_the_browsers_previous_session()
    {
        using var browser = Browser();
        var previous = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await BffTestSupport.SignInAsync(browser, previous);

        // GET /auth/login only issues a challenge: a same-site <img> must not be able to sign anyone out.
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        (await BffTestSupport.GetSessionAsync(Services, previous.Id)).EndedAt.Should().BeNull();
        using (var stillIn = await browser.GetAsync("/test/whoami", TestContext.Current.CancellationToken))
            stillIn.StatusCode.Should().Be(HttpStatusCode.OK);

        // The new user signs in through the IdP while the previous user's cookie is still in the browser.
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));
        using var code = await browser.GetAsync(Pending(posted.Headers.Location!), TestContext.Current.CancellationToken);
        using var signedIn = await browser.GetAsync(Pending(code.Headers.Location!), TestContext.Current.CancellationToken);
        signedIn.StatusCode.Should().Be(HttpStatusCode.Found);

        (await BffTestSupport.GetSessionAsync(Services, previous.Id)).EndReason.Should().Be(SessionEndReason.Replaced);
        using var whoAmI = await browser.GetAsync("/test/whoami", TestContext.Current.CancellationToken);
        whoAmI.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(whoAmI);
        json.RootElement.GetProperty("userCode").GetString().Should().Be(user.UserName); // never the previous user

        // A brand-new session row for the new user: not the previous user's row taken over.
        var sessions = await BffTestSupport.GetSessionsAsync(Services, user.Id);
        sessions.Should().ContainSingle().Which.UserId.Should().Be(user.Id);
        sessions[0].Id.Should().NotBe((await BffTestSupport.GetSessionAsync(Services, previous.Id)).Id);
    }

    [Fact]
    public async Task A_stale_callback_restarts_sign_in_without_ending_anything()
    {
        // The original bug: a bookmarked login URL replayed while another user's session is live. The callback
        // fails; it restarts sign-in, and the previous session is replaced only by a sign-in that completes.
        using var browser = Browser();
        var previous = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await BffTestSupport.SignInAsync(browser, previous);

        using var response = await browser.GetAsync("/signin-oidc?code=stale&state=stale", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().StartWith($"{PublicBase}/auth/login?returnUrl=").And.EndWith("&retry=1");
        (await BffTestSupport.GetSessionAsync(Services, previous.Id)).EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_second_failure_in_a_row_stops_instead_of_looping()
    {
        using var browser = Browser();
        using var login = await browser.GetAsync(
            "/auth/login?retry=1&returnUrl=" + Uri.EscapeDataString(BffTestSupport.Spa + "/"),
            TestContext.Current.CancellationToken);
        var state = Query(login.Headers.Location!.PathAndQuery, "state");

        // The callback arrives without the correlation cookie: a state failure on an attempt that was already a retry.
        using var cookieless = Browser();
        using var response = await cookieless.GetAsync($"/signin-oidc?code=x&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Sign-in failed");
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Login?ReturnUrl=%2F")]
    [InlineData("/Account/Login?ReturnUrl=https%3A%2F%2Fevil.example%2F")]
    public async Task The_login_page_without_a_pending_authorize_request_sends_the_browser_back_to_the_spa(string url)
    {
        using var browser = Browser();
        using var response = await browser.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("https://localhost:3000/");
    }

    [Fact]
    public async Task The_login_page_and_its_errors_follow_the_browsers_language()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var returnUrl = Query(loginPage, "ReturnUrl");

        async Task<string> Send(HttpRequestMessage request, string language, string? cookie = null)
        {
            request.Headers.Add("Accept-Language", language);
            if (cookie is not null) request.Headers.Add("Cookie", cookie);
            using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        var thai = await Send(new HttpRequestMessage(HttpMethod.Get, loginPage), "th-TH,th;q=0.9");
        thai.Should().MatchRegex("/Assets/index\\.css\\?v=[0-9a-f]{8}\"");
        thai.Should().Contain("<html lang=\"th\">").And.Contain("กรอกชื่อผู้ใช้และรหัสผ่าน").And.Contain("เลือกวิธีเข้าสู่ระบบอื่น");

        // The cas-lang cookie wins over Accept-Language; an unsupported value falls back to it.
        (await Send(new HttpRequestMessage(HttpMethod.Get, loginPage), "th", "cas-lang=zh"))
            .Should().Contain("<html lang=\"zh\">").And.Contain("请输入用户名和密码");
        (await Send(new HttpRequestMessage(HttpMethod.Get, loginPage), "th", "cas-lang=xx"))
            .Should().Contain("<html lang=\"th\">");
        (await Send(new HttpRequestMessage(HttpMethod.Get, loginPage), "fr"))
            .Should().Contain("<html lang=\"en\">").And.Contain("Enter your username and password");

        var token = await AntiforgeryTokenAsync(browser, loginPage);
        var post = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = user.UserName,
                ["Password"] = "wrong-password",
                ["ReturnUrl"] = returnUrl,
                ["__RequestVerificationToken"] = token
            })
        };
        post.Headers.Add("Origin", PublicBase);
        (await Send(post, "th")).Should().Contain("ข้อมูลการเข้าสู่ระบบไม่ถูกต้อง");
    }

    [Fact]
    public async Task The_login_form_never_redirects_to_an_absolute_url()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);

        using var posted = await PostLoginAsync(browser, token, user, "https://evil.example/phish");

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        posted.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task The_login_form_works_while_another_users_app_session_is_still_in_the_browser()
    {
        // The original-bug scenario: B's session is alive, A submits the bookmarked login form. The CSRF
        // middleware checks every unsafe cookie-bearing request; the Razor login POST passes because it carries the
        // API's own origin, which is trusted.
        using var browser = Browser();
        var previous = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await BffTestSupport.SignInAsync(browser, previous);

        var authorize = "/connect/authorize?client_id=spa&redirect_uri=" + Uri.EscapeDataString($"{PublicBase}/signin-oidc")
                        + "&response_type=code&scope=" + Uri.EscapeDataString("openid profile")
                        + "&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256"
                        + "&state=s&nonce=n&prompt=login";
        using var hop = await browser.GetAsync(authorize, TestContext.Current.CancellationToken);
        var loginPage = Pending(hop.Headers.Location!);
        var returnUrl = Query(loginPage, "ReturnUrl");
        var token = await AntiforgeryTokenAsync(browser, loginPage);

        using var withoutToken = await PostLoginAsync(browser, "", user, returnUrl);
        // Refused by the page's own antiforgery (400), not by our CSRF middleware (403 "CSRF check failed").
        withoutToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var posted = await PostLoginAsync(browser, token, user, returnUrl);
        posted.StatusCode.Should().Be(HttpStatusCode.Found, await posted.Content.ReadAsStringAsync());
        Pending(posted.Headers.Location!).Should().StartWith("/connect/authorize");
    }

    public enum WayTheSessionIsGone
    {
        EndedInTheDatabase,
        IdleButNotYetNoticed, // the logout request itself is what finds out
        UserDeactivatedButSessionStillLive // ditto
    }

    [Theory]
    [InlineData(WayTheSessionIsGone.EndedInTheDatabase)]
    [InlineData(WayTheSessionIsGone.IdleButNotYetNoticed)]
    [InlineData(WayTheSessionIsGone.UserDeactivatedButSessionStillLive)]
    public async Task Logout_after_the_app_session_is_gone_still_clears_the_idp_cookie(WayTheSessionIsGone way)
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));
        using var code = await browser.GetAsync(Pending(posted.Headers.Location!), TestContext.Current.CancellationToken);
        using var signedIn = await browser.GetAsync(Pending(code.Headers.Location!), TestContext.Current.CancellationToken);
        signedIn.StatusCode.Should().Be(HttpStatusCode.Found);

        // The browser keeps its app cookie while the session behind it goes away, as it would while the user is away.
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        switch (way)
        {
            case WayTheSessionIsGone.EndedInTheDatabase:
                await BffTestSupport.UpdateSessionAsync(Services, session.Id, s =>
                {
                    s.EndedAt = Now(Services);
                    s.EndReason = SessionEndReason.Revoked;
                });
                break;
            case WayTheSessionIsGone.IdleButNotYetNoticed:
                await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = Now(Services).AddHours(-2));
                break;
            default:
                await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);
                await using (var scope = Services.CreateAsyncScope())
                    scope.ServiceProvider.GetRequiredService<AuthorizationClaimsBuilder>().Invalidate(user.Id);
                break;
        }

        Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            .Remove(UserSessionService.CacheKey(session.Id));

        using var logout = await browser.SendAsync(BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"),
            TestContext.Current.CancellationToken);

        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(logout);
        var url = json.RootElement.GetProperty("url").GetString()!;
        url.Should().StartWith($"{PublicBase}/connect/logout").And.Contain("client_id=spa")
            .And.Contain("post_logout_redirect_uri=");
        if (way == WayTheSessionIsGone.EndedInTheDatabase)
            url.Should().NotContain("id_token_hint="); // nothing live to take it from

        // The IdP accepts the URL, clears its SSO cookie and sends the browser back to the SPA.
        using var idp = await browser.GetAsync(Pending(new Uri(url)), TestContext.Current.CancellationToken);
        idp.StatusCode.Should().Be(HttpStatusCode.Found);
        idp.Headers.GetValues("Set-Cookie").Should().Contain(c =>
            c.StartsWith("__Host-cas-idp-7111=;") && c.Contains("expires=Thu, 01 Jan 1970"));
        Pending(idp.Headers.Location!).Should().StartWith("/signout-callback-oidc");
    }

    [Fact]
    public async Task A_sign_in_attempt_survives_an_hour_on_the_login_page()
    {
        // The default 15 minutes would make a user who was called away fail with "Correlation failed" and face a
        // second password prompt.
        var options = OidcOptions();
        options.RemoteAuthenticationTimeout.Should().Be(TimeSpan.FromMinutes(60));
        options.ProtocolValidator.NonceLifetime.Should().Be(TimeSpan.FromMinutes(60));

        using var browser = Browser();
        using var response = await browser.GetAsync("/auth/login", TestContext.Current.CancellationToken);

        var shortLived = response.Headers.GetValues("Set-Cookie")
            .Where(c => c.Contains("Correlation") || c.Contains("Nonce"))
            .Select(c => DateTimeOffset.Parse(
                System.Text.RegularExpressions.Regex.Match(c, "expires=([^;]+)").Groups[1].Value))
            .ToList();
        shortLived.Should().HaveCount(2);
        shortLived.Should().OnlyContain(expires =>
            expires > DateTimeOffset.UtcNow.AddMinutes(58) && expires < DateTimeOffset.UtcNow.AddMinutes(62));
        // and prompt=login stays on the attempt
        Query(response.Headers.Location!.PathAndQuery, "prompt").Should().Be("login");
    }

    [Fact]
    public async Task Two_stale_callbacks_in_a_row_stop_even_though_the_state_cannot_be_read()
    {
        // Neither callback carries a readable state, so the "second attempt" flag cannot ride in it.
        using var browser = Browser();

        using var first = await browser.GetAsync("/signin-oidc?code=stale&state=stale", TestContext.Current.CancellationToken);
        using var second = await browser.GetAsync("/signin-oidc?code=stale&state=stale", TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.Found);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Sign-in failed")
            .And.Contain("check that VITE_API_URL equals AppBaseUrl")
            .And.Contain(PublicBase);

        // The block is over: a later, separate failure restarts once again.
        using var later = await browser.GetAsync("/signin-oidc?code=stale&state=stale", TestContext.Current.CancellationToken);
        later.StatusCode.Should().Be(HttpStatusCode.Found);
    }

    [Fact]
    public void With_no_internal_authority_the_back_channel_goes_to_the_idps_public_address()
    {
        using var split = fixture.BffWebApplicationFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:Idp:PublicBaseUrl", "https://idp.example.test:8443");
            builder.UseSetting("Auth:InternalAuthBaseUrl", "");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:InternalAuthBaseUrl"] = "" }));
        });

        var options = split.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("oidc");

        options.Authority.Should().Be("https://idp.example.test:8443");
    }

    [Fact]
    public async Task A_split_idp_is_reached_at_its_own_public_address_while_callbacks_stay_on_the_api()
    {
        using var split = fixture.BffWebApplicationFactory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:Idp:PublicBaseUrl", "https://idp.example.test:8443"));
        using var browser = split.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = BffWebApplicationFactory.BaseAddress,
            AllowAutoRedirect = false
        });

        using var response = await browser.GetAsync("/auth/login", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Authority).Should().Be("https://idp.example.test:8443");
        location.AbsolutePath.Should().Be("/connect/authorize");
        Query(location.PathAndQuery, "redirect_uri").Should().Be($"{PublicBase}/signin-oidc");
    }

    [Fact]
    public async Task A_failed_callback_keeps_a_path_on_the_apis_own_origin_when_it_restarts_sign_in()
    {
        // The Hangfire dashboard sends the browser through sign-in with a relative return path.
        using var browser = Browser();
        using var login = await browser.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString("/hangfire/recurring"),
            TestContext.Current.CancellationToken);
        var state = Query(login.Headers.Location!.PathAndQuery, "state");
        OidcOptions().StateDataFormat.Unprotect(state)!.RedirectUri.Should().Be("/hangfire/recurring");

        // The callback arrives without the correlation cookie, so only the state can say where to restart.
        using var cookieless = Browser();
        using var failed = await cookieless.GetAsync(
            $"/signin-oidc?code=x&state={Uri.EscapeDataString(state)}", TestContext.Current.CancellationToken);

        failed.StatusCode.Should().Be(HttpStatusCode.Found);
        failed.Headers.Location!.OriginalString.Should()
            .Be($"{PublicBase}/auth/login?returnUrl={Uri.EscapeDataString("/hangfire/recurring")}&retry=1");
    }

    [Fact]
    public async Task Sign_in_goes_ahead_on_a_host_other_than_app_base_url_without_depending_on_the_proxy()
    {
        // Whether the Host the API sees is the public one depends on the load balancer, which it cannot know. A
        // mismatch is only warned about; the challenge is issued all the same.
        using var otherHost = fixture.BffWebApplicationFactory.CreateBrowser(
            baseAddress: new Uri("https://other.example.test:7111"));

        using var response = await otherHost.GetAsync("/auth/login", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().StartWith($"{PublicBase}/connect/authorize?");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
