using System.Net;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Sessions;
using Auth.Infrastructure;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Time;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>Server-side sessions: the cookie is only a key, the row decides.</summary>
[Collection("Integration")]
public class SessionStoreTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private DateTime Now => Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;

    private async Task<(HttpClient Browser, TestUser User, string Cookie)> SignedInAsync()
    {
        var browser = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        var user = await BffTestSupport.CreateUserAsync(Services, "RequestMaker");
        return (browser, user, await BffTestSupport.SignInAsync(browser, user));
    }

    [Fact]
    public async Task Sign_in_creates_a_session_row_and_a_session_started_audit_entry()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;

        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        session.EndedAt.Should().BeNull();
        session.Ticket.Should().NotBeEmpty();
        session.UserId.Should().Be(user.Id);

        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            (await db.AuthAuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityId == user.Id && a.Action == AuditAction.SessionStarted)).Should().BeTrue();
        }

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_ends_the_row_and_a_replayed_cookie_is_refused()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;

        using var logout = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"), cookie);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = await BffTestSupport.ReadJsonAsync(logout);
        var url = body.RootElement.GetProperty("url").GetString()!;
        // The IdP end-session URL names the PUBLIC origin even though discovery ran against the internal one.
        url.Should().StartWith("https://localhost:7111/connect/logout")
            .And.Contain("client_id=spa")
            .And.Contain("post_logout_redirect_uri=" + Uri.EscapeDataString("https://localhost:7111/signout-callback-oidc"));

        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        session.EndReason.Should().Be(SessionEndReason.Logout);
        session.EndedAt.Should().NotBeNull();

        // The very same cookie, copied before logout, is dead — no 302 either.
        using var replay = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        replay.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Logout_without_a_session_still_answers_with_the_idp_url()
    {
        using var browser = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        using var response = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"), cookie: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_session_idle_for_the_timeout_is_refused_and_ended_as_Idle()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        var lastActivity = Now.AddMinutes(-31);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = lastActivity);

        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var ended = await BffTestSupport.GetSessionAsync(Services, user.Id);
        ended.EndReason.Should().Be(SessionEndReason.Idle);
        // Recorded as ending when the idle window ran out, not when someone noticed.
        ended.EndedAt.Should().BeCloseTo(lastActivity.AddMinutes(30), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Background_polling_does_not_extend_the_session_but_real_activity_does()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        var lastActivity = Now.AddMinutes(-29);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = lastActivity);

        using var polling = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie, background: true);
        polling.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).LastActivityAt
            .Should().BeCloseTo(lastActivity, TimeSpan.FromMilliseconds(5));

        using var click = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        click.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).LastActivityAt
            .Should().BeCloseTo(Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_refused_request_says_whether_the_session_idled_or_was_ended()
    {
        // No cookie at all: nothing to report.
        using var anonymous = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        using var none = await BffTestSupport.GetAsync(anonymous, "/test/whoami", null);
        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        none.Headers.Contains("X-Session-End").Should().BeFalse();

        // Idled: reported on the request that notices it and on every one after.
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = Now.AddMinutes(-31));
        for (var i = 0; i < 2; i++)
        {
            using var idled = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
            idled.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            idled.Headers.GetValues("X-Session-End").Should().Equal("idle");
        }

        // Signed out (here, as in another tab): the user's own action, so nothing to report.
        var (browser3, user3, cookie3) = await SignedInAsync();
        using var ___ = browser3;
        using (var logout = await BffTestSupport.SendAsync(browser3,
                   BffTestSupport.SpaRequest(HttpMethod.Post, "/auth/logout"), cookie3))
            logout.StatusCode.Should().Be(HttpStatusCode.OK);
        for (var i = 0; i < 2; i++) // the second one comes from the cached "missing" entry
        {
            using var signedOut = await BffTestSupport.GetAsync(browser3, "/test/whoami", cookie3);
            signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            signedOut.Headers.Contains("X-Session-End").Should().BeFalse();
        }

        // Revoked by an administrator: "ended", and readable cross-origin.
        var (browser2, user2, cookie2) = await SignedInAsync();
        using var __ = browser2;
        var revoked = await BffTestSupport.GetSessionAsync(Services, user2.Id);
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUserSessionService>()
                .EndAsync(revoked.Id, SessionEndReason.Revoked);
        using var ended = await BffTestSupport.SendAsync(browser2,
            BffTestSupport.SpaRequest(HttpMethod.Get, "/test/whoami"), cookie2);
        ended.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ended.Headers.GetValues("X-Session-End").Should().Equal("ended");
        ended.Headers.GetValues("Access-Control-Expose-Headers").Single().Should().Contain("X-Session-End");
    }

    [Fact]
    public async Task Polling_alone_cannot_keep_a_session_alive_past_the_timeout()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = Now.AddMinutes(-31));

        using var polling = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie, background: true);

        polling.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Activity_is_written_at_most_once_a_minute()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;

        for (var i = 0; i < 3; i++)
        {
            using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        session.LastActivityAt.Should().Be(session.CreatedAt);
    }

    [Fact]
    public async Task Revoking_a_user_ends_every_session_except_the_one_kept()
    {
        var (browser, user, first) = await SignedInAsync();
        using var _ = browser;
        var second = await BffTestSupport.SignInAsync(browser, user);
        var kept = (await BffTestSupport.GetSessionsAsync(Services, user.Id)).OrderBy(s => s.CreatedAt).Last();

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserSessionService>().RevokeUserAsync(
                user.Id, user.UserName, SessionEndReason.PasswordChanged, exceptSessionId: kept.Id);
        }

        using var firstResponse = await BffTestSupport.GetAsync(browser, "/test/whoami", first);
        using var secondResponse = await BffTestSupport.GetAsync(browser, "/test/whoami", second);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await BffTestSupport.GetSessionsAsync(Services, user.Id);
        sessions.Single(s => s.Id != kept.Id).EndReason.Should().Be(SessionEndReason.PasswordChanged);
        sessions.Single(s => s.Id == kept.Id).EndedAt.Should().BeNull();

        await using var again = Services.CreateAsyncScope();
        await again.ServiceProvider.GetRequiredService<IUserSessionService>()
            .RevokeUserAsync(user.Id, user.UserName, SessionEndReason.Revoked);
        using var afterRevokeAll = await BffTestSupport.GetAsync(browser, "/test/whoami", second);
        afterRevokeAll.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ended_sessions_are_reported_to_the_signalr_sweeper()
    {
        var (browser, user, _) = await SignedInAsync();
        using var __ = browser;
        var live = await BffTestSupport.GetSessionAsync(Services, user.Id);
        await BffTestSupport.SignInAsync(browser, user);
        var other = (await BffTestSupport.GetSessionsAsync(Services, user.Id)).Single(s => s.Id != live.Id);
        await using var scope = Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IUserSessionService>();
        await sessions.EndAsync(other.Id, SessionEndReason.Logout);

        var ended = await sessions.GetEndedAsync([live.Id, other.Id, Guid.NewGuid()], CancellationToken.None);

        ended.Should().Contain(other.Id).And.NotContain(live.Id);
    }

    [Fact]
    public async Task The_hangfire_dashboards_real_stats_poll_does_not_extend_the_session_but_opening_it_does()
    {
        // Drives the dashboard the way its own JavaScript does: open the page, read the poll URL and CSRF token
        // from it, then POST the metrics as a jQuery XHR with the cookie jar a browser would hold.
        using var browser = fixture.BffWebApplicationFactory.CreateBrowser();
        var user = await BffTestSupport.CreateUserAsync(Services, "Admin");
        await BffTestSupport.SignInAsync(browser, user);
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);

        string html;
        using (var page = await browser.GetAsync("/hangfire", TestContext.Current.CancellationToken))
        {
            page.StatusCode.Should().Be(HttpStatusCode.OK);
            html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        var pollUrl = System.Text.RegularExpressions.Regex.Match(html, "data-pollurl=\"([^\"]+)\"").Groups[1].Value;
        var csrfHeader = System.Text.RegularExpressions.Regex.Match(html, "name=\"csrf-header\" content=\"([^\"]+)\"").Groups[1].Value;
        var csrfToken = System.Text.RegularExpressions.Regex.Match(html, "name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value;
        pollUrl.Should().Be("/hangfire/stats");

        // Opening the page counted. Now age the session and forget this node's cached copy, so that only a
        // request that would count can move LastActivityAt.
        var lastActivity = Now.AddMinutes(-5);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = lastActivity);
        Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            .Remove(UserSessionService.CacheKey(session.Id));

        for (var i = 0; i < 3; i++)
        {
            var poll = new HttpRequestMessage(HttpMethod.Post, pollUrl)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("metrics[]", "servers:count"),
                    new KeyValuePair<string, string>("metrics[]", "enqueued:count-or-null")
                ])
            };
            poll.Headers.Add("Origin", "https://localhost:7111");
            poll.Headers.Add("Referer", "https://localhost:7111/hangfire");
            poll.Headers.Add("X-Requested-With", "XMLHttpRequest");
            poll.Headers.Add(csrfHeader, csrfToken);
            using var response = await browser.SendAsync(poll, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        (await BffTestSupport.GetSessionAsync(Services, user.Id)).LastActivityAt
            .Should().BeCloseTo(lastActivity, TimeSpan.FromMilliseconds(5));

        // A button press or opening another dashboard page is a person.
        using var open = await browser.GetAsync("/hangfire/recurring", TestContext.Current.CancellationToken);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).LastActivityAt
            .Should().BeCloseTo(Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SignalR_transport_traffic_does_not_extend_the_session()
    {
        var (browser, user, cookie) = await SignedInAsync();
        using var _ = browser;
        var session = await BffTestSupport.GetSessionAsync(Services, user.Id);
        var lastActivity = Now.AddMinutes(-5);
        await BffTestSupport.UpdateSessionAsync(Services, session.Id, s => s.LastActivityAt = lastActivity);
        Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()
            .Remove(UserSessionService.CacheKey(session.Id));

        using var negotiate = await BffTestSupport.SendAsync(browser,
            BffTestSupport.SpaRequest(HttpMethod.Post, "/notificationHub/negotiate?negotiateVersion=1"), cookie);

        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BffTestSupport.GetSessionAsync(Services, user.Id)).LastActivityAt
            .Should().BeCloseTo(lastActivity, TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task Revoking_gives_the_reason_only_to_live_sessions_and_ends_stale_open_rows_as_Idle()
    {
        var (browser, user, _) = await SignedInAsync();
        using var _1 = browser;
        await BffTestSupport.SignInAsync(browser, user);
        await BffTestSupport.SignInAsync(browser, user);
        var rows = (await BffTestSupport.GetSessionsAsync(Services, user.Id)).OrderBy(r => r.CreatedAt).ToList();
        var idle = TimeSpan.FromMinutes(30);
        var staleActivity = Now.AddHours(-3);
        await BffTestSupport.UpdateSessionAsync(Services, rows[2].Id, s => s.LastActivityAt = staleActivity);

        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserSessionService>().RevokeUserAsync(
                user.Id, user.UserName, SessionEndReason.Deactivated, exceptSessionId: rows[0].Id);
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().SaveChangesAsync();
        }

        var after = (await BffTestSupport.GetSessionsAsync(Services, user.Id)).ToDictionary(r => r.Id);
        after[rows[0].Id].EndedAt.Should().BeNull(); // excepted
        after[rows[1].Id].EndReason.Should().Be(SessionEndReason.Deactivated); // live
        after[rows[2].Id].EndReason.Should().Be(SessionEndReason.Idle); // already over when the admin acted
        after[rows[2].Id].EndedAt.Should().BeCloseTo(staleActivity + idle, TimeSpan.FromMilliseconds(5));

        await using var check = Services.CreateAsyncScope();
        var audit = await check.ServiceProvider.GetRequiredService<AuthDbContext>().AuthAuditLogs.AsNoTracking()
            .Where(a => a.EntityId == user.Id && a.Action == AuditAction.SessionEnded)
            .Select(a => a.ChangesJson).ToListAsync();
        audit.Should().ContainSingle().Which.Should().Contain("\"count\":1");
    }
}
