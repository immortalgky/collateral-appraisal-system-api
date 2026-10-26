using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Auth.Domain.Auditing;
using Auth.Domain.Identity;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>The profile's own two-factor endpoints, the admin policy fields and the admin user detail.</summary>
[Collection("Integration")]
public class TwoFactorEndpointTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private async Task<(HttpClient Browser, TestUser User, string Cookie)> SignedInAsync(string role = "RequestMaker",
        Guid? companyId = null)
    {
        var browser = fixture.BffWebApplicationFactory.CreateBrowser();
        var user = await BffTestSupport.CreateUserAsync(Services, role, companyId);
        return (browser, user, await BffTestSupport.SignInAsync(browser, user));
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient browser, string cookie, string path, object? body = null)
    {
        var request = BffTestSupport.SpaRequest(HttpMethod.Post, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return BffTestSupport.SendAsync(browser, request, cookie);
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        (await BffTestSupport.ReadJsonAsync(response)).RootElement.Clone();

    private static async Task<JsonElement> MeAsync(HttpClient browser, string cookie)
    {
        using var me = await BffTestSupport.GetAsync(browser, "/auth/me", cookie);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonOf(me);
    }

    /// <summary>Calls setup and returns the key the QR code carries, as the app would read it.</summary>
    private static async Task<string> SetupAsync(HttpClient browser, string cookie)
    {
        using var setup = await PostAsync(browser, cookie, "/auth/me/two-factor/setup");
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await JsonOf(setup);
        json.GetProperty("sharedKey").GetString().Should().MatchRegex("^([a-z2-7]{4} )+[a-z2-7]{1,4}$");
        json.GetProperty("authenticatorUri").GetString().Should()
            .StartWith("otpauth://totp/Collateral%20Appraisal%20System:")
            .And.Contain("issuer=Collateral%20Appraisal%20System").And.EndWith("&digits=6");
        json.GetProperty("qrSvg").GetString().Should().Contain("<svg").And.Contain("viewBox");
        return Regex.Match(json.GetProperty("authenticatorUri").GetString()!, "secret=([A-Z2-7]+)").Groups[1].Value;
    }

    private static async Task<string[]> EnableAsync(HttpClient browser, string cookie, string key)
    {
        using var enabled = await PostAsync(browser, cookie, "/auth/me/two-factor/enable",
            new { code = TotpTestSupport.Code(key) });
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await JsonOf(enabled)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string errorCode)
    {
        // Never 401: the SPA signs the user out on any 401.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await JsonOf(response);
        json.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("errorCode").GetString().Should().Be(errorCode);
    }

    private const string Invalid = "InvalidTwoFactorCode";

    [Fact]
    public async Task Setup_enable_regenerate_and_disable_walk_through_with_bad_codes_answered_400()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        (await MeAsync(browser, cookie)).GetProperty("twoFactorEnabled").GetBoolean().Should().BeFalse();

        var key = await SetupAsync(browser, cookie);

        // Not on until a code from the app has been verified.
        (await MeAsync(browser, cookie)).GetProperty("twoFactorEnabled").GetBoolean().Should().BeFalse();
        using (var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/enable", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(bad, Invalid);
        using (var missing = await PostAsync(browser, cookie, "/auth/me/two-factor/enable", new { }))
            await AssertBadRequestAsync(missing, Invalid);

        // Spaces and dashes the user copied along are ignored.
        var typed = TotpTestSupport.Code(key);
        using var enabled = await PostAsync(browser, cookie, "/auth/me/two-factor/enable", new { code = $" {typed[..3]}-{typed[3..]} " });
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        var codes = (await JsonOf(enabled)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        codes.Should().HaveCount(10).And.OnlyHaveUniqueItems();

        var me = await MeAsync(browser, cookie);
        me.GetProperty("twoFactorEnabled").GetBoolean().Should().BeTrue();
        me.GetProperty("twoFactorRequired").GetBoolean().Should().BeFalse();
        me.GetProperty("recoveryCodesLeft").GetInt32().Should().Be(10);

        // Already on.
        using (var again = await PostAsync(browser, cookie, "/auth/me/two-factor/setup"))
            await AssertBadRequestAsync(again, "TwoFactorAlreadyEnabled");
        using (var enableAgain = await PostAsync(browser, cookie, "/auth/me/two-factor/enable", new { code = TotpTestSupport.Code(key) }))
            await AssertBadRequestAsync(enableAgain, "TwoFactorAlreadyEnabled");

        // Regenerate and disable both need a current code.
        using (var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(bad, Invalid);
        using (var renewed = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.Code(key) }))
        {
            renewed.StatusCode.Should().Be(HttpStatusCode.OK);
            var fresh = (await JsonOf(renewed)).GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
            fresh.Should().HaveCount(10).And.NotIntersectWith(codes);
        }

        using (var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(bad, Invalid);
        (await MeAsync(browser, cookie)).GetProperty("twoFactorEnabled").GetBoolean().Should().BeTrue();

        using (var off = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = TotpTestSupport.Code(key) }))
            off.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me = await MeAsync(browser, cookie);
        me.GetProperty("twoFactorEnabled").GetBoolean().Should().BeFalse();
        me.GetProperty("recoveryCodesLeft").GetInt32().Should().Be(0);
        (await TwoFactorStateAsync(user.Id)).Key.Should().BeNull();
        (await BffTestSupport.AuditAsync(Services, user.Id)).Select(a => a.Action).Should().Contain(AuditAction.TwoFactorDisabled);

        // Turning it off while it is off is a state error, not a crash.
        using (var offAgain = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = "123456" }))
            await AssertBadRequestAsync(offAgain, "TwoFactorNotEnabled");
        using (var renewOff = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = "123456" }))
            await AssertBadRequestAsync(renewOff, "TwoFactorNotEnabled");
    }

    [Fact]
    public async Task Two_factor_cannot_be_turned_off_when_the_policy_requires_it()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        var (browser, user, cookie) = await SignedInAsync("ExtAppraisalStaff", Guid.NewGuid());
        using var __ = browser;
        var key = await SetupAsync(browser, cookie);
        await EnableAsync(browser, cookie, key);
        (await MeAsync(browser, cookie)).GetProperty("twoFactorRequired").GetBoolean().Should().BeTrue();

        using var refused = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = TotpTestSupport.Code(key) });

        await AssertBadRequestAsync(refused, "TwoFactorRequiredByPolicy");
        (await MeAsync(browser, cookie)).GetProperty("twoFactorEnabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_endpoints_need_a_session()
    {
        using var browser = fixture.BffWebApplicationFactory.CreateBrowser();

        using var response = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/me/two-factor/setup"), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_password_policy_round_trips_the_two_factor_fields_and_clamps_the_hours()
    {
        var (browser, _, cookie) = await SignedInAsync("Admin");
        using var _ = browser;
        await using var __ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 0, TwoFactorRememberHours = 12",
            "RequireTwoFactorForExternal = 0, TwoFactorRememberHours = 12");

        using var before = await BffTestSupport.GetAsync(browser, "/auth/admin/password-policy", cookie);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await before.Content.ReadAsStringAsync())!.AsObject();
        body["requireTwoFactorForExternal"]!.GetValue<bool>().Should().BeFalse();
        body["twoFactorRememberHours"]!.GetValue<int>().Should().Be(12);

        body["requireTwoFactorForExternal"] = true;
        body["twoFactorRememberHours"] = 48;
        await PutPolicyAsync(browser, cookie, body);
        var after = await PolicyAsync(browser, cookie);
        after["requireTwoFactorForExternal"]!.GetValue<bool>().Should().BeTrue();
        after["twoFactorRememberHours"]!.GetValue<int>().Should().Be(48);

        // A client that predates the fields leaves them alone.
        body.Remove("requireTwoFactorForExternal");
        body.Remove("twoFactorRememberHours");
        await PutPolicyAsync(browser, cookie, body);
        after = await PolicyAsync(browser, cookie);
        after["requireTwoFactorForExternal"]!.GetValue<bool>().Should().BeTrue();
        after["twoFactorRememberHours"]!.GetValue<int>().Should().Be(48);

        // Out of range is refused rather than silently clamped.
        body["twoFactorRememberHours"] = 721;
        var request = BffTestSupport.SpaRequest(HttpMethod.Put, "/auth/admin/password-policy");
        request.Content = JsonContent.Create(body);
        using var tooLong = await BffTestSupport.SendAsync(browser, request, cookie);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonObject> PolicyAsync(HttpClient browser, string cookie)
    {
        using var response = await BffTestSupport.GetAsync(browser, "/auth/admin/password-policy", cookie);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    private static async Task PutPolicyAsync(HttpClient browser, string cookie, JsonObject body)
    {
        var request = BffTestSupport.SpaRequest(HttpMethod.Put, "/auth/admin/password-policy");
        request.Content = JsonContent.Create(body);
        using var response = await BffTestSupport.SendAsync(browser, request, cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_admin_user_detail_says_whether_the_user_has_two_factor()
    {
        var (adminBrowser, _, adminCookie) = await SignedInAsync("Admin");
        using var _ = adminBrowser;
        var plain = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        var secured = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await TotpTestSupport.EnableAsync(Services, secured);

        foreach (var (target, expected) in new[] { (plain, false), (secured, true) })
        {
            using var detail = await BffTestSupport.GetAsync(adminBrowser, $"/auth/users/{target.Id}", adminCookie);
            detail.StatusCode.Should().Be(HttpStatusCode.OK);
            (await JsonOf(detail)).GetProperty("twoFactorEnabled").GetBoolean().Should().Be(expected);
        }
    }

    [Fact]
    public async Task Resetting_two_factor_needs_the_manage_users_permission()
    {
        var (browser, user, cookie) = await SignedInAsync("RequestMaker");
        using var _ = browser;

        using var response = await PostAsync(browser, cookie, $"/auth/users/{user.Id}/two-factor/reset");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Wrong_codes_on_the_guarded_endpoints_lock_the_account_and_a_right_code_is_then_refused()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var key = await SetupAsync(browser, cookie);
        await EnableAsync(browser, cookie, key);

        Task<(int Failed, DateTimeOffset? LockoutEnd)> StateAsync() =>
            BffTestSupport.ReadUserAsync(Services, user.Id, (_, u) => Task.FromResult((u.AccessFailedCount, u.LockoutEnd)));

        // A right code clears what wrong ones added.
        for (var i = 0; i < 2; i++)
        {
            using var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.WrongCode(key) });
            await AssertBadRequestAsync(bad, Invalid);
        }
        (await StateAsync()).Failed.Should().Be(2);
        using (var ok = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.Code(key) }))
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StateAsync()).Failed.Should().Be(0);

        // The default policy locks at five failures, and the fifth already says so.
        for (var i = 0; i < 5; i++)
        {
            using var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.WrongCode(key) });
            await AssertBadRequestAsync(bad, i < 4 ? Invalid : "TwoFactorLockedOut");
        }
        (await StateAsync()).LockoutEnd.Should().NotBeNull().And.BeAfter(DateTimeOffset.UtcNow);

        // Locked out: even the right code is refused, on both guarded endpoints, still as 400.
        foreach (var path in new[] { "/auth/me/two-factor/recovery-codes", "/auth/me/two-factor/disable" })
        {
            using var refused = await PostAsync(browser, cookie, path, new { code = TotpTestSupport.Code(key) });
            await AssertBadRequestAsync(refused, "TwoFactorLockedOut");
        }
        (await MeAsync(browser, cookie)).GetProperty("twoFactorEnabled").GetBoolean().Should().BeTrue();
    }

    private record TwoFactorState(bool Enabled, string? Key, int RecoveryCodesLeft, int AccessFailed);

    private Task<TwoFactorState> TwoFactorStateAsync(Guid userId) =>
        BffTestSupport.ReadUserAsync(Services, userId, async (users, u) => new TwoFactorState(
            u.TwoFactorEnabled, await users.GetAuthenticatorKeyAsync(u), await users.CountRecoveryCodesAsync(u), u.AccessFailedCount));

    [Fact]
    public async Task A_new_device_can_be_set_up_with_a_recovery_code_even_when_the_policy_requires_two_factor()
    {
        await using var _ = await TotpTestSupport.SetPolicyAsync(
            Services, "RequireTwoFactorForExternal = 1", "RequireTwoFactorForExternal = 0");
        var (browser, user, cookie) = await SignedInAsync("ExtAppraisalStaff", Guid.NewGuid());
        using var __ = browser;
        var key = await SetupAsync(browser, cookie);
        var codes = await EnableAsync(browser, cookie, key);

        // A wrong code is a 400 that counts toward the lockout and changes nothing.
        using (var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = "ABCDE-99999" }))
            await AssertBadRequestAsync(bad, Invalid);
        var afterWrong = await TwoFactorStateAsync(user.Id);
        afterWrong.Enabled.Should().BeTrue();
        afterWrong.AccessFailed.Should().Be(1);

        // The disable endpoint refuses this user, which is why the new-device one exists. A recovery code typed
        // lower case and without its hyphen is accepted.
        using (var disable = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = TotpTestSupport.Code(key) }))
            await AssertBadRequestAsync(disable, "TwoFactorRequiredByPolicy");
        using (var reset = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = codes[0].Replace("-", "").ToLowerInvariant() }))
            reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Off, no key, no recovery codes, and signed out everywhere: the next sign-in is forced into enrolment by the policy.
        (await TwoFactorStateAsync(user.Id)).Should().BeEquivalentTo(new { Enabled = false, Key = (string?)null, RecoveryCodesLeft = 0, AccessFailed = 0 });
        (await BffTestSupport.GetSessionsAsync(Services, user.Id)).Should().OnlyContain(s => s.EndedAt != null);
        using var me = await BffTestSupport.GetAsync(browser, "/auth/me", cookie);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await BffTestSupport.AuditAsync(Services, user.Id)).Select(a => a.Action).Should()
            .Contain([AuditAction.RecoveryCodeUsed, AuditAction.TwoFactorReset]);
    }

    [Fact]
    public async Task A_wrong_new_device_code_counts_and_a_save_Identity_refuses_neither_counts_nor_spends_the_code()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var key = await SetupAsync(browser, cookie);
        var codes = await EnableAsync(browser, cookie, key);

        using (var wrong = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = "ABCDE-99999" }))
            await AssertBadRequestAsync(wrong, Invalid);
        (await TwoFactorStateAsync(user.Id)).AccessFailed.Should().Be(1);

        // An old account Identity's validators refuse to update (an invalid e-mail): the spent code cannot be saved, and a
        // wrong code cannot be counted either, so a right and a wrong code get the same answer on every guarded endpoint.
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.Email = "not-an-email");
        using (var refused = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = codes[0] }))
            await AssertBadRequestAsync(refused, "TwoFactorSaveFailed");
        using (var wrongNewDevice = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = "ABCDE-99999" }))
            await AssertBadRequestAsync(wrongNewDevice, "TwoFactorSaveFailed");
        using (var wrongGuarded = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(wrongGuarded, "TwoFactorSaveFailed");
        using (var wrongDisable = await PostAsync(browser, cookie, "/auth/me/two-factor/disable", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(wrongDisable, "TwoFactorSaveFailed");
        // A right authenticator code gets the same answer, so none of this tells a right code from a wrong one.
        using (var rightGuarded = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.Code(key) }))
            await AssertBadRequestAsync(rightGuarded, "TwoFactorSaveFailed");
        using (var rightNewDevice = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = TotpTestSupport.Code(key) }))
            await AssertBadRequestAsync(rightNewDevice, "TwoFactorSaveFailed");

        var state = await TwoFactorStateAsync(user.Id);
        state.AccessFailed.Should().Be(1);
        state.Enabled.Should().BeTrue();
        state.RecoveryCodesLeft.Should().Be(10);
    }

    [Fact]
    public async Task A_new_device_can_be_set_up_with_an_authenticator_code_and_only_while_two_factor_is_on()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;

        using (var off = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = "123456" }))
            await AssertBadRequestAsync(off, "TwoFactorNotEnabled");

        var key = await SetupAsync(browser, cookie);
        await EnableAsync(browser, cookie, key);
        using (var wrong = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = TotpTestSupport.WrongCode(key) }))
            await AssertBadRequestAsync(wrong, Invalid);
        (await TwoFactorStateAsync(user.Id)).AccessFailed.Should().Be(1);
        using (var reset = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = TotpTestSupport.Code(key) }))
            reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await TwoFactorStateAsync(user.Id)).Should().BeEquivalentTo(new { Enabled = false, Key = (string?)null, RecoveryCodesLeft = 0, AccessFailed = 0 });
        (await BffTestSupport.GetSessionsAsync(Services, user.Id)).Should().OnlyContain(s => s.EndedAt != null);
    }

    [Fact]
    public async Task Logging_out_still_works_from_the_session_a_new_device_set_up_just_ended()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var key = await SetupAsync(browser, cookie);
        await EnableAsync(browser, cookie, key);
        using (var reset = await PostAsync(browser, cookie, "/auth/me/two-factor/new-device", new { code = TotpTestSupport.Code(key) }))
            reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The SPA's logout() runs next, with the cookie of the session that was just ended.
        using var logout = await PostAsync(browser, cookie, "/auth/logout");
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await JsonOf(logout)).GetProperty("url").GetString()!;
        url.Should().NotBeNullOrWhiteSpace();

        // And the URL it returns is accepted: the IdP sends the browser back through /signout-callback-oidc to the SPA.
        var next = new Uri(new Uri(BffTestSupport.Idp), url);
        using var idp = await browser.GetAsync(next.PathAndQuery, TestContext.Current.CancellationToken);
        idp.StatusCode.Should().Be(HttpStatusCode.Found, url);
        using var spa = await browser.GetAsync(idp.Headers.Location!.PathAndQuery, TestContext.Current.CancellationToken);
        spa.StatusCode.Should().Be(HttpStatusCode.Found);
        // Back at the SPA's home, which has no session and so sends the user to /login.
        spa.Headers.Location!.ToString().Should().StartWith(BffTestSupport.Spa);
        (await BffTestSupport.GetSessionsAsync(Services, user.Id)).Should().OnlyContain(s => s.EndedAt != null);
    }

    [Fact]
    public async Task Setup_twice_keeps_the_same_key_until_it_is_verified()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;

        var first = await SetupAsync(browser, cookie);
        var second = await SetupAsync(browser, cookie);

        second.Should().Be(first);
    }

    [Fact]
    public async Task When_identity_refuses_the_update_the_endpoints_answer_400_not_500()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        Task BreakAsync(Guid id) => BffTestSupport.UpdateUserAsync(Services, id, u =>
        {
            u.Email = $"not-an-email-{id:N}"; // Identity's validator rejects it, so every update comes back failed
            u.NormalizedEmail = u.Email.ToUpperInvariant();
        });
        await BreakAsync(user.Id);

        using (var setup = await PostAsync(browser, cookie, "/auth/me/two-factor/setup"))
            await AssertBadRequestAsync(setup, "TwoFactorSaveFailed");

        // An account that has two-factor on, then ends up unable to be updated: the admin reset must not be a 500 either.
        var enabled = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await TotpTestSupport.EnableAsync(Services, enabled);
        await BreakAsync(enabled.Id);
        var (adminBrowser, _, adminCookie) = await SignedInAsync("Admin");
        using var __ = adminBrowser;
        using var reset = await PostAsync(adminBrowser, adminCookie, $"/auth/users/{enabled.Id}/two-factor/reset");
        await AssertBadRequestAsync(reset, "TwoFactorSaveFailed");

        // All or nothing: the failed reset left two-factor on.
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == enabled.Id)).TwoFactorEnabled.Should().BeTrue();
    }
}
