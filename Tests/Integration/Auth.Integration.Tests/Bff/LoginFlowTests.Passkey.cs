using System.Net;
using System.Text.RegularExpressions;
using Auth.Application.Configurations;
using Auth.Domain.Auditing;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Integration.WebApplicationFactories;
using Microsoft.Extensions.Options;

namespace Integration.Auth.Integration.Tests.Bff;

// Sign-in with a passkey: the IdP's Account/Passkey page, driven the way the browser drives it.
public partial class LoginFlowTests
{
    private const string PasskeyPage = "/Account/Passkey";

    private BffWebApplicationFactory Factory => fixture.BffWebApplicationFactory;

    /// <summary>Starts a sign-in that asks for a passkey and returns the pending authorize URL and where the IdP's login page sent the browser.</summary>
    private async Task<(string ReturnUrl, string Next)> StartPasskeyAsync(HttpClient browser, string? method = "passkey")
    {
        using var login = await browser.GetAsync(
            "/auth/login?returnUrl=" + Uri.EscapeDataString(BffTestSupport.Spa + "/")
            + (method is null ? "" : "&method=" + Uri.EscapeDataString(method)), TestContext.Current.CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.Found);
        using var hop = await browser.GetAsync(Pending(login.Headers.Location!), TestContext.Current.CancellationToken);
        hop.StatusCode.Should().Be(HttpStatusCode.Found);
        var loginPage = Pending(hop.Headers.Location!);
        var returnUrl = Query(loginPage, "ReturnUrl");

        using var landing = await browser.GetAsync(loginPage, TestContext.Current.CancellationToken);
        return (returnUrl, landing.StatusCode == HttpStatusCode.Found ? Pending(landing.Headers.Location!) : loginPage);
    }

    /// <summary>What the passkey page hands the browser: the options to run, the sealed state, and the antiforgery token.</summary>
    private sealed record PasskeyChallenge(string OptionsJson, string State, string Token, string Html);

    private static async Task<PasskeyChallenge> OpenPasskeyPageAsync(HttpClient browser, string pageUrl)
    {
        using var page = await browser.GetAsync(pageUrl, TestContext.Current.CancellationToken);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        return ParseChallenge(await page.Content.ReadAsStringAsync());
    }

    private static PasskeyChallenge ParseChallenge(string html) => new(
        WebUtility.HtmlDecode(Regex.Match(html, "data-options=\"([^\"]*)\"").Groups[1].Value),
        WebUtility.HtmlDecode(Regex.Match(html, "name=\"State\" value=\"([^\"]*)\"").Groups[1].Value),
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value,
        html);

    private static async Task<HttpResponseMessage> PostPasskeyAsync(HttpClient browser, string pageUrl, PasskeyChallenge challenge,
        string credential, string? state = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, pageUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ReturnUrl"] = Query(pageUrl, "returnUrl"),
                ["State"] = state ?? challenge.State,
                ["Credential"] = credential,
                ["__RequestVerificationToken"] = challenge.Token
            })
        };
        request.Headers.Add("Origin", PublicBase);
        return await browser.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(TestUser User, SoftAuthenticator Authenticator)> UserWithPasskeyAsync(string role = "RequestMaker",
        Guid? companyId = null, Action<ApplicationUser>? configure = null, Action<SoftAuthenticator>? tweak = null)
    {
        var user = await BffTestSupport.CreateUserAsync(Services, role, companyId, configure: configure);
        var (authenticator, _) = await PasskeyTestSupport.RegisterAsync(Factory, user, configure: tweak);
        return (user, authenticator);
    }

    private async Task<string> SignInWithPasskeyAsync(HttpClient browser, SoftAuthenticator authenticator)
    {
        var (_, next) = await StartPasskeyAsync(browser);
        next.Should().StartWith(PasskeyPage);
        var challenge = await OpenPasskeyPageAsync(browser, next);
        using var posted = await PostPasskeyAsync(browser, next, challenge, authenticator.Get(challenge.OptionsJson, PasskeyTestSupport.IdpOrigin));
        posted.StatusCode.Should().Be(HttpStatusCode.Found, await posted.Content.ReadAsStringAsync());
        return Pending(posted.Headers.Location!);
    }

    [Fact]
    public async Task A_passkey_signs_the_user_in_without_a_password()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync();

        var resume = await SignInWithPasskeyAsync(browser, authenticator);

        resume.Should().StartWith("/connect/authorize");
        (await RedeemAsync(browser, resume)).Should().Be(BffTestSupport.Spa + "/");
        await AssertSignedInAsync(browser, user);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    private async Task<int> CountAuditAsync(string fragment)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        return await db.AuthAuditLogs.AsNoTracking()
            .CountAsync(a => a.Action == AuditAction.LoginFailed && a.ChangesJson!.Contains(fragment));
    }

    private async Task<global::Auth.Domain.Passkeys.UserPasskey> StoredPasskeyAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuthDbContext>().UserPasskeys.AsNoTracking()
            .SingleAsync(p => p.UserId == userId);
    }

    /// <summary>
    /// Runs a passkey sign-in that must be turned away: the page answers 200 with the one generic message, the browser has no
    /// session, and the audit log says why. <paramref name="respond"/> builds the authenticator's answer (default: the honest one).
    /// </summary>
    private async Task AssertPasskeyRefusedAsync(SoftAuthenticator authenticator, string reason,
        Func<SoftAuthenticator, PasskeyChallenge, string>? respond = null, Func<PasskeyChallenge, string>? state = null,
        bool signalsUnknownCredential = false)
    {
        using var browser = Browser();
        var before = await CountAuditAsync(reason);
        var (_, next) = await StartPasskeyAsync(browser);
        var challenge = await OpenPasskeyPageAsync(browser, next);
        var credential = respond?.Invoke(authenticator, challenge)
                         ?? authenticator.Get(challenge.OptionsJson, PasskeyTestSupport.IdpOrigin);

        using var posted = await PostPasskeyAsync(browser, next, challenge, credential, state?.Invoke(challenge));

        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await posted.Content.ReadAsStringAsync();
        html.Should().Contain("Passkey sign-in did not work").And.Contain("data-options="); // generic, with a fresh challenge
        html.Should().Contain(" data-failed", "the page waits for \"Try again\" instead of prompting again at once");
        // The status line itself (not the script's text table, which every render carries): neutral, no prompt to follow.
        StatusLine(html).Should().Be("Press Try again when you are ready.");
        html.Should().NotContain(reason); // the page never says why
        if (signalsUnknownCredential)
            html.Should().Contain($"data-unknown-credential=\"{SoftAuthenticator.B64(authenticator.CredentialId)}\"");
        else
            html.Should().NotContain("data-unknown-credential=\"", "only an unknown credential is signalled to the browser");
        await AssertNotSignedInAsync(browser);
        (await CountAuditAsync(reason)).Should().BeGreaterThan(before, $"the audit log should carry {reason}");
    }

    /// <summary>The text of the page's <c>#passkey-status</c> element, as the server rendered it.</summary>
    private static string StatusLine(string html) =>
        WebUtility.HtmlDecode(Regex.Match(html, "<p[^>]*id=\"passkey-status\"[^>]*>(.*?)</p>", RegexOptions.Singleline).Groups[1].Value).Trim();

    private static string Honest(SoftAuthenticator a, PasskeyChallenge c) => a.Get(c.OptionsJson, PasskeyTestSupport.IdpOrigin);

    // ---- success ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_passkey_sign_in_is_audited_remembered_as_the_last_method_and_stamps_the_passkey()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync();
        var (_, next) = await StartPasskeyAsync(browser);
        var challenge = await OpenPasskeyPageAsync(browser, next);

        using var posted = await PostPasskeyAsync(browser, next, challenge, Honest(authenticator, challenge));

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        var method = posted.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("cas-login-method="));
        method.Should().StartWith("cas-login-method=passkey;")
            .And.Contain("max-age=31536000").And.Contain("path=/").And.Contain("samesite=lax").And.Contain("secure")
            .And.NotContain("httponly");

        var loggedIn = (await AuditAsync(user.Id)).Single(a => a.Action == AuditAction.LoggedIn);
        loggedIn.Changes.Should().Contain("\"source\":\"Local\"").And.Contain("\"secondFactor\":\"passkey\"");
        var stored = await StoredPasskeyAsync(user.Id);
        stored.LastUsedAt.Should().NotBeNull();
        stored.SignCount.Should().Be(1);
    }

    [Fact]
    public async Task A_password_sign_in_remembers_password_as_the_last_method()
    {
        using var browser = Browser();
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var (_, loginPage) = await StartAsync(browser, BffTestSupport.Spa + "/");
        var token = await AntiforgeryTokenAsync(browser, loginPage);

        using var posted = await PostLoginAsync(browser, token, user, Query(loginPage, "ReturnUrl"));

        posted.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("cas-login-method=password;"));
    }

    [Fact]
    public async Task A_passkey_satisfies_two_factor_so_a_user_with_an_authenticator_code_is_not_asked_for_one()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync();
        await TotpTestSupport.EnableAsync(Services, user);

        var (_, next) = await StartPasskeyAsync(browser);
        var challenge = await OpenPasskeyPageAsync(browser, next);
        using var posted = await PostPasskeyAsync(browser, next, challenge, Honest(authenticator, challenge));

        posted.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(posted.Headers.Location!).Should().StartWith("/connect/authorize");
        posted.Headers.GetValues("Set-Cookie").Should().NotContain(c => c.Contains("2fa")); // no code page, no remember cookie
        await RedeemAsync(browser, Pending(posted.Headers.Location!));
        await AssertSignedInAsync(browser, user);
    }

    [Fact]
    public async Task An_external_user_under_the_forced_policy_is_not_sent_to_enrolment_by_a_passkey_sign_in()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync("ExtAppraisalStaff", Guid.NewGuid());

        var resume = await SignInWithPasskeyAsync(browser, authenticator);

        resume.Should().StartWith("/connect/authorize");
        await RedeemAsync(browser, resume);
        await AssertSignedInAsync(browser, user);
    }

    [Fact]
    public async Task An_ldap_user_signs_in_with_a_passkey_when_the_directory_says_the_account_is_fine()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);

        var resume = await SignInWithPasskeyAsync(browser, authenticator);

        await RedeemAsync(browser, resume);
        await AssertSignedInAsync(browser, user);
        (await AuditAsync(user.Id)).Single(a => a.Action == AuditAction.LoggedIn).Changes
            .Should().Contain("\"source\":\"Ldap\"").And.Contain("\"secondFactor\":\"passkey\"");
    }

    // ---- the account stands in the way ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_deactivated_account_cannot_sign_in_with_its_passkey()
    {
        var (user, authenticator) = await UserWithPasskeyAsync();
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);

        await AssertPasskeyRefusedAsync(authenticator, "Inactive");
    }

    [Fact]
    public async Task An_account_whose_access_window_has_closed_cannot_sign_in_with_its_passkey()
    {
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.IsTemporaryAccess = true);
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.AccessExpiresAt = Now(Services).AddHours(-1));

        await AssertPasskeyRefusedAsync(authenticator, "Expired");
    }

    [Fact]
    public async Task A_locked_out_account_cannot_sign_in_with_its_passkey()
    {
        var (user, authenticator) = await UserWithPasskeyAsync();
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.LockoutEnd = DateTimeOffset.UtcNow.AddDays(1));

        await AssertPasskeyRefusedAsync(authenticator, "LockedOut");
    }

    [Theory]
    [InlineData(514, 0L, "AdDisabled")] // ACCOUNTDISABLE
    [InlineData(66050, 0L, "AdDisabled")]
    public async Task An_ldap_account_disabled_in_the_directory_cannot_sign_in_with_its_passkey(int control, long expires, string reason)
    {
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        FakeDirectory.Set(user.UserName, control, expires);

        await AssertPasskeyRefusedAsync(authenticator, reason);
    }

    [Fact]
    public async Task An_ldap_account_whose_directory_entry_has_expired_cannot_sign_in_with_its_passkey()
    {
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        FakeDirectory.Set(user.UserName, 512, DateTime.UtcNow.AddDays(-1).ToFileTimeUtc());

        await AssertPasskeyRefusedAsync(authenticator, "AdDisabled");
    }

    [Theory]
    [InlineData(0x10, "AdLockedOut")] // locked out in AD
    [InlineData(0x800000, "AdPasswordExpired")] // password past its maximum age, or must be changed at next logon
    public async Task An_ldap_account_AD_would_refuse_a_password_bind_for_cannot_sign_in_with_its_passkey(int computed, string reason)
    {
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        FakeDirectory.Set(user.UserName, computed: computed);

        await AssertPasskeyRefusedAsync(authenticator, reason);
    }

    [Fact]
    public async Task An_ldap_account_is_refused_when_the_directory_cannot_be_reached()
    {
        var (user, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        FakeDirectory.MakeUnavailable(user.UserName);

        await AssertPasskeyRefusedAsync(authenticator, "AdUnavailable");
    }

    [Fact]
    public async Task An_ldap_account_is_refused_when_ldap_is_switched_off()
    {
        var (_, authenticator) = await UserWithPasskeyAsync(configure: u => u.AuthSource = AuthSources.Ldap);
        var ldap = Services.GetRequiredService<IOptions<LdapConfiguration>>().Value;
        ldap.Enabled = false;
        try
        {
            await AssertPasskeyRefusedAsync(authenticator, "LdapDisabled");
        }
        finally
        {
            ldap.Enabled = true;
        }
    }

    // ---- the assertion is not good ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_passkey_the_server_never_registered_is_refused()
    {
        var stranger = new SoftAuthenticator();

        await AssertPasskeyRefusedAsync(stranger, "UnknownPasskey",
            respond: (a, c) => a.Get(c.OptionsJson, PasskeyTestSupport.IdpOrigin, Guid.NewGuid().ToByteArray()),
            signalsUnknownCredential: true);
    }

    [Fact]
    public async Task A_passkey_the_server_no_longer_has_is_signalled_to_the_browser_and_nothing_else_is()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync();
        await using (var scope = Services.CreateAsyncScope()) // deleted in the profile, say; the keychain still has it
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().UserPasskeys
                .Where(p => p.UserId == user.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var (_, next) = await StartPasskeyAsync(browser);
        var fresh = await OpenPasskeyPageAsync(browser, next);
        fresh.Html.Should().NotContain("data-unknown-credential=\"").And.NotContain(" data-failed"); // an ordinary visit signals nothing and prompts
        StatusLine(fresh.Html).Should().StartWith("Follow the prompt on your device"); // and tells the user to follow the prompt

        using var posted = await PostPasskeyAsync(browser, next, fresh, Honest(authenticator, fresh));

        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await posted.Content.ReadAsStringAsync();
        html.Should().Contain("Passkey sign-in did not work"); // the same generic message
        html.Should().Contain($"data-unknown-credential=\"{SoftAuthenticator.B64(authenticator.CredentialId)}\"");
        // Fresh options come with it, so the page can still be used for another passkey or for the retry.
        html.Should().Contain("data-options=");
    }

    [Fact]
    public async Task An_assertion_made_for_another_origin_is_refused()
    {
        var (_, authenticator) = await UserWithPasskeyAsync();

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey",
            respond: (a, c) => a.Get(c.OptionsJson, "https://evil.example"));
    }

    [Fact]
    public async Task An_assertion_made_without_user_verification_is_refused()
    {
        var (_, authenticator) = await UserWithPasskeyAsync();
        authenticator.UserVerified = false; // no PIN, no biometric

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey");
    }

    [Fact]
    public async Task An_assertion_whose_user_handle_belongs_to_someone_else_is_refused()
    {
        var (_, authenticator) = await UserWithPasskeyAsync();
        var other = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey",
            respond: (a, c) => a.Get(c.OptionsJson, PasskeyTestSupport.IdpOrigin, other.Id.ToByteArray()));
    }

    [Fact]
    public async Task A_tampered_or_missing_state_is_refused()
    {
        var (_, authenticator) = await UserWithPasskeyAsync();

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey", state: c => c.State[..^3] + "AAA");
        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey", state: _ => "");
    }

    [Fact]
    public async Task A_state_that_was_not_sealed_for_sign_in_is_refused()
    {
        // A registration state (sealed for that user) is no sign-in state.
        var (user, authenticator) = await UserWithPasskeyAsync();
        string registrationState;
        using (var spa = Browser())
        {
            var cookie = await BffTestSupport.SignInAsync(spa, user);
            using var begin = await PasskeyTestSupport.PostAsync(spa, cookie, "/auth/me/passkeys/register/options");
            registrationState = (await BffTestSupport.ReadJsonAsync(begin)).RootElement.GetProperty("state").GetString()!;
        }

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey", state: _ => registrationState);
    }

    [Theory]
    [InlineData(true)] // a hardware key: the signature counter refuses the repeat
    [InlineData(false)] // a synced passkey: the counter stays 0, so only the single-use guard can refuse it
    public async Task A_replayed_sign_in_is_refused(bool authenticatorCounts)
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync(tweak: a => a.Counts = authenticatorCounts);
        var (_, next) = await StartPasskeyAsync(browser);
        var challenge = await OpenPasskeyPageAsync(browser, next);
        var credential = Honest(authenticator, challenge);
        var before = await CountAuditAsync("InvalidPasskey");

        using (var first = await PostPasskeyAsync(browser, next, challenge, credential))
            first.StatusCode.Should().Be(HttpStatusCode.Found);
        using var replay = await PostPasskeyAsync(browser, next, challenge, credential);

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadAsStringAsync()).Should().Contain("Passkey sign-in did not work");
        (await CountAuditAsync("InvalidPasskey")).Should().BeGreaterThan(before);
        (await StoredPasskeyAsync(user.Id)).SignCount.Should().Be(authenticatorCounts ? 1u : 0u);
    }

    [Fact]
    public async Task A_challenge_issued_before_the_last_successful_use_is_refused()
    {
        // Two tabs each opened the page; the first signs in, and the second's older challenge is then dead.
        using var browser = Browser();
        var (_, authenticator) = await UserWithPasskeyAsync(tweak: a => a.Counts = false);
        var (_, next) = await StartPasskeyAsync(browser);
        var older = await OpenPasskeyPageAsync(browser, next);
        await Task.Delay(30); // the clock ticks between the two issues
        var newer = await OpenPasskeyPageAsync(browser, next);

        using (var first = await PostPasskeyAsync(browser, next, newer, Honest(authenticator, newer)))
            first.StatusCode.Should().Be(HttpStatusCode.Found);
        using var second = await PostPasskeyAsync(browser, next, older, Honest(authenticator, older));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Passkey sign-in did not work");
    }

    // ---- getting to the page ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Asking_for_a_passkey_opens_the_passkey_page_and_asking_for_a_password_stays_on_the_password_page()
    {
        using var browser = Browser();

        var (returnUrl, passkeyNext) = await StartPasskeyAsync(browser);
        passkeyNext.Should().StartWith(PasskeyPage);

        foreach (var method in new[] { "password", null, "carrier-pigeon" })
        {
            var (_, next) = await StartPasskeyAsync(browser, method);
            next.Should().StartWith("/Account/Login", $"method={method}");
        }

        // "Use a password instead": back to Login with method=password, which stays even though the authorize URL says passkey.
        returnUrl.Should().Contain("method=passkey");
        using var stay = await browser.GetAsync(
            $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&method=password", TestContext.Current.CancellationToken);
        stay.StatusCode.Should().Be(HttpStatusCode.OK);

        var challenge = await OpenPasskeyPageAsync(browser, passkeyNext);
        challenge.Html.Should().Contain("method=password");
    }

    [Fact]
    public async Task The_passkey_page_needs_the_oidc_flow_like_the_other_sign_in_pages()
    {
        using var browser = Browser();

        using var noReturn = await browser.GetAsync(PasskeyPage, TestContext.Current.CancellationToken);
        noReturn.StatusCode.Should().Be(HttpStatusCode.Found);
        noReturn.Headers.Location!.ToString().Should().NotContain("Account");

        using var offsite = await browser.GetAsync($"{PasskeyPage}?returnUrl=https://evil.example/", TestContext.Current.CancellationToken);
        offsite.Headers.Location!.ToString().Should().NotContain("evil.example");
    }

    [Fact]
    public async Task The_passkey_page_offers_fresh_options_and_never_echoes_a_credential_back()
    {
        using var browser = Browser();
        var (_, authenticator) = await UserWithPasskeyAsync();
        var (_, next) = await StartPasskeyAsync(browser);
        var first = await OpenPasskeyPageAsync(browser, next);
        var second = await OpenPasskeyPageAsync(browser, next);

        first.State.Should().NotBe(second.State);
        first.OptionsJson.Should().NotBe(second.OptionsJson);
        using var options = System.Text.Json.JsonDocument.Parse(first.OptionsJson);
        options.RootElement.GetProperty("userVerification").GetString().Should().Be("required");
        options.RootElement.GetProperty("rpId").GetString().Should().Be("localhost");
        options.RootElement.GetProperty("allowCredentials").GetArrayLength().Should().Be(0); // discoverable

        // A refused attempt re-renders with the credential field empty.
        using var refused = await PostPasskeyAsync(browser, next, first, authenticator.Get(first.OptionsJson, "https://evil.example"));
        (await refused.Content.ReadAsStringAsync()).Should().MatchRegex("name=\"Credential\"[^>]*value=\"\"");
    }

    // ---- malformed input ----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"id\":\"AAAA\",\"rawId\":\"AAAA\",\"type\":\"public-key\",\"response\":{\"clientDataJSON\":\"!!\"}}")]
    public async Task A_garbage_credential_is_the_generic_refusal_not_a_server_error(string credential)
    {
        var (_, authenticator) = await UserWithPasskeyAsync();

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey", respond: (_, _) => credential);
    }

    [Fact]
    public async Task An_assertion_with_a_malformed_signature_is_the_generic_refusal()
    {
        var (_, authenticator) = await UserWithPasskeyAsync();

        await AssertPasskeyRefusedAsync(authenticator, "InvalidPasskey", respond: (a, c) =>
        {
            var honest = System.Text.Json.Nodes.JsonNode.Parse(Honest(a, c))!;
            honest["response"]!["signature"] = "AAECAw"; // not a DER signature
            return honest.ToJsonString();
        });
    }

    // ---- "use a password instead" holds -------------------------------------------------------------------------------------

    [Fact]
    public async Task Choosing_a_password_holds_for_later_redirects_back_to_login()
    {
        // Passkey page -> "use a password instead" -> password step with an authenticator code -> the parked step is lost (its
        // cookie lives five minutes) -> back to Login: it must stay the password page, not bounce to the passkey page.
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await TotpTestSupport.EnableAsync(Services, user);
        using var browser = Browser();
        var (returnUrl, passkeyNext) = await StartPasskeyAsync(browser);
        passkeyNext.Should().StartWith(PasskeyPage);
        returnUrl.Should().Contain("method=passkey"); // the SPA's own request did ask for a passkey
        var passwordUrl = WebUtility.HtmlDecode(Regex.Match((await OpenPasskeyPageAsync(browser, passkeyNext)).Html,
            "class=\"login-back\" href=\"([^\"]+)\"").Groups[1].Value);

        using var login = await browser.GetAsync(passwordUrl, TestContext.Current.CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var input = Regex.Match(await login.Content.ReadAsStringAsync(), "<input[^>]*name=\"ReturnUrl\"[^>]*>").Value;
        var carried = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value);
        carried.Should().StartWith("/connect/authorize").And.Contain("method=password").And.NotContain("method=passkey");

        var token = await AntiforgeryTokenAsync(browser, passwordUrl);
        using var posted = await PostLoginAsync(browser, token, user, carried);
        var parked = Pending(posted.Headers.Location!);
        parked.Should().StartWith(CodePage);

        // A browser with no parked cookie: what the user meets when the five minutes are up.
        using var later = Browser();
        using var expired = await later.GetAsync(parked, TestContext.Current.CancellationToken);
        expired.StatusCode.Should().Be(HttpStatusCode.Found);
        var backToLogin = Pending(expired.Headers.Location!);
        backToLogin.Should().StartWith("/Account/Login");
        using var stays = await later.GetAsync(backToLogin, TestContext.Current.CancellationToken);
        stays.StatusCode.Should().Be(HttpStatusCode.OK); // the password page, not a redirect to /Account/Passkey
    }

    // ---- admin revoke ends the IdP cookie ---------------------------------------------------------------------------------------

    [Fact]
    public async Task After_an_admin_revokes_a_users_passkeys_their_old_idp_cookie_mints_no_more_codes()
    {
        using var browser = Browser();
        var (user, authenticator) = await UserWithPasskeyAsync();
        var resume = await SignInWithPasskeyAsync(browser, authenticator); // the browser now holds the IdP cookie

        var admin = await BffTestSupport.CreateUserAsync(Services, "Admin");
        using var adminBrowser = Browser();
        var adminCookie = await BffTestSupport.SignInAsync(adminBrowser, admin);
        using (var revoke = await BffTestSupport.SendAsync(adminBrowser,
                   BffTestSupport.SpaRequest(HttpMethod.Delete, $"/auth/users/{user.Id}/passkeys"), adminCookie))
            revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var authorize = await browser.GetAsync(resume, TestContext.Current.CancellationToken);

        authorize.StatusCode.Should().Be(HttpStatusCode.Found);
        Pending(authorize.Headers.Location!).Should().StartWith("/Account/Login"); // not a code for /signin-oidc
    }
}
