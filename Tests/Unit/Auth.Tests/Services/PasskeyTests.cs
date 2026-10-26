using Auth.Application.Services;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.DataProtection;
using Shared.Exceptions;

namespace Auth.Tests.Services;

public class PasskeyTests
{
    // ---- AD account status --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(512, false)] // NORMAL_ACCOUNT
    [InlineData(514, true)] // NORMAL_ACCOUNT | ACCOUNTDISABLE
    [InlineData(66050, true)] // NORMAL_ACCOUNT | ACCOUNTDISABLE | DONT_EXPIRE_PASSWORD
    [InlineData(66048, false)] // NORMAL_ACCOUNT | DONT_EXPIRE_PASSWORD
    [InlineData(2, true)]
    [InlineData(0, false)]
    public void An_ad_account_is_disabled_when_bit_two_of_user_account_control_is_set(int control, bool disabled) =>
        Assert.Equal(disabled, AdAccountStatus.IsDisabled(control));

    [Fact]
    public void An_ad_account_expires_at_its_file_time_and_zero_or_max_means_never()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(AdAccountStatus.IsExpired(0, now));
        Assert.False(AdAccountStatus.IsExpired(long.MaxValue, now));
        Assert.False(AdAccountStatus.IsExpired(now.AddDays(1).ToFileTimeUtc(), now));
        Assert.True(AdAccountStatus.IsExpired(now.AddSeconds(-1).ToFileTimeUtc(), now));
        Assert.True(AdAccountStatus.IsExpired(now.ToFileTimeUtc(), now)); // at the instant, it is over
        Assert.False(AdAccountStatus.IsExpired(DateTime.MaxValue.ToFileTimeUtc() + 1, now)); // beyond year 9999
        Assert.True(AdAccountStatus.IsExpired(-5, now)); // not a file time: refuse
    }

    private static LdapUserInfo Entry(int control = 512, long expires = 0, int computed = 0) =>
        new("jdoe", null, null, null, null, null, null, control, expires, computed);

    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_normal_directory_entry_may_sign_in() => Assert.Null(AdAccountStatus.RefusalReason(Entry(), Now));

    [Theory]
    [InlineData(0x10, "AdLockedOut")] // UF_LOCKOUT in the computed attribute
    [InlineData(0x800000, "AdPasswordExpired")] // UF_PASSWORD_EXPIRED
    [InlineData(0x800010, "AdLockedOut")] // both: the lockout is reported
    [InlineData(0x1, null)] // an unrelated computed bit
    public void The_computed_control_attribute_refuses_a_locked_out_account_and_an_expired_password(int computed, string? reason) =>
        Assert.Equal(reason, AdAccountStatus.RefusalReason(Entry(computed: computed), Now));

    [Fact]
    public void A_password_that_never_expires_is_not_refused_whatever_its_age()
    {
        // DONT_EXPIRE_PASSWORD (0x10000): AD then never sets UF_PASSWORD_EXPIRED, not even for a pwdLastSet of 0, and the
        // password bind works. The decision rests on that computed bit alone, so such an account is let in.
        Assert.Null(AdAccountStatus.RefusalReason(Entry(control: 512 | 0x10000, computed: 0), Now));
    }

    [Fact]
    public void A_disabled_or_expired_account_is_refused_before_anything_else()
    {
        Assert.Equal("AdDisabled", AdAccountStatus.RefusalReason(Entry(control: 514, computed: 0x10), Now));
        Assert.Equal("AdDisabled", AdAccountStatus.RefusalReason(Entry(expires: Now.AddDays(-1).ToFileTimeUtc()), Now));
        Assert.Null(AdAccountStatus.RefusalReason(Entry(expires: Now.AddDays(1).ToFileTimeUtc()), Now));
    }

    [Fact]
    public void Not_knowing_is_a_refusal()
    {
        Assert.Equal("AdUnavailable", AdAccountStatus.RefusalReason(null, Now));
        Assert.Equal("AdUnavailable", AdAccountStatus.RefusalReason(Entry() with { UserAccountControl = null }, Now));
        Assert.Equal("AdUnavailable", AdAccountStatus.RefusalReason(Entry() with { AccountExpires = null }, Now));
        Assert.Equal("AdUnavailable", AdAccountStatus.RefusalReason(Entry() with { UserAccountControlComputed = null }, Now));
    }

    // ---- sealed state ---------------------------------------------------------------------------------------------------

    private static PasskeyStateSealer Sealer(IDataProtectionProvider? provider = null) =>
        new(provider ?? new EphemeralDataProtectionProvider());

    [Fact]
    public void A_sealed_state_opens_for_its_purpose_and_carries_what_was_sealed()
    {
        var sealer = Sealer();
        var issued = new DateTime(2026, 10, 16, 9, 30, 15, 123, DateTimeKind.Unspecified).AddTicks(4567);

        var state = sealer.Unseal(PasskeyStateSealer.AssertPurpose,
            sealer.Seal(PasskeyStateSealer.AssertPurpose, "{\"challenge\":\"x\"}", issued));

        Assert.NotNull(state);
        Assert.Equal("{\"challenge\":\"x\"}", state.Options);
        Assert.Equal(issued, state.IssuedAt);
    }

    [Fact]
    public void A_sealed_state_does_not_open_after_it_expired()
    {
        var sealer = Sealer();
        var sealedState = sealer.Seal(PasskeyStateSealer.AssertPurpose, "{}", DateTime.UtcNow, TimeSpan.FromSeconds(-1));

        Assert.Null(sealer.Unseal(PasskeyStateSealer.AssertPurpose, sealedState));
    }

    [Fact]
    public void A_sealed_state_does_not_open_for_another_purpose_or_another_user()
    {
        var sealer = Sealer();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var registration = sealer.Seal(PasskeyStateSealer.RegisterPurpose(alice), "{}", DateTime.UtcNow);
        var signIn = sealer.Seal(PasskeyStateSealer.AssertPurpose, "{}", DateTime.UtcNow);

        Assert.NotNull(sealer.Unseal(PasskeyStateSealer.RegisterPurpose(alice), registration));
        Assert.Null(sealer.Unseal(PasskeyStateSealer.RegisterPurpose(bob), registration)); // someone else's registration
        Assert.Null(sealer.Unseal(PasskeyStateSealer.AssertPurpose, registration)); // a registration cannot sign in
        Assert.Null(sealer.Unseal(PasskeyStateSealer.RegisterPurpose(alice), signIn)); // and a sign-in cannot register
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-state")]
    public void A_missing_or_malformed_state_is_just_null(string? garbage) =>
        Assert.Null(Sealer().Unseal(PasskeyStateSealer.AssertPurpose, garbage));

    [Fact]
    public void A_tampered_state_does_not_open()
    {
        var sealer = Sealer();
        var sealedState = sealer.Seal(PasskeyStateSealer.AssertPurpose, "{}", DateTime.UtcNow);
        var tampered = sealedState[..^4] + (sealedState.EndsWith("AAAA") ? "BBBB" : "AAAA");

        Assert.Null(sealer.Unseal(PasskeyStateSealer.AssertPurpose, tampered));
    }

    [Fact]
    public void A_state_from_another_key_ring_does_not_open()
    {
        var sealedState = Sealer(new EphemeralDataProtectionProvider()).Seal(PasskeyStateSealer.AssertPurpose, "{}", DateTime.UtcNow);

        Assert.Null(Sealer(new EphemeralDataProtectionProvider()).Unseal(PasskeyStateSealer.AssertPurpose, sealedState));
    }

    // ---- RP id and origins -----------------------------------------------------------------------------------------------

    [Fact]
    public void The_relying_party_id_is_the_idp_host_and_the_origins_are_the_idp_and_the_spa()
    {
        var settings = new PasskeySettings(new BffUrls(
            "https://sit1.lhb.net:7112", ["https://sit1.lhb.net:3112/", "https://sit1.lhb.net:3112"], "https://idp.lhb.net"));

        Assert.Equal("idp.lhb.net", settings.RpId);
        Assert.Equal(["https://idp.lhb.net", "https://sit1.lhb.net:3112"], settings.Origins.Order());
        Assert.Equal(["https://sit1.lhb.net:3112"], settings.MismatchedOrigins); // sibling host: cannot use idp.lhb.net's id
    }

    [Fact]
    public void Without_a_separate_idp_the_id_is_the_api_host_and_the_spa_on_another_port_matches()
    {
        var settings = new PasskeySettings(new BffUrls("https://localhost:7111", ["https://localhost:3000"]));

        Assert.Equal("localhost", settings.RpId);
        Assert.Empty(settings.MismatchedOrigins);
    }

    [Theory]
    [InlineData("https://localhost:7111", false)]
    [InlineData("https://idp.lhb.net", false)]
    [InlineData("https://10.20.30.40:7112", true)]
    [InlineData("https://[::1]:7112", true)]
    public void An_ip_address_cannot_be_a_relying_party_id(string idpUrl, bool isIp) =>
        Assert.Equal(isIp, new PasskeySettings(new BffUrls(idpUrl, [], null)).RpIdIsIpAddress);

    [Theory]
    [InlineData("lhb.net", "https://app.lhb.net", true)] // a parent domain of the origin's host
    [InlineData("lhb.net", "https://lhb.net:8443", true)]
    [InlineData("app.lhb.net", "https://lhb.net", false)] // not the other way round
    [InlineData("lhb.net", "https://evil-lhb.net", false)] // a suffix is not a parent domain
    [InlineData("lhb.net", "not a url", false)]
    public void The_browsers_rule_for_a_relying_party_id(string rpId, string origin, bool allowed) =>
        Assert.Equal(allowed, PasskeySettings.CanUse(rpId, origin));

    // ---- names, method ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Work laptop", "Work laptop")]
    [InlineData("  Phone  ", "Phone")]
    [InlineData("เครื่องที่ทำงาน", "เครื่องที่ทำงาน")]
    public void A_passkey_name_is_trimmed(string typed, string expected) => Assert.Equal(expected, PasskeyService.ValidName(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("bad\nname")]
    public void A_blank_or_control_character_name_is_refused_with_its_error_code(string? name)
    {
        var refused = Assert.Throws<BadRequestException>(() => PasskeyService.ValidName(name));
        Assert.Equal(PasskeyErrors.NameInvalid, refused.Code);
    }

    [Fact]
    public void A_name_is_at_most_one_hundred_characters()
    {
        Assert.Equal(100, PasskeyService.ValidName(new string('a', 100)).Length);
        Assert.Equal(100, PasskeyService.ValidName(" " + new string('a', 100) + " ").Length); // the limit is on the trimmed name
        Assert.Throws<BadRequestException>(() => PasskeyService.ValidName(new string('a', 101)));
    }

    [Theory]
    [InlineData("/connect/authorize?client_id=spa&method=passkey&state=a%2Bb%3D", "password")]
    [InlineData("/connect/authorize?client_id=spa&state=a%2Bb%3D", "password")] // absent: added
    [InlineData("/connect/authorize?method=passkey&method=passkey", "password")] // repeated: one left
    [InlineData("/connect/authorize", "password")]
    public void The_method_in_the_authorize_url_is_rewritten_and_nothing_else_changes(string url, string method)
    {
        var rewritten = LoginMethod.WithMethod(url, method);

        Assert.Equal(method, LoginMethod.FromAuthorizeUrl(rewritten));
        Assert.StartsWith("/connect/authorize", rewritten);
        Assert.Equal(1, rewritten.Split("method=").Length - 1);
        if (url.Contains("state="))
            Assert.Equal(
                Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url[url.IndexOf('?')..])["state"],
                Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(rewritten[rewritten.IndexOf('?')..])["state"]);
    }

    [Theory]
    [InlineData("passkey", "passkey")]
    [InlineData(" PASSWORD ", "password")]
    [InlineData("sms", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Only_the_two_known_sign_in_methods_pass(string? value, string? expected) =>
        Assert.Equal(expected, LoginMethod.Sanitize(value));

    [Theory]
    [InlineData("/connect/authorize?client_id=spa&method=passkey&state=x", "passkey")]
    [InlineData("/connect/authorize?method=password", "password")]
    [InlineData("/connect/authorize?client_id=spa", null)]
    [InlineData("/connect/authorize?method=passkey&method=password", null)] // ambiguous
    [InlineData("/connect/authorize?method=evil", null)]
    [InlineData("/connect/authorize", null)]
    public void The_method_is_read_back_from_the_pending_authorize_url(string url, string? expected) =>
        Assert.Equal(expected, LoginMethod.FromAuthorizeUrl(url));
}
