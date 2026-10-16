using System.Net;
using System.Text.RegularExpressions;
using Auth.Domain.Auditing;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using Auth.Domain.Sessions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests.Bff;

// The second step of the browser sign-in: authenticator code, recovery code, remembered device, forced enrolment.
public partial class LoginFlowTests
{
    private const string CodePage = "/Account/TwoFactor";
    private const string SetupPage = "/Account/TwoFactorSetup";

    /// <summary>Starts a sign-in and posts the password. Returns the pending authorize URL and where the IdP sent the browser.</summary>
    private async Task<(string ReturnUrl, string Next)> PasswordStepAsync(HttpClient browser, TestUser user)
    {
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var returnUrl = Query(loginPage, "ReturnUrl");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, returnUrl);
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        return (returnUrl, Pending(posted.Headers.Location!));
    }

    /// <summary>Opens a two-factor page and posts <paramref name="code"/> to it, as the browser would.</summary>
    private async Task<HttpResponseMessage> PostCodeAsync(HttpClient browser, string pageUrl, string code) =>
        await PostCodeAsync(browser, pageUrl, code, await AntiforgeryTokenAsync(browser, pageUrl));

    private static async Task<HttpResponseMessage> PostCodeAsync(HttpClient browser, string pageUrl, string code, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, pageUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Code"] = code,
                ["ReturnUrl"] = Query(pageUrl, "returnUrl"),
                ["__RequestVerificationToken"] = token
            })
        };
        request.Headers.Add("Origin", PublicBase);
        return await browser.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>From the pending /connect/authorize through the callback; returns where the SPA is sent.</summary>
    private async Task<string> RedeemAsync(HttpClient browser, string authorizeUrl)
    {
        using var code = await browser.GetAsync(authorizeUrl, TestContext.Current.CancellationToken);
        code.StatusCode.Should().Be(HttpStatusCode.Found);
        using var signedIn = await browser.GetAsync(Pending(code.Headers.Location!), TestContext.Current.CancellationToken);
        signedIn.StatusCode.Should().Be(HttpStatusCode.Found);
        return signedIn.Headers.Location!.ToString();
    }

    private static async Task AssertSignedInAsync(HttpClient browser, TestUser user)
    {
        using var me = await browser.GetAsync("/auth/me", TestContext.Current.CancellationToken);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(me);
        json.RootElement.GetProperty("username").GetString().Should().Be(user.UserName);
    }

    private static async Task AssertNotSignedInAsync(HttpClient browser)
    {
        using var me = await browser.GetAsync("/auth/me", TestContext.Current.CancellationToken);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<List<(AuditAction Action, string? Changes)>> AuditAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var rows = await db.AuthAuditLogs.AsNoTracking().Where(a => a.EntityId == userId)
            .OrderBy(a => a.OccurredAt).Select(a => new { a.Action, a.ChangesJson }).ToListAsync();
        return rows.Select(r => (r.Action, r.ChangesJson)).ToList();
    }

    private async Task<TestUser> TwoFactorUserAsync(string role = "RequestMaker", Guid? companyId = null,
        Action<ApplicationUser>? configure = null)
    {
        var user = await BffTestSupport.CreateUserAsync(Services, role, companyId, configure: configure);
        await TotpTestSupport.EnableAsync(Services, user);
        return user;
    }

    [Fact]
    public async Task A_local_user_with_two_factor_is_asked_for_a_code_and_a_right_one_signs_them_in()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();

        var (returnUrl, next) = await PasswordStepAsync(browser, user);

        // The password alone gives nothing: no app session, and the authorize hop still wants a sign-in.
        next.Should().StartWith(CodePage).And.Contain("returnUrl=");
        await AssertNotSignedInAsync(browser);
        using (var early = await browser.GetAsync(returnUrl, TestContext.Current.CancellationToken))
            early.Headers.Location!.OriginalString.Should().Contain("/Account/Login");

        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        var resume = Pending(posted.Headers.Location!);
        resume.Should().StartWith("/connect/authorize");

        (await RedeemAsync(browser, resume)).Should().Be(BffTestSupport.Spa + "/");
        await AssertSignedInAsync(browser, user);

        // Post-2FA bookkeeping: last login stamped, one LoggedIn entry that names the second factor.
        await using var scope = Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id);
        stored.LastLoginAt.Should().NotBeNull();
        stored.AccessFailedCount.Should().Be(0);
        var loggedIn = (await AuditAsync(user.Id)).Single(a => a.Action == AuditAction.LoggedIn);
        loggedIn.Changes.Should().Contain("\"source\":\"Local\"").And.Contain("\"secondFactor\":\"totp\"");
    }

    [Fact]
    public async Task The_code_page_shows_no_code_back_and_carries_the_two_factor_cookie_under_its_own_name()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var posted = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));

        var cookie = posted.Headers.GetValues("Set-Cookie").Single(c => c.Contains("2fa"));
        cookie.Should().StartWith("__Host-cas-2fa-").And.NotContain("Identity.").And.Contain("secure")
            .And.Contain("httponly").And.Contain("samesite=lax").And.Contain("path=/");

        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        using var rejected = await PostCodeAsync(browser, Pending(posted.Headers.Location!), TotpTestSupport.WrongCode(key));
        // The input is rendered empty: what was typed is never sent back.
        (await rejected.Content.ReadAsStringAsync()).Should().NotMatchRegex("name=\"Code\"[^>]*value=");
    }

    [Fact]
    public async Task A_wrong_code_is_audited_counts_toward_lockout_and_gives_no_session()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var (_, next) = await PasswordStepAsync(browser, user);
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);

        using var rejected = await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key));

        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("The code is not valid");
        await AssertNotSignedInAsync(browser);
        (await AuditAsync(user.Id)).Should().Contain(a =>
            a.Action == AuditAction.LoginFailed && a.Changes!.Contains("InvalidTwoFactorCode"));
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).AccessFailedCount.Should().Be(1);
    }

    [Fact]
    public async Task Wrong_codes_lock_the_account_and_a_right_code_is_then_refused()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var (_, next) = await PasswordStepAsync(browser, user);
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);

        // The default policy locks at five failures.
        for (var i = 0; i < 5; i++)
            (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        using var refused = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        refused.StatusCode.Should().Be(HttpStatusCode.OK);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Account locked out");
        await AssertNotSignedInAsync(browser);
        (await AuditAsync(user.Id)).Should().Contain(a =>
            a.Action == AuditAction.LoginFailed && a.Changes!.Contains("LockedOut"));
    }

    [Fact]
    public async Task Signing_in_again_does_not_wipe_the_failed_code_count()
    {
        // Otherwise someone who knows the password could guess codes forever: sign in, guess four, sign in, ...
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);

        var (_, next) = await PasswordStepAsync(browser, user);
        for (var i = 0; i < 2; i++)
            (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();
        await PasswordStepAsync(browser, user);

        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).AccessFailedCount.Should().Be(2);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once_and_is_then_spent()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var codes = await TotpTestSupport.EnableAsync(Services, user);

        var (_, next) = await PasswordStepAsync(browser, user);
        using var posted = await PostCodeAsync(browser, next + "&recovery=true", codes[0]);
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);

        var audit = await AuditAsync(user.Id);
        audit.Should().Contain(a => a.Action == AuditAction.RecoveryCodeUsed);
        audit.Single(a => a.Action == AuditAction.LoggedIn).Changes.Should().Contain("\"secondFactor\":\"recovery\"");

        // A fresh browser: the same code no longer works, and the failure is audited like a wrong code.
        using var other = Browser();
        var (_, again) = await PasswordStepAsync(other, user);
        using var reused = await PostCodeAsync(other, again + "&recovery=true", codes[0]);
        reused.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertNotSignedInAsync(other);

        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CountRecoveryCodesAsync((await users.FindByIdAsync(user.Id.ToString()))!)).Should().Be(9);
    }

    [Fact]
    public async Task A_remembered_device_skips_the_code_page_until_the_cookie_is_gone()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        var remember = posted.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-cas-2fa-remember"));
        remember.Should().Contain("secure").And.Contain("httponly").And.Contain("samesite=lax").And.Contain("path=/");
        // Policy default: 12 hours, an absolute date rather than a session cookie.
        var expires = DateTimeOffset.Parse(Regex.Match(remember, "expires=([^;]+)").Groups[1].Value);
        expires.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(12), TimeSpan.FromMinutes(5));

        // Same browser, new sign-in: the password is enough.
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith("/connect/authorize");

        // A browser without the cookie is asked.
        using var other = Browser();
        (await PasswordStepAsync(other, user)).Next.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task With_remember_hours_at_zero_the_code_is_asked_every_time_and_no_cookie_is_written()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(Services, "TwoFactorRememberHours = 0", "TwoFactorRememberHours = 12");
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        posted.Headers.GetValues("Set-Cookie").Should().NotContain(c => c.StartsWith("__Host-cas-2fa-remember"));
        var (_, second) = await PasswordStepAsync(browser, user);
        second.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task An_external_user_is_made_to_enrol_then_sees_the_recovery_codes_and_is_signed_in()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());

        var (returnUrl, next) = await PasswordStepAsync(browser, user);

        next.Should().StartWith(SetupPage);
        await AssertNotSignedInAsync(browser);
        using var page = await browser.GetAsync(next, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("<svg").And.Contain("autocomplete=\"one-time-code\"");
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        // Each group of four is its own span, so a line can only break between groups; single spaces keep a copy readable.
        html.Should().Contain(string.Join(' ',
            key.ToLowerInvariant().Chunk(4).Select(c => $"<span class=\"enrol-key-group\">{new string(c)}</span>")));

        // A wrong code keeps the user on the page and enables nothing.
        using (var wrong = await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key)))
        {
            wrong.StatusCode.Should().Be(HttpStatusCode.OK);
            (await wrong.Content.ReadAsStringAsync()).Should().Contain("The code is not valid").And.Contain("<svg");
        }
        await AssertNotSignedInAsync(browser);

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        posted.Headers.CacheControl!.NoStore.Should().BeTrue();
        var done = await posted.Content.ReadAsStringAsync();
        Regex.Matches(done, "<li>[^<]+</li>").Should().HaveCount(10);
        // Copy and download work in the browser, from the list on the page: both buttons are there (hidden until the script
        // shows them) with a polite live region for the "copied" message, and the codes appear in the HTML once each,
        // in the list, not again in the script.
        done.Should().Contain("id=\"recovery-copy\"").And.Contain("id=\"recovery-download\"")
            .And.Contain("Copy all").And.Contain("Download .txt").And.Contain("aria-label=\"Your recovery codes\"")
            .And.Contain("role=\"status\" aria-live=\"polite\"");
        // Server-rendered hidden: without JS the page never shows buttons that do nothing.
        Regex.IsMatch(done, "<div[^>]*id=\"recovery-actions\"[^>]*\\shidden[\\s>]").Should().BeTrue();
        Regex.Matches(done, "<li>([^<]+)</li>").Select(m => m.Groups[1].Value)
            .Should().OnlyContain(code => Regex.Matches(done, Regex.Escape(code)).Count == 1);
        var proceed = Regex.Match(done, "<a class=\"login-button\" href=\"([^\"]+)\"").Groups[1].Value;
        proceed = WebUtility.HtmlDecode(proceed);
        proceed.Should().Be(returnUrl);

        await RedeemAsync(browser, proceed);
        await AssertSignedInAsync(browser, user);
        var audit = await AuditAsync(user.Id);
        audit.Should().Contain(a => a.Action == AuditAction.TwoFactorEnabled);
        audit.Single(a => a.Action == AuditAction.LoggedIn).Changes.Should().Contain("\"secondFactor\":\"enrolled\"");

        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task An_internal_user_is_not_made_to_enrol_when_the_policy_is_on()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");

        var (_, next) = await PasswordStepAsync(browser, user);

        next.Should().StartWith("/connect/authorize");
    }

    [Fact]
    public async Task The_enrolment_page_is_no_way_past_the_code_page_for_a_user_who_has_two_factor()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (returnUrl, next) = await PasswordStepAsync(browser, user);
        var setup = $"{SetupPage}?returnUrl={Uri.EscapeDataString(returnUrl)}";

        using (var get = await browser.GetAsync(setup, TestContext.Current.CancellationToken))
            Pending(get.Headers.Location!).Should().StartWith(CodePage);

        // Posting a valid code there must not sign anyone in either.
        var token = await AntiforgeryTokenAsync(browser, next);
        var request = new HttpRequestMessage(HttpMethod.Post, setup)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Code"] = TotpTestSupport.Code(key), ["ReturnUrl"] = returnUrl, ["__RequestVerificationToken"] = token
            })
        };
        request.Headers.Add("Origin", PublicBase);
        using var post = await browser.SendAsync(request, TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(post.Headers.Location!).Should().StartWith(CodePage);
        await AssertNotSignedInAsync(browser);
    }

    [Fact]
    public async Task The_two_factor_pages_without_a_parked_user_or_a_proper_return_url_go_elsewhere()
    {
        using var browser = Browser();

        using var noUser = await browser.GetAsync(
            $"{CodePage}?returnUrl={Uri.EscapeDataString("/connect/authorize?x=1")}", TestContext.Current.CancellationToken);
        Pending(noUser.Headers.Location!).Should().StartWith("/Account/Login");

        foreach (var page in new[] { CodePage, SetupPage })
        {
            using var noReturn = await browser.GetAsync(page, TestContext.Current.CancellationToken);
            noReturn.StatusCode.Should().Be(HttpStatusCode.Found);
            noReturn.Headers.Location!.ToString().Should().NotContain("Account");
            using var offsite = await browser.GetAsync($"{page}?returnUrl=https://evil.example/", TestContext.Current.CancellationToken);
            offsite.Headers.Location!.ToString().Should().NotContain("evil.example");
        }
    }

    [Fact]
    public async Task An_ldap_user_with_two_factor_gets_the_same_code_page()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);

        var (_, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(CodePage);

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);
        (await AuditAsync(user.Id)).Single(a => a.Action == AuditAction.LoggedIn).Changes
            .Should().Contain("\"source\":\"Ldap\"").And.Contain("\"secondFactor\":\"totp\"");
    }

    [Fact]
    public async Task An_ldap_user_without_two_factor_still_signs_in_with_the_bind_alone()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: u => u.AuthSource = AuthSources.Ldap);

        var (_, next) = await PasswordStepAsync(browser, user);

        next.Should().StartWith("/connect/authorize");
        await RedeemAsync(browser, next);
        await AssertSignedInAsync(browser, user);
    }

    [Fact]
    public async Task A_deactivated_account_is_refused_at_the_code_page()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);
        var token = await AntiforgeryTokenAsync(browser, next);

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);
        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key), token);

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(posted.Headers.Location!).Should().StartWith("/Account/Login");
        await AssertNotSignedInAsync(browser);
    }

    [Fact]
    public async Task An_admin_reset_ends_the_sessions_and_the_remember_cookie_stops_working()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);
        using (var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key)))
            await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);

        string stampBefore;
        await using (var scope = Services.CreateAsyncScope())
            stampBefore = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(user.Id.ToString()))!.SecurityStamp!;
        var admin = await BffTestSupport.CreateUserAsync(Services, "Admin");
        using var adminBrowser = Browser();
        var adminCookie = await BffTestSupport.SignInAsync(adminBrowser, admin);
        using var reset = await BffTestSupport.SendAsync(adminBrowser,
            BffTestSupport.SpaRequest(HttpMethod.Post, $"/auth/users/{user.Id}/two-factor/reset"), adminCookie);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AssertNotSignedInAsync(browser); // the session was revoked
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Revoked);
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = (await users.FindByIdAsync(user.Id.ToString()))!;
            stored.TwoFactorEnabled.Should().BeFalse();
            (await users.CountRecoveryCodesAsync(stored)).Should().Be(0);
            (await users.GetAuthenticatorKeyAsync(stored)).Should().BeNull(); // removed, not replaced
            stored.SecurityStamp.Should().NotBe(stampBefore);
        }
        (await AuditAsync(user.Id)).Should().Contain(a => a.Action == AuditAction.TwoFactorReset);

        // Without two-factor the password is enough.
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith("/connect/authorize");

        // Turn it on again on a new key: the old remember cookie, still in this browser, must not count.
        await TotpTestSupport.EnableAsync(Services, user);
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task A_recovery_code_is_accepted_whatever_case_it_is_typed_in()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var codes = await TotpTestSupport.EnableAsync(Services, user);

        var (_, next) = await PasswordStepAsync(browser, user);
        var typed = new string(codes[0].Select((c, i) => i % 2 == 0 ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)).ToArray());
        using var posted = await PostCodeAsync(browser, next + "&recovery=true", " " + typed + " ");

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);
    }

    [Fact]
    public async Task The_code_page_is_no_way_in_for_a_user_who_is_parked_to_enrol()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        var (returnUrl, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(SetupPage);
        var token = await AntiforgeryTokenAsync(browser, next); // the setup page creates the key
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var codePage = $"{CodePage}?returnUrl={Uri.EscapeDataString(returnUrl)}";

        using (var get = await browser.GetAsync(codePage, TestContext.Current.CancellationToken))
            Pending(get.Headers.Location!).Should().StartWith(SetupPage);

        var request = new HttpRequestMessage(HttpMethod.Post, codePage)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Code"] = TotpTestSupport.Code(key), ["ReturnUrl"] = returnUrl, ["__RequestVerificationToken"] = token
            })
        };
        request.Headers.Add("Origin", PublicBase);
        using var post = await browser.SendAsync(request, TestContext.Current.CancellationToken);

        post.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(post.Headers.Location!).Should().StartWith(SetupPage);
        await AssertNotSignedInAsync(browser);
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeFalse();
    }

    private async Task RememberDeviceAsync(HttpClient browser, TestUser user)
    {
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);
        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        posted.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("__Host-cas-2fa-remember"));
    }

    [Fact]
    public async Task A_password_change_rotates_the_stamp_and_the_remember_cookie_stops_counting()
    {
        // Two-factor stays on throughout: only the security stamp changes.
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        await RememberDeviceAsync(browser, user);
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith("/connect/authorize");

        const string newPassword = "Str0ng!Passw0rd#43";
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = (await users.FindByIdAsync(user.Id.ToString()))!;
            (await users.ChangePasswordAsync(stored, user.Password, newPassword)).Succeeded.Should().BeTrue();
            stored.TwoFactorEnabled.Should().BeTrue();
        }

        (await PasswordStepAsync(browser, user with { Password = newPassword })).Next.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task One_users_remember_cookie_does_not_let_another_user_skip_the_code()
    {
        using var browser = Browser();
        var a = await TwoFactorUserAsync();
        var b = await TwoFactorUserAsync();
        await RememberDeviceAsync(browser, a);

        (await PasswordStepAsync(browser, a)).Next.Should().StartWith("/connect/authorize");
        (await PasswordStepAsync(browser, b)).Next.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task An_ldap_user_locked_out_by_wrong_codes_is_refused_at_the_bind()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);
        for (var i = 0; i < 5; i++)
            (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        using var again = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));

        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await again.Content.ReadAsStringAsync()).Should().Contain("Account locked out");
    }

    [Fact]
    public async Task Enrolling_after_failed_binds_clears_the_failed_count()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid(),
            configure: u => u.AuthSource = AuthSources.Ldap);
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var returnUrl = Query(loginPage, "ReturnUrl");
        var token = await AntiforgeryTokenAsync(browser, loginPage);
        for (var i = 0; i < 4; i++)
        {
            using var wrong = await PostLoginAsync(browser, token, user, returnUrl, "not-the-password");
            wrong.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        await using (var scope = Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
                .SingleAsync(u => u.Id == user.Id)).AccessFailedCount.Should().Be(4);

        var (_, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(SetupPage);
        using var page = await browser.GetAsync(next, TestContext.Current.CancellationToken); // creates the key
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        using var enrolled = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));
        enrolled.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var after = Services.CreateAsyncScope();
        (await after.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).AccessFailedCount.Should().Be(0);
    }

    /// <summary>An e-mail Identity's validator rejects: every <c>UserManager</c> update of this account then comes back failed.</summary>
    private Task MakeUpdatesFailAsync(TestUser user) =>
        BffTestSupport.UpdateUserAsync(Services, user.Id, u =>
        {
            u.Email = $"not-an-email-{user.Id:N}";
            u.NormalizedEmail = u.Email.ToUpperInvariant();
        });

    [Fact]
    public async Task When_identity_refuses_the_update_the_setup_page_shows_an_error_and_no_qr_code()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        await MakeUpdatesFailAsync(user);
        var (_, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(SetupPage);

        using var page = await browser.GetAsync(next, TestContext.Current.CancellationToken);

        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.Should().Contain("could not be set up").And.NotContain("<svg class=\"enrol").And.NotContain("one-time-code");
        await AssertNotSignedInAsync(browser);
    }

    [Fact]
    public async Task A_blank_code_is_just_an_invalid_code()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var (_, next) = await PasswordStepAsync(browser, user);

        foreach (var blank in new[] { "", "   " })
        {
            using var posted = await PostCodeAsync(browser, next + "&recovery=true", blank);
            posted.StatusCode.Should().Be(HttpStatusCode.OK);
            (await posted.Content.ReadAsStringAsync()).Should().Contain("The code is not valid");
        }
        foreach (var blank in new[] { "", "   " })
        {
            using var posted = await PostCodeAsync(browser, next, blank);
            posted.StatusCode.Should().Be(HttpStatusCode.OK);
            (await posted.Content.ReadAsStringAsync()).Should().Contain("The code is not valid");
        }
        await AssertNotSignedInAsync(browser);
    }

    [Fact]
    public async Task Setting_the_remember_hours_to_zero_ends_devices_remembered_under_the_old_policy()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        await RememberDeviceAsync(browser, user);
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith("/connect/authorize");

        await using var _ = await TotpTestSupport.SetPolicyAsync(Services, "TwoFactorRememberHours = 0", "TwoFactorRememberHours = 12");

        (await PasswordStepAsync(browser, user)).Next.Should().StartWith(CodePage);
    }

    [Fact]
    public async Task A_user_parked_on_the_code_page_whose_two_factor_was_reset_is_sent_back_to_login_not_to_enrol()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync(); // opted in; the policy does not require it
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (returnUrl, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(CodePage);
        var token = await AntiforgeryTokenAsync(browser, next);

        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await scope.ServiceProvider.GetRequiredService<global::Auth.Application.Services.TwoFactorService>()
                .ResetAsync((await users.FindByIdAsync(user.Id.ToString()))!);
        }

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key), token);
        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(posted.Headers.Location!).Should().StartWith("/Account/Login");
        await AssertNotSignedInAsync(browser);

        // The parked cookie was dropped, so the enrolment page has nobody either; and Login now lets the password through.
        using var setup = await browser.GetAsync($"{SetupPage}?returnUrl={Uri.EscapeDataString(returnUrl)}",
            TestContext.Current.CancellationToken);
        Pending(setup.Headers.Location!).Should().StartWith("/Account/Login");
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith("/connect/authorize");
    }

    [Fact]
    public async Task The_enrolment_page_is_not_for_a_user_the_policy_does_not_require()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var (returnUrl, _) = await PasswordStepAsync(browser, user); // parked on the code page, with two-factor on
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await scope.ServiceProvider.GetRequiredService<global::Auth.Application.Services.TwoFactorService>()
                .ResetAsync((await users.FindByIdAsync(user.Id.ToString()))!);
        }

        using var setup = await browser.GetAsync($"{SetupPage}?returnUrl={Uri.EscapeDataString(returnUrl)}",
            TestContext.Current.CancellationToken);

        Pending(setup.Headers.Location!).Should().StartWith("/Account/Login");
    }

    [Fact]
    public async Task A_locked_out_user_cannot_finish_enrolment()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        var (_, next) = await PasswordStepAsync(browser, user);
        using (await browser.GetAsync(next, TestContext.Current.CancellationToken)) { } // creates the key
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1));

        using var posted = await PostCodeAsync(browser, next, TotpTestSupport.Code(key));

        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await posted.Content.ReadAsStringAsync()).Should().Contain("Account locked out");
        await AssertNotSignedInAsync(browser);
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeFalse();
        (await AuditAsync(user.Id)).Should().Contain(a => a.Action == AuditAction.LoginFailed && a.Changes!.Contains("LockedOut"));
    }

    /// <summary>An admin opens a new access window on the account (a PUT, as the admin screen does). Returns the new password.</summary>
    private async Task<string> OpenAccessWindowAsync(TestUser account)
    {
        var admin = await BffTestSupport.CreateUserAsync(Services, "Admin");
        using var adminBrowser = Browser();
        var cookie = await BffTestSupport.SignInAsync(adminBrowser, admin);
        var request = BffTestSupport.SpaRequest(HttpMethod.Put, $"/auth/users/{account.Id}/access-window");
        request.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            expiresAt = Now(Services).AddHours(2), reason = "test", extendOnly = false
        });
        using var response = await BffTestSupport.SendAsync(adminBrowser, request, cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var json = await BffTestSupport.ReadJsonAsync(response);
        return json.RootElement.GetProperty("password").GetString()!;
    }

    private async Task AssertTwoFactorClearedAsync(TestUser account)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByIdAsync(account.Id.ToString()))!;
        stored.TwoFactorEnabled.Should().BeFalse();
        (await users.CountRecoveryCodesAsync(stored)).Should().Be(0);
        (await AuditAsync(account.Id)).Should().Contain(a =>
            a.Action == AuditAction.TwoFactorReset && a.Changes!.Contains("AccessWindow"));
    }

    [Fact]
    public async Task A_new_access_window_clears_the_previous_holders_two_factor_and_the_next_holder_just_signs_in()
    {
        using var browser = Browser();
        var account = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: u => u.IsTemporaryAccess = true);
        await TotpTestSupport.EnableAsync(Services, account);
        var oldKey = await TotpTestSupport.KeyOfAsync(Services, account.Id);

        var password = await OpenAccessWindowAsync(account);

        await AssertTwoFactorClearedAsync(account);
        (await TotpTestSupport.KeyOfAsync(Services, account.Id)).Should().BeNull(); // removed, not replaced
        oldKey.Should().NotBeNullOrEmpty();
        (await PasswordStepAsync(browser, account with { Password = password })).Next.Should().StartWith("/connect/authorize");
    }

    [Fact]
    public async Task A_new_access_window_on_an_external_account_sends_the_next_holder_to_enrol()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var account = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid(),
            configure: u => u.IsTemporaryAccess = true);
        await TotpTestSupport.EnableAsync(Services, account);

        var password = await OpenAccessWindowAsync(account);

        await AssertTwoFactorClearedAsync(account);
        (await PasswordStepAsync(browser, account with { Password = password })).Next.Should().StartWith(SetupPage);
    }

    [Fact]
    public async Task Extending_or_closing_an_access_window_leaves_two_factor_alone()
    {
        var account = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: u => u.IsTemporaryAccess = true);
        await TotpTestSupport.EnableAsync(Services, account);
        await OpenAccessWindowAsync(account); // clears it; switch it back on for the two cases below
        await TotpTestSupport.EnableAsync(Services, account);

        var admin = await BffTestSupport.CreateUserAsync(Services, "Admin");
        using var adminBrowser = Browser();
        var cookie = await BffTestSupport.SignInAsync(adminBrowser, admin);
        foreach (var (expires, extend) in new[] { (Now(Services).AddHours(3), true), (Now(Services).AddMinutes(-1), false) })
        {
            var request = BffTestSupport.SpaRequest(HttpMethod.Put, $"/auth/users/{account.Id}/access-window");
            request.Content = System.Net.Http.Json.JsonContent.Create(new { expiresAt = expires, reason = "test", extendOnly = extend });
            using var response = await BffTestSupport.SendAsync(adminBrowser, request, cookie);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(account.Id.ToString()))!.TwoFactorEnabled.Should().BeTrue();
    }

    private async Task ChangePasswordAsync(TestUser user, string newPassword)
    {
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.ChangePasswordAsync((await users.FindByIdAsync(user.Id.ToString()))!, user.Password, newPassword))
            .Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task A_password_change_between_the_two_steps_voids_the_parked_user_on_the_code_page()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        var (_, next) = await PasswordStepAsync(browser, user);
        var token = await AntiforgeryTokenAsync(browser, next);

        await ChangePasswordAsync(user, "Str0ng!Passw0rd#43");

        using (var get = await browser.GetAsync(next, TestContext.Current.CancellationToken))
            Pending(get.Headers.Location!).Should().StartWith("/Account/Login");
        using var post = await PostCodeAsync(browser, next, TotpTestSupport.Code(key), token);
        post.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(post.Headers.Location!).Should().StartWith("/Account/Login");
        await AssertNotSignedInAsync(browser);
    }

    [Fact]
    public async Task A_password_change_between_the_two_steps_voids_the_parked_user_on_the_enrolment_page()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        var (_, next) = await PasswordStepAsync(browser, user);
        // Opening the page creates the key, which rotates the stamp: the page must keep its own parked cookie valid.
        var token = await AntiforgeryTokenAsync(browser, next);
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        using (var again = await browser.GetAsync(next, TestContext.Current.CancellationToken))
            again.StatusCode.Should().Be(HttpStatusCode.OK);

        await ChangePasswordAsync(user, "Str0ng!Passw0rd#43");

        using (var get = await browser.GetAsync(next, TestContext.Current.CancellationToken))
            Pending(get.Headers.Location!).Should().StartWith("/Account/Login");
        using var post = await PostCodeAsync(browser, next, TotpTestSupport.Code(key), token);
        Pending(post.Headers.Location!).Should().StartWith("/Account/Login");
        await AssertNotSignedInAsync(browser);
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_codes_survive_signing_in_again_after_the_remember_hours_were_lowered()
    {
        using var browser = Browser();
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id);
        await RememberDeviceAsync(browser, user); // remembered under the 12 h policy
        await using var _ = await TotpTestSupport.SetPolicyAsync(Services, "TwoFactorRememberHours = 0", "TwoFactorRememberHours = 12");

        var (_, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(CodePage);
        for (var i = 0; i < 4; i++) // one short of the default lockout of five
            (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        // Signing in again must not wipe those four.
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith(CodePage);
        (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).LockoutEnd.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task A_recovery_code_typed_in_lower_case_and_without_its_hyphen_works()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var codes = await TotpTestSupport.EnableAsync(Services, user);
        codes.Should().OnlyContain(c => Regex.IsMatch(c, "^[A-Z0-9]{5}-[A-Z0-9]{5}$"));

        var (_, next) = await PasswordStepAsync(browser, user);
        using var posted = await PostCodeAsync(browser, next + "&recovery=true", codes[1].Replace("-", "").ToLowerInvariant());

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);
    }

    [Fact]
    public async Task A_new_access_window_leaves_an_account_without_two_factor_or_key_alone()
    {
        var account = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: u => u.IsTemporaryAccess = true);

        await OpenAccessWindowAsync(account);

        (await TotpTestSupport.KeyOfAsync(Services, account.Id)).Should().BeNull();
        (await AuditAsync(account.Id)).Should().NotContain(a => a.Action == AuditAction.TwoFactorReset);
    }

    [Fact]
    public async Task A_new_access_window_discards_a_half_finished_enrolments_key_without_an_audit_row()
    {
        var account = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: u => u.IsTemporaryAccess = true);
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await scope.ServiceProvider.GetRequiredService<global::Auth.Application.Services.TwoFactorService>()
                .BeginSetupAsync((await users.FindByIdAsync(account.Id.ToString()))!);
        }
        var unverified = await TotpTestSupport.KeyOfAsync(Services, account.Id);

        await OpenAccessWindowAsync(account);

        unverified.Should().NotBeNullOrEmpty();
        (await TotpTestSupport.KeyOfAsync(Services, account.Id)).Should().BeNull();
        (await AuditAsync(account.Id)).Should().NotContain(a => a.Action == AuditAction.TwoFactorReset);
    }

    [Fact]
    public async Task Reloading_the_enrolment_page_does_not_renew_the_parked_cookie()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "ExtAppraisalStaff", companyId: Guid.NewGuid());
        var (_, next) = await PasswordStepAsync(browser, user);

        static bool Parked(string cookie) =>
            cookie.StartsWith("__Host-cas-2fa-") && !cookie.StartsWith("__Host-cas-2fa-remember");
        IEnumerable<string> SetCookies(HttpResponseMessage r) =>
            r.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        // The first visit creates the key, which rotates the stamp: the cookie is issued again, once.
        using (var first = await browser.GetAsync(next, TestContext.Current.CancellationToken))
            SetCookies(first).Any(Parked).Should().BeTrue();
        // Any later visit leaves the cookie, and so its five minutes, alone.
        for (var i = 0; i < 2; i++)
        {
            using var again = await browser.GetAsync(next, TestContext.Current.CancellationToken);
            again.StatusCode.Should().Be(HttpStatusCode.OK);
            SetCookies(again).Any(Parked).Should().BeFalse();
        }
    }

    [Fact]
    public async Task The_recovery_attempt_that_locks_the_account_says_so()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await TotpTestSupport.EnableAsync(Services, user);
        var (_, next) = await PasswordStepAsync(browser, user);

        // The default policy locks at five failures.
        for (var i = 0; i < 4; i++)
        {
            using var wrong = await PostCodeAsync(browser, next + "&recovery=true", "AAAAA-BBBBB");
            (await wrong.Content.ReadAsStringAsync()).Should().Contain("The code is not valid");
        }
        using var fifth = await PostCodeAsync(browser, next + "&recovery=true", "AAAAA-BBBBB");

        (await fifth.Content.ReadAsStringAsync()).Should().Contain("Account locked out");
        (await AuditAsync(user.Id)).Should().Contain(a => a.Action == AuditAction.LoginFailed && a.Changes!.Contains("LockedOut"));
        await AssertNotSignedInAsync(browser);
    }

    /// <summary>
    /// A client that never honours Set-Cookie: it keeps sending every cookie it was ever given, including one the server
    /// asked it to delete (an attacker's script, or a browser extension). Cookies are kept by name only, so it has no
    /// domain, path or expiry logic that could lose the old remember cookie.
    /// </summary>
    private sealed class StickyCookieHandler : DelegatingHandler
    {
        private readonly Dictionary<string, string> _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_cookies.Count > 0)
                request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                foreach (var setCookie in setCookies)
                {
                    var pair = setCookie.Split(';')[0].Split('=', 2);
                    var deletion = pair[1].Length == 0 || setCookie.Contains("expires=Thu, 01 Jan 1970");
                    if (deletion && pair[0].StartsWith("__Host-cas-2fa-remember")) continue; // ignores the deletion
                    if (deletion) _cookies.Remove(pair[0]); else _cookies[pair[0]] = pair[1];
                }
            return response;
        }
    }

    [Fact]
    public async Task A_client_that_keeps_sending_a_deleted_remember_cookie_cannot_reset_the_failed_code_count()
    {
        using var browser = fixture.BffWebApplicationFactory.CreateDefaultClient(
            global::Integration.WebApplicationFactories.BffWebApplicationFactory.BaseAddress, new StickyCookieHandler());
        var user = await TwoFactorUserAsync();
        var key = await TotpTestSupport.KeyOfAsync(Services, user.Id) ?? throw new InvalidOperationException();
        await RememberDeviceAsync(browser, user); // remembered under the 12 h policy
        await using var _ = await TotpTestSupport.SetPolicyAsync(Services, "TwoFactorRememberHours = 0", "TwoFactorRememberHours = 12");

        var (_, next) = await PasswordStepAsync(browser, user);
        next.Should().StartWith(CodePage); // the old cookie no longer counts
        for (var i = 0; i < 4; i++) // one short of the default lockout of five
            (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        // The correct password with the old cookie still attached: no reset.
        (await PasswordStepAsync(browser, user)).Next.Should().StartWith(CodePage);
        (await PostCodeAsync(browser, next, TotpTestSupport.WrongCode(key))).Dispose();

        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id)).LockoutEnd.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow);
    }
}
