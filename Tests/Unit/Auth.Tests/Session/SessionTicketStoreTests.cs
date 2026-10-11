using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Sessions;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Time;

namespace Auth.Tests.Session;

public class SessionTicketStoreTests
{
    private static readonly DateTime T0 = new(2026, 10, 10, 9, 0, 0);

    private readonly IUserSessionService _sessions = Substitute.For<IUserSessionService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly SessionTicketStore _store;
    private readonly Guid _userId = Guid.NewGuid();
    private byte[] _blob = [];
    private Guid _sessionId;
    private string _key = "";

    public SessionTicketStoreTests()
    {
        _clock.ApplicationNow.Returns(T0);
        _sessions.TouchAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        var services = new ServiceCollection().AddSingleton(_sessions).BuildServiceProvider();
        _store = new SessionTicketStore(
            services.GetRequiredService<IServiceScopeFactory>(),
            new MemoryCache(new MemoryCacheOptions()),
            new EphemeralDataProtectionProvider(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Microsoft.Extensions.Options.Options.Create(new BrowserSessionOptions()),
            _clock,
            NullLogger<SessionTicketStore>.Instance);
    }

    /// <summary>Starts a session whose row the fake database reports as last active at <paramref name="lastActivity"/>.</summary>
    private async Task StartSessionAsync(DateTime lastActivity)
    {
        _sessions.CreateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Do<byte[]>(b => _blob = b),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var identity = new ClaimsIdentity("test");
        identity.AddClaim(new Claim("sub", _userId.ToString()));
        _key = await _store.StoreAsync(new AuthenticationTicket(new ClaimsPrincipal(identity), "test"));
        _sessionId = Guid.Parse(_key);
        ReportRow(lastActivity);
    }

    private void ReportRow(DateTime lastActivity) =>
        _sessions.GetActiveAsync(_sessionId, Arg.Any<CancellationToken>())
            .Returns(new SessionSnapshot(_userId, _blob, lastActivity));

    private void At(TimeSpan afterT0) => _clock.ApplicationNow.Returns(T0 + afterT0);

    private int RowReads => _sessions.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IUserSessionService.GetActiveAsync));

    [Fact]
    public async Task Hits_inside_the_cache_lifetime_do_not_read_the_row_again()
    {
        await StartSessionAsync(T0);

        foreach (var second in new[] { 0, 3, 6 })
        {
            At(TimeSpan.FromSeconds(second));
            Assert.NotNull(await _store.RetrieveAsync(_key));
        }

        Assert.Equal(1, RowReads);
    }

    [Fact]
    public async Task A_busy_session_cannot_keep_a_stale_entry_alive_past_the_cache_lifetime()
    {
        await StartSessionAsync(T0);

        // A request every second for 25 s. If a hit refreshed the entry it would be read once, ever.
        for (var second = 0; second <= 25; second++)
        {
            At(TimeSpan.FromSeconds(second));
            Assert.NotNull(await _store.RetrieveAsync(_key));
        }

        // Loaded at 0, 10 and 20 s.
        Assert.Equal(3, RowReads);
    }

    [Fact]
    public async Task A_revoke_on_another_node_is_seen_within_the_cache_lifetime_even_under_continuous_traffic()
    {
        await StartSessionAsync(T0);
        Assert.NotNull(await _store.RetrieveAsync(_key));

        _sessions.GetActiveAsync(_sessionId, Arg.Any<CancellationToken>()).Returns((SessionSnapshot?)null);

        At(SessionTicketStore.CacheLifetime - TimeSpan.FromSeconds(1));
        Assert.NotNull(await _store.RetrieveAsync(_key)); // still within what the cache may say
        At(SessionTicketStore.CacheLifetime);
        Assert.Null(await _store.RetrieveAsync(_key));
    }

    [Fact]
    public async Task A_stale_cached_activity_time_cannot_end_a_session_another_node_has_kept_alive()
    {
        var idle = new BrowserSessionOptions().IdleTimeout;
        // The row was last active 2 s short of the timeout when this node loaded it...
        await StartSessionAsync(T0 - idle + TimeSpan.FromSeconds(2));
        Assert.NotNull(await _store.RetrieveAsync(_key));

        // ...the other node has served it since, and 5 s later this node's cached time says "idle".
        At(TimeSpan.FromSeconds(5));
        ReportRow(T0 + TimeSpan.FromSeconds(3));

        Assert.NotNull(await _store.RetrieveAsync(_key));
        await _sessions.DidNotReceive().EndAsync(Arg.Any<Guid>(), Arg.Any<SessionEndReason>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_genuinely_idle_session_is_ended_with_the_time_it_went_idle()
    {
        var idle = new BrowserSessionOptions().IdleTimeout;
        await StartSessionAsync(T0 - idle - TimeSpan.FromMinutes(1));

        Assert.Null(await _store.RetrieveAsync(_key));

        await _sessions.Received(1).EndAsync(_sessionId, SessionEndReason.Idle, T0 - TimeSpan.FromMinutes(1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_dead_session_id_is_remembered_briefly_so_a_polling_tab_does_not_hit_the_database()
    {
        await StartSessionAsync(T0);
        _sessions.GetActiveAsync(_sessionId, Arg.Any<CancellationToken>()).Returns((SessionSnapshot?)null);

        foreach (var second in new[] { 0, 2, 4, 6, 8 })
        {
            At(TimeSpan.FromSeconds(second));
            Assert.Null(await _store.RetrieveAsync(_key));
        }

        Assert.Equal(1, RowReads);

        // ...and asked again once the memory has run out.
        At(SessionTicketStore.CacheLifetime + TimeSpan.FromSeconds(1));
        Assert.Null(await _store.RetrieveAsync(_key));
        Assert.Equal(2, RowReads);
    }
}
