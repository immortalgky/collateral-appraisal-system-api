using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using Integration.Fixtures;
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

        async Task<(int Failed, DateTimeOffset? LockoutEnd)> StateAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::Auth.Domain.Identity.ApplicationUser>>();
            var stored = (await users.FindByIdAsync(user.Id.ToString()))!;
            return (stored.AccessFailedCount, stored.LockoutEnd);
        }

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

        // The default policy locks at five failures.
        for (var i = 0; i < 5; i++)
        {
            using var bad = await PostAsync(browser, cookie, "/auth/me/two-factor/recovery-codes", new { code = TotpTestSupport.WrongCode(key) });
            await AssertBadRequestAsync(bad, Invalid);
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
        (await scope.ServiceProvider.GetRequiredService<global::Auth.Infrastructure.AuthDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == enabled.Id)).TwoFactorEnabled.Should().BeTrue();
    }
}
