using System.Net;
using System.Net.Http.Json;
using Auth.Application.Features.Users.UpdateUser;
using Auth.Application.Services;
using Auth.Domain.Sessions;
using Auth.Infrastructure;
using Auth.Infrastructure.Configuration;
using FluentAssertions;
using Integration.Fixtures;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Time;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// The cookie is not a credential on its own: every request re-checks the account behind it, so a
/// deactivation, a closed access window or a forced password change bites on the next request (single node here; the other node follows within its cache lifetime).
/// </summary>
[Collection("Integration")]
public class AccountStateTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private async Task<(HttpClient Browser, TestUser User, string Cookie)> SignedInAsync(
        Action<global::Auth.Domain.Identity.ApplicationUser>? configure = null)
    {
        var browser = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker", configure: configure);
        return (browser, user, await BffTestSupport.SignInAsync(browser, user));
    }

    /// <summary>The per-user view is cached for 30 s per node; tests that change the account drop it.</summary>
    private async Task ForgetCachedUserAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<AuthorizationClaimsBuilder>().Invalidate(userId);
    }

    [Fact]
    public async Task A_deactivated_account_is_refused_on_its_next_request_and_the_session_ends_as_Deactivated()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);
        await ForgetCachedUserAsync(user.Id);

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // The rejection comes from ValidatePrincipal, not the ticket store: its 401 must still carry the hint.
        response.Headers.GetValues("X-Session-End").Should().Equal("ended");
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Deactivated);
    }

    [Fact]
    public async Task A_closed_access_window_ends_the_session_as_AccessExpired()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var (browser, user, cookie) = await SignedInAsync(u => u.AccessExpiresAt = now.AddHours(1));
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.AccessExpiresAt = now.AddMinutes(-1));
        await ForgetCachedUserAsync(user.Id);

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.AccessExpired);
    }

    [Fact]
    public async Task Making_a_signed_in_account_temporary_access_ends_the_session_as_AccessExpired()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Through the real handler, and with no manual cache invalidation: the handler must do both.
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var current = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new UpdateUserCommand(user.Id, current.FirstName!, current.LastName!, current.Email!, null, null, null, null,
                    IsTemporaryAccess: true));
        }

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.AccessExpired);
    }

    [Fact]
    public async Task Moving_a_user_to_a_company_reaches_their_next_request_without_waiting_for_the_claims_cache()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        using (var before = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie))
        {
            using var json = await BffTestSupport.ReadJsonAsync(before);
            json.RootElement.GetProperty("companyId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        }

        // Through the real handler, and with no manual cache invalidation: the handler must do it.
        var companyId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var current = await db.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new UpdateUserCommand(user.Id, current.FirstName!, current.LastName!, current.Email!, null, null, null,
                    companyId));
        }

        using var after = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        using var afterJson = await BffTestSupport.ReadJsonAsync(after);
        afterJson.RootElement.GetProperty("companyId").GetGuid().Should().Be(companyId);
    }

    [Fact]
    public async Task A_deleted_account_ends_the_session_as_Revoked()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<global::Auth.Domain.Identity.ApplicationUser>>();
            await users.DeleteAsync((await users.FindByIdAsync(user.Id.ToString()))!);
        }

        await ForgetCachedUserAsync(user.Id);

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Revoked);
    }

    [Fact]
    public async Task A_forced_password_change_blocks_everything_but_the_change_password_calls()
    {
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;

        using var blocked = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("Password change required");

        using var me = await BffTestSupport.GetAsync(browser, "/auth/me", cookie);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var meJson = await BffTestSupport.ReadJsonAsync(me);
        meJson.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();

        using var policy = await BffTestSupport.GetAsync(browser, "/auth/password-policy", cookie);
        policy.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_your_own_password_clears_the_gate_keeps_this_session_and_ends_the_others()
    {
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;
        var otherDevice = await BffTestSupport.SignInAsync(browser, user);
        var sessions = (await BffTestSupport.GetSessionsAsync(Services, user.Id)).OrderBy(s => s.CreatedAt).ToList();

        var change = BffTestSupport.SpaRequest(HttpMethod.Post, $"/auth/users/{user.Id}/change-password");
        change.Content = JsonContent.Create(new
        {
            currentPassword = user.Password,
            newPassword = "N3w!Passw0rd#77",
            confirmPassword = "N3w!Passw0rd#77"
        });
        using var changed = await BffTestSupport.SendAsync(browser, change, cookie);
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent, await changed.Content.ReadAsStringAsync());

        // This session carries on, and is no longer gated (the cached "must change" view was dropped).
        using var stillIn = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        stillIn.StatusCode.Should().Be(HttpStatusCode.OK);
        // The other device was signed out.
        using var other = await BffTestSupport.GetAsync(browser, "/test/whoami", otherDevice);
        other.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var after = await BffTestSupport.GetSessionsAsync(Services, user.Id);
        after.Single(s => s.Id == sessions[0].Id).EndedAt.Should().BeNull();
        after.Single(s => s.Id == sessions[1].Id).EndReason.Should().Be(SessionEndReason.PasswordChanged);
    }

    [Fact]
    public async Task An_expired_password_gates_a_live_session_and_persists_the_flag()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var (browser, user, cookie) = await SignedInAsync(u => u.PasswordChangedAt = now.AddDays(-40));
        using var _ = browser;

        int updated;
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            updated = await db.Database.ExecuteSqlRawAsync("UPDATE auth.PasswordPolicy SET ExpiryDays = 30");
            scope.ServiceProvider.GetRequiredService<IPasswordPolicyProvider>().Invalidate();
        }

        try
        {
            Assert.SkipUnless(updated > 0, "No password-policy row to set an expiry on.");
            await ForgetCachedUserAsync(user.Id);

            using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            (await db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.MustChangePassword).SingleAsync())
                .Should().BeTrue();
        }
        finally
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>()
                .Database.ExecuteSqlRawAsync("UPDATE auth.PasswordPolicy SET ExpiryDays = 0");
            scope.ServiceProvider.GetRequiredService<IPasswordPolicyProvider>().Invalidate();
        }
    }

    [Fact]
    public async Task Me_reports_the_access_window_end_as_local_time_without_a_Z_and_omits_it_for_ordinary_accounts()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var end = now.AddHours(2).AddTicks(1234567); // sub-second part must not leak out
        var (windowBrowser, _, windowCookie) = await SignedInAsync(u => u.AccessExpiresAt = end);
        var (plainBrowser, _, plainCookie) = await SignedInAsync();
        using var _1 = windowBrowser;
        using var _2 = plainBrowser;

        using var withWindow = await BffTestSupport.GetAsync(windowBrowser, "/auth/me", windowCookie);
        using var withoutWindow = await BffTestSupport.GetAsync(plainBrowser, "/auth/me", plainCookie);

        using var windowJson = await BffTestSupport.ReadJsonAsync(withWindow);
        windowJson.RootElement.GetProperty("accessExpiresAt").GetString()
            .Should().Be(end.ToString("s")).And.NotEndWith("Z");
        using var plainJson = await BffTestSupport.ReadJsonAsync(withoutWindow);
        plainJson.RootElement.TryGetProperty("accessExpiresAt", out _).Should().BeFalse();
    }

    [Fact]
    public async Task While_a_password_change_is_owed_sign_in_sign_out_and_the_idp_stay_reachable()
    {
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;

        using var login = await BffTestSupport.GetAsync(browser, "/auth/login", cookie);
        login.StatusCode.Should().Be(HttpStatusCode.Found);
        login.Headers.Location!.ToString().Should().StartWith("https://localhost:7111/connect/authorize");

        using var loginPage = await BffTestSupport.GetAsync(browser, "/Account/Login", cookie);
        loginPage.StatusCode.Should().Be(HttpStatusCode.Found); // bounced to the SPA, not 403

        using var logout = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"), cookie);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.Logout);
    }

    [Fact]
    public async Task A_password_changed_through_another_node_ungates_this_one_at_once()
    {
        // This node cached "must change" for the user; the change was made elsewhere, so nothing here
        // invalidated it. A cached "must change" is never trusted, so the next request re-reads the database.
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;
        using var gated = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        gated.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.MustChangePassword = false);

        using var after = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task For_a_user_who_owes_a_change_only_the_flag_is_re_read_not_the_whole_claim_set()
    {
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;
        using var gated = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        gated.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Clear the flag and take the role away, both behind this node's back. The flag is read fresh; the
        // claims are the cached ones until their normal 30 s run out.
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.MustChangePassword = false);
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<global::Auth.Domain.Identity.ApplicationUser>>();
            await users.RemoveFromRoleAsync((await users.FindByIdAsync(user.Id.ToString()))!, "RequestMaker");
        }

        using var after = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);

        after.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(after);
        json.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Contain("RequestMaker");
    }

    [Fact]
    public async Task A_window_extended_through_another_node_keeps_the_session_alive_past_the_old_expiry()
    {
        // This node cached "usable until T". The window was then extended elsewhere (nothing here was told).
        // When T passes, ending the session on the cached copy would cut the user off; the database is asked first.
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var (browser, user, cookie) = await SignedInAsync(u => u.AccessExpiresAt = now.AddSeconds(4));
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.AccessExpiresAt = now.AddHours(1));
        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var after = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_window_that_really_runs_out_still_ends_the_session()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var (browser, user, cookie) = await SignedInAsync(u => u.AccessExpiresAt = now.AddSeconds(4));
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var after = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).EndReason.Should().Be(SessionEndReason.AccessExpired);
    }

    [Fact]
    public async Task A_reactivated_account_is_not_cut_off_by_a_cached_deactivation()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = false);
        await ForgetCachedUserAsync(user.Id);
        // Cache "inactive" on this node without ending the session: a different session of the same user asks first.
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AuthorizationClaimsBuilder>()
                .ReloadAsync(user.Id, CancellationToken.None);
        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.IsActive = true);

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_password_expiry_gate_holds_even_if_the_flag_was_never_persisted()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var (browser, user, cookie) = await SignedInAsync(u => u.PasswordChangedAt = now.AddDays(-40));
        using var _ = browser;
        int updated;
        await using (var scope = Services.CreateAsyncScope())
        {
            updated = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database
                .ExecuteSqlRawAsync("UPDATE auth.PasswordPolicy SET ExpiryDays = 30");
            scope.ServiceProvider.GetRequiredService<IPasswordPolicyProvider>().Invalidate();
        }

        try
        {
            Assert.SkipUnless(updated > 0, "No password-policy row to set an expiry on.");
            await ForgetCachedUserAsync(user.Id);
            (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Simulate the best-effort UPDATE having failed: the flag reads false while the password is expired.
            await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.MustChangePassword = false);

            using var again = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
            again.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database
                .ExecuteSqlRawAsync("UPDATE auth.PasswordPolicy SET ExpiryDays = 0");
            scope.ServiceProvider.GetRequiredService<IPasswordPolicyProvider>().Invalidate();
        }
    }

    [Fact]
    public async Task Clearing_the_flag_does_not_give_the_old_claims_a_fresh_lifetime()
    {
        var (browser, user, cookie) = await SignedInAsync(u => u.MustChangePassword = true);
        using var _ = browser;
        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden); // loads + caches

        await BffTestSupport.UpdateUserAsync(Services, user.Id, u => u.MustChangePassword = false);
        await using (var scope = Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<global::Auth.Domain.Identity.ApplicationUser>>();
            await users.RemoveFromRoleAsync((await users.FindByIdAsync(user.Id.ToString()))!, "RequestMaker");
        }

        (await BffTestSupport.GetAsync(browser, "/test/whoami", cookie)).StatusCode.Should().Be(HttpStatusCode.OK); // flag-only refresh

        // Past the ORIGINAL 30 s the claims are read again and the role is gone. A fresh lifetime granted at the
        // refresh would have kept serving the old roles until 30 s after it.
        await Task.Delay(TimeSpan.FromSeconds(31), TestContext.Current.CancellationToken);
        using var later = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        later.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(later);
        json.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().NotContain("RequestMaker");
    }
}
