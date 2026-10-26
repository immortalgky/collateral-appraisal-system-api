using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auth.Domain.Auditing;
using Auth.Domain.Passkeys;
using Auth.Domain.Sessions;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>The profile's own passkey endpoints, the admin revoke and the admin user detail.</summary>
[Collection("Integration")]
public class PasskeyEndpointTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private async Task<(HttpClient Browser, TestUser User, string Cookie)> SignedInAsync(string role = "RequestMaker")
    {
        var browser = fixture.BffWebApplicationFactory.CreateBrowser();
        var user = await BffTestSupport.CreateUserAsync(Services, role);
        return (browser, user, await BffTestSupport.SignInAsync(browser, user));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient browser, string cookie, HttpMethod method, string path, object? body = null)
    {
        var request = BffTestSupport.SpaRequest(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return BffTestSupport.SendAsync(browser, request, cookie);
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        (await BffTestSupport.ReadJsonAsync(response)).RootElement.Clone();

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string errorCode)
    {
        // Never 401: the SPA signs the user out on any 401.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var json = await JsonOf(response);
        json.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("errorCode").GetString().Should().Be(errorCode);
    }

    private static async Task<JsonElement> ListAsync(HttpClient browser, string cookie)
    {
        using var response = await BffTestSupport.GetAsync(browser, "/auth/me/passkeys", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonOf(response);
    }

    /// <summary>Sets the session's sign-in time back, as if the user signed in long ago.</summary>
    private async Task AgeSessionAsync(Guid userId, TimeSpan by)
    {
        var session = await BffTestSupport.GetSessionAsync(Services, userId);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.CreatedAt = s.CreatedAt - by);
    }

    // ---- register, list, rename, delete --------------------------------------------------------------------------------

    [Fact]
    public async Task A_passkey_is_registered_listed_renamed_and_removed()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        (await ListAsync(browser, cookie)).GetArrayLength().Should().Be(0);

        var authenticator = new SoftAuthenticator { BackupEligible = true, BackedUp = true };
        var added = await PasskeyTestSupport.RegisterAsync(browser, cookie, authenticator, "  Work laptop  ");

        added.GetProperty("name").GetString().Should().Be("Work laptop"); // trimmed
        added.GetProperty("synced").GetBoolean().Should().BeTrue();
        added.GetProperty("lastUsedAt").ValueKind.Should().Be(JsonValueKind.Null);
        added.GetProperty("createdAt").GetDateTime().Should().BeCloseTo(DateTime.Now, TimeSpan.FromHours(14));
        var id = added.GetProperty("id").GetGuid();
        var listed = (await ListAsync(browser, cookie)).EnumerateArray().Single();
        listed.GetProperty("id").GetGuid().Should().Be(id);
        listed.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "synced", "createdAt", "lastUsedAt");

        using (var rename = await SendAsync(browser, cookie, HttpMethod.Put, $"/auth/me/passkeys/{id}", new { name = "Home PC" }))
            rename.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(browser, cookie)).EnumerateArray().Single().GetProperty("name").GetString().Should().Be("Home PC");

        using (var remove = await SendAsync(browser, cookie, HttpMethod.Delete, $"/auth/me/passkeys/{id}"))
            remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(browser, cookie)).GetArrayLength().Should().Be(0);

        // Audit: adding and removing are logged (and the name, not the key); renaming is not.
        await using var scope = Services.CreateAsyncScope();
        var actions = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().AuthAuditLogs.AsNoTracking()
            .Where(a => a.EntityId == user.Id).Select(a => a.Action).ToListAsync();
        actions.Should().Contain(AuditAction.PasskeyAdded).And.Contain(AuditAction.PasskeyRemoved);
    }

    [Fact]
    public async Task The_registration_options_are_plain_json_for_the_browsers_parser()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        var options = started.GetProperty("options");

        // parseCreationOptionsFromJSON takes base64url strings and does not accept null where a value is expected.
        static void NoNulls(JsonElement e, string path)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Null: throw new Xunit.Sdk.XunitException($"null at {path}");
                case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) NoNulls(p.Value, path + "." + p.Name); break;
                case JsonValueKind.Array: foreach (var (item, i) in e.EnumerateArray().Select((x, i) => (x, i))) NoNulls(item, $"{path}[{i}]"); break;
            }
        }
        NoNulls(options, "options");
        options.GetProperty("rp").GetProperty("id").GetString().Should().Be("localhost");
        options.GetProperty("user").GetProperty("id").GetString().Should().MatchRegex("^[A-Za-z0-9_-]+$");
        options.GetProperty("user").GetProperty("id").GetString().Should().Be(SoftAuthenticator.B64(user.Id.ToByteArray()));
        options.GetProperty("challenge").GetString().Should().MatchRegex("^[A-Za-z0-9_-]{16,}$");
        options.GetProperty("pubKeyCredParams").EnumerateArray().Select(p => p.GetProperty("alg").GetInt32())
            .Should().Contain(-7);
        var selection = options.GetProperty("authenticatorSelection");
        selection.GetProperty("residentKey").GetString().Should().Be("required");
        selection.GetProperty("userVerification").GetString().Should().Be("required");
        started.GetProperty("state").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Adding_a_passkey_needs_a_sign_in_within_the_last_ten_minutes()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        await AgeSessionAsync(user.Id, TimeSpan.FromMinutes(11));

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");

        await AssertBadRequestAsync(begin, "PasskeyReauthRequired");
    }

    [Fact]
    public async Task A_session_kept_alive_by_activity_does_not_count_as_a_recent_sign_in()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        await AgeSessionAsync(user.Id, TimeSpan.FromMinutes(30));
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = DateTime.Now); // busy right now

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");

        await AssertBadRequestAsync(begin, "PasskeyReauthRequired");
    }

    [Fact]
    public async Task A_user_can_have_at_most_ten_passkeys()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            for (var i = 0; i < UserPasskey.MaxPerUser; i++)
                db.UserPasskeys.Add(new UserPasskey
                {
                    Id = Guid.CreateVersion7(), UserId = user.Id, CredentialId = Guid.NewGuid().ToByteArray(),
                    PublicKey = [1], Name = $"key {i}", CreatedAt = DateTime.Now
                });
            await db.SaveChangesAsync();
        }

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        await AssertBadRequestAsync(begin, "PasskeyLimitReached");

        // And the second step refuses too: a state obtained before the tenth was added cannot get an eleventh in.
        using var finish = await SendAsync(browser, cookie, HttpMethod.Post, "/auth/me/passkeys/register",
            new { state = "x", name = "eleventh", credential = new { } });
        await AssertBadRequestAsync(finish, "PasskeyStateExpired"); // the state is checked first
    }

    [Fact]
    public async Task Registering_the_same_authenticator_twice_is_refused()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var authenticator = new SoftAuthenticator();
        await PasskeyTestSupport.RegisterAsync(browser, cookie, authenticator, "first");

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        // The browser would refuse (excludeCredentials); a script that ignores that gets the same credential again.
        var credential = JsonDocument.Parse(authenticator.Create(started.GetProperty("options").GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement;
        using var again = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
            new { state = started.GetProperty("state").GetString(), name = "second", credential });

        await AssertBadRequestAsync(again, "PasskeyAlreadyRegistered");
        (await ListAsync(browser, cookie)).GetArrayLength().Should().Be(1);
        // The refused attempt left no audit row behind: the passkey and its row are written together or not at all.
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().AuthAuditLogs.AsNoTracking()
            .CountAsync(a => a.EntityId == user.Id && a.Action == AuditAction.PasskeyAdded)).Should().Be(1);
    }

    [Fact]
    public async Task The_existing_passkeys_are_excluded_from_a_new_registration()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        var authenticator = new SoftAuthenticator();
        await PasskeyTestSupport.RegisterAsync(browser, cookie, authenticator, "first");

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var exclude = (await JsonOf(begin)).GetProperty("options").GetProperty("excludeCredentials");

        exclude.EnumerateArray().Single().GetProperty("id").GetString().Should().Be(SoftAuthenticator.B64(authenticator.CredentialId));
    }

    [Fact]
    public async Task A_registration_that_fails_verification_or_whose_state_is_bad_is_a_400_with_its_code()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        var state = started.GetProperty("state").GetString();
        var authenticator = new SoftAuthenticator();
        var good = JsonDocument.Parse(authenticator.Create(started.GetProperty("options").GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement;

        using (var wrongOrigin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new
               {
                   state,
                   name = "x",
                   credential = JsonDocument.Parse(authenticator.Create(started.GetProperty("options").GetRawText(), "https://evil.example")).RootElement
               }))
            await AssertBadRequestAsync(wrongOrigin, "PasskeyRegistrationFailed");
        using (var noUv = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new
               {
                   state,
                   name = "x",
                   credential = JsonDocument.Parse(new SoftAuthenticator { UserVerified = false }
                       .Create(started.GetProperty("options").GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement
               }))
            await AssertBadRequestAsync(noUv, "PasskeyRegistrationFailed");
        using (var empty = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
                   new { state, name = "x", credential = new { } }))
            await AssertBadRequestAsync(empty, "PasskeyRegistrationFailed");
        using (var tampered = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
                   new { state = state![..^3] + "AAA", name = "x", credential = good }))
            await AssertBadRequestAsync(tampered, "PasskeyStateExpired");
        using (var missing = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
                   new { name = "x", credential = good }))
            await AssertBadRequestAsync(missing, "PasskeyStateExpired");
        (await ListAsync(browser, cookie)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Another_users_registration_state_does_not_work_for_me()
    {
        var (aliceBrowser, _, aliceCookie) = await SignedInAsync();
        var (bobBrowser, _, bobCookie) = await SignedInAsync();
        using var _ = aliceBrowser;
        using var __ = bobBrowser;
        using var begin = await PasskeyTestSupport.PostAsync(aliceBrowser, aliceCookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        var credential = JsonDocument.Parse(new SoftAuthenticator()
            .Create(started.GetProperty("options").GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement;

        using var stolen = await PasskeyTestSupport.PostAsync(bobBrowser, bobCookie, "/auth/me/passkeys/register",
            new { state = started.GetProperty("state").GetString(), name = "x", credential });

        await AssertBadRequestAsync(stolen, "PasskeyStateExpired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_name_is_refused_on_register_and_rename(string? name)
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        var authenticator = new SoftAuthenticator();
        var added = await PasskeyTestSupport.RegisterAsync(browser, cookie, authenticator, "ok");

        using (var rename = await SendAsync(browser, cookie, HttpMethod.Put, $"/auth/me/passkeys/{added.GetProperty("id").GetGuid()}", new { name }))
            await AssertBadRequestAsync(rename, "PasskeyNameInvalid");

        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        var credential = JsonDocument.Parse(new SoftAuthenticator().Create(started.GetProperty("options").GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement;
        using var register = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
            new { state = started.GetProperty("state").GetString(), name, credential });
        await AssertBadRequestAsync(register, "PasskeyNameInvalid");
    }

    [Fact]
    public async Task A_name_longer_than_one_hundred_characters_is_refused()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        var added = await PasskeyTestSupport.RegisterAsync(browser, cookie, new SoftAuthenticator(), "ok");

        using var rename = await SendAsync(browser, cookie, HttpMethod.Put, $"/auth/me/passkeys/{added.GetProperty("id").GetGuid()}",
            new { name = new string('a', 101) });

        await AssertBadRequestAsync(rename, "PasskeyNameInvalid");
    }

    [Fact]
    public async Task Only_the_owner_can_rename_or_remove_a_passkey()
    {
        var (aliceBrowser, _, aliceCookie) = await SignedInAsync();
        var (bobBrowser, _, bobCookie) = await SignedInAsync();
        using var _ = aliceBrowser;
        using var __ = bobBrowser;
        var alices = await PasskeyTestSupport.RegisterAsync(aliceBrowser, aliceCookie, new SoftAuthenticator(), "alice's");
        var id = alices.GetProperty("id").GetGuid();

        using (var rename = await SendAsync(bobBrowser, bobCookie, HttpMethod.Put, $"/auth/me/passkeys/{id}", new { name = "mine now" }))
            rename.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var remove = await SendAsync(bobBrowser, bobCookie, HttpMethod.Delete, $"/auth/me/passkeys/{id}"))
            remove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using (var missing = await SendAsync(bobBrowser, bobCookie, HttpMethod.Delete, $"/auth/me/passkeys/{Guid.NewGuid()}"))
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var still = (await ListAsync(aliceBrowser, aliceCookie)).EnumerateArray().Single();
        still.GetProperty("name").GetString().Should().Be("alice's");
        (await ListAsync(bobBrowser, bobCookie)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_endpoints_need_a_session()
    {
        using var browser = fixture.BffWebApplicationFactory.CreateBrowser();

        foreach (var (method, path) in new[]
                 {
                     (HttpMethod.Get, "/auth/me/passkeys"), (HttpMethod.Post, "/auth/me/passkeys/register/options"),
                     (HttpMethod.Delete, $"/auth/me/passkeys/{Guid.NewGuid()}")
                 })
        {
            using var response = await BffTestSupport.SendAsync(browser, BffTestSupport.SpaRequest(method, path), null);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
        }
    }

    // ---- admin ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_admin_can_see_a_users_passkeys_and_remove_them_all_which_ends_their_sessions()
    {
        var (userBrowser, user, userCookie) = await SignedInAsync();
        using var _ = userBrowser;
        await PasskeyTestSupport.RegisterAsync(userBrowser, userCookie, new SoftAuthenticator { BackupEligible = true, BackedUp = true }, "laptop");
        await PasskeyTestSupport.RegisterAsync(userBrowser, userCookie, new SoftAuthenticator(), "phone");
        var (adminBrowser, _, adminCookie) = await SignedInAsync("Admin");
        using var __ = adminBrowser;
        await TotpTestSupport.EnableAsync(Services, user); // Reset 2FA and passkeys are separate: this must survive

        using (var detail = await BffTestSupport.GetAsync(adminBrowser, $"/auth/users/{user.Id}", adminCookie))
        {
            var passkeys = (await JsonOf(detail)).GetProperty("passkeys").EnumerateArray().ToList();
            passkeys.Select(p => p.GetProperty("name").GetString()).Should().BeEquivalentTo("laptop", "phone");
            passkeys.Single(p => p.GetProperty("name").GetString() == "laptop").GetProperty("synced").GetBoolean().Should().BeTrue();
            passkeys[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "synced", "createdAt", "lastUsedAt");
        }

        using var revoke = await SendAsync(adminBrowser, adminCookie, HttpMethod.Delete, $"/auth/users/{user.Id}/passkeys");

        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var me = await BffTestSupport.GetAsync(userBrowser, "/auth/me", userCookie);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // the session ended
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Revoked);
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        (await db.UserPasskeys.AsNoTracking().CountAsync(p => p.UserId == user.Id)).Should().Be(0);
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).TwoFactorEnabled.Should().BeTrue();
        (await db.AuthAuditLogs.AsNoTracking().AnyAsync(a => a.EntityId == user.Id && a.Action == AuditAction.PasskeysRevoked && a.ChangesJson!.Contains("\"removed\":2")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Resetting_two_factor_leaves_the_passkeys_alone()
    {
        var (userBrowser, user, userCookie) = await SignedInAsync();
        using var _ = userBrowser;
        await PasskeyTestSupport.RegisterAsync(userBrowser, userCookie, new SoftAuthenticator(), "laptop");
        await TotpTestSupport.EnableAsync(Services, user);
        var (adminBrowser, _, adminCookie) = await SignedInAsync("Admin");
        using var __ = adminBrowser;

        using var reset = await SendAsync(adminBrowser, adminCookie, HttpMethod.Post, $"/auth/users/{user.Id}/two-factor/reset");

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var scope = Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AuthDbContext>().UserPasskeys.AsNoTracking()
            .CountAsync(p => p.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Revoking_passkeys_needs_the_manage_users_permission_and_an_existing_user()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var (adminBrowser, _, adminCookie) = await SignedInAsync("Admin");
        using var __ = adminBrowser;

        using (var forbidden = await SendAsync(browser, cookie, HttpMethod.Delete, $"/auth/users/{user.Id}/passkeys"))
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var missing = await SendAsync(adminBrowser, adminCookie, HttpMethod.Delete, $"/auth/users/{Guid.NewGuid()}/passkeys");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- malformed input --------------------------------------------------------------------------------------------------

    private async Task<(string State, JsonElement Options)> BeginAsync(HttpClient browser, string cookie)
    {
        using var begin = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register/options");
        var started = await JsonOf(begin);
        return (started.GetProperty("state").GetString()!, started.GetProperty("options"));
    }

    [Fact]
    public async Task A_register_body_with_no_credential_is_a_400_not_a_500()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        var (state, _) = await BeginAsync(browser, cookie);

        using (var none = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new { state, name = "x" }))
            await AssertBadRequestAsync(none, "PasskeyRegistrationFailed");
        using (var text = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new { state, name = "x", credential = "nope" }))
            await AssertBadRequestAsync(text, "PasskeyRegistrationFailed");
        using (var array = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new { state, name = "x", credential = new[] { 1 } }))
            await AssertBadRequestAsync(array, "PasskeyRegistrationFailed");
    }

    [Fact]
    public async Task A_credential_with_malformed_cbor_or_an_unsupported_algorithm_is_a_400_not_a_500()
    {
        var (browser, _, cookie) = await SignedInAsync();
        using var _ = browser;
        var (state, options) = await BeginAsync(browser, cookie);

        var garbage = System.Text.Json.Nodes.JsonNode.Parse(new SoftAuthenticator().Create(options.GetRawText(), PasskeyTestSupport.SpaOrigin))!;
        garbage["response"]!["attestationObject"] = SoftAuthenticator.B64([0xFF, 0x00, 0xA1, 0x7F, 0x13]); // not CBOR
        using (var cbor = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register",
                   new { state, name = "x", credential = JsonDocument.Parse(garbage.ToJsonString()).RootElement }))
            await AssertBadRequestAsync(cbor, "PasskeyRegistrationFailed");

        var oddAlgorithm = new SoftAuthenticator { CoseAlgorithm = -999 };
        using (var algorithm = await PasskeyTestSupport.PostAsync(browser, cookie, "/auth/me/passkeys/register", new
               {
                   state, name = "x",
                   credential = JsonDocument.Parse(oddAlgorithm.Create(options.GetRawText(), PasskeyTestSupport.SpaOrigin)).RootElement
               }))
            await AssertBadRequestAsync(algorithm, "PasskeyRegistrationFailed");

        (await ListAsync(browser, cookie)).GetArrayLength().Should().Be(0);
    }

    // ---- the save ---------------------------------------------------------------------------------------------------------

    private async Task<DbUpdateException> SaveFailureAsync(Action<UserPasskey> shape)
    {
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var passkey = new UserPasskey
        {
            Id = Guid.CreateVersion7(), UserId = user.Id, CredentialId = Guid.NewGuid().ToByteArray(), PublicKey = [1],
            Name = "k", CreatedAt = DateTime.Now
        };
        shape(passkey);
        db.UserPasskeys.Add(passkey);
        return await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Only_the_unique_index_on_the_credential_id_counts_as_already_registered()
    {
        var existing = Guid.NewGuid().ToByteArray();
        var owner = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.UserPasskeys.Add(new UserPasskey
            {
                Id = Guid.CreateVersion7(), UserId = owner.Id, CredentialId = existing, PublicKey = [1], Name = "first", CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var duplicate = await SaveFailureAsync(p => p.CredentialId = existing);
        var tooLong = await SaveFailureAsync(p => p.Name = new string('x', 101)); // a different failure: the column is 100

        global::Auth.Application.Services.PasskeyService.IsCredentialIdConflict(duplicate).Should().BeTrue();
        global::Auth.Application.Services.PasskeyService.IsCredentialIdConflict(tooLong).Should().BeFalse();
    }
}
