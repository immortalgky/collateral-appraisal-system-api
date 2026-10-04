using System.Net;
using System.Text;
using FluentAssertions;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.Tests;

/// <summary>
/// <see cref="FailedMessageCollectorService.FetchManagementQueuesAsync"/>
/// is the ONE Management API call per round, feeding both Discover and Snapshot. It classifies its
/// failures: 401/403 → Unauthorized; connect/timeout → Unreachable with the cooldown; a malformed payload
/// → logged and used as a THIS-ROUND-ONLY fallback, with NO status change/cooldown; shutdown cancellation
/// → rethrown. Uses the same fake-handler approach as the existing cooldown tests
/// (<see cref="FailedMessageCollectorManagementCooldownTests"/>), just wired to a real <see cref="HttpClient"/>.
/// </summary>
public class FailedMessageManagementFetchTests
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        // Captures the fully-resolved URI (BaseAddress + relative request path
        // combined) so a test can assert the actual outgoing request, not just the response handling.
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw exception;
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler, Uri? baseAddress = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = baseAddress ?? new Uri("http://localhost:15672/") };
    }

    private static FailedMessageCollectorService CreateCollector(
        IHttpClientFactory httpClientFactory, IDateTimeProvider? dateTimeProvider = null,
        string rabbitHost = "amqp://localhost:5672/")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = rabbitHost,
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        var clock = dateTimeProvider ?? Substitute.For<IDateTimeProvider>();
        if (dateTimeProvider is null)
            clock.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));

        return new FailedMessageCollectorService(
            Substitute.For<IServiceScopeFactory>(),
            httpClientFactory,
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            clock,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// A ManagementUrl with a path prefix (a management API reachable behind a reverse
    /// proxy at a sub-path, e.g. "http://host:15672/rabbit") must still work. This requires BOTH halves of
    /// the fix together — BaseAddress ending with '/' (IntegrationModule's client configuration) AND the
    /// request path having no leading '/' (FetchManagementQueuesAsync) — either alone still drops the
    /// prefix per RFC 3986 §5.3's relative-reference resolution rules.
    /// </summary>
    [Fact]
    public async Task FetchManagementQueuesAsync_BaseAddressHasPathPrefix_PreservesPrefixInFinalUri()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        // Mirrors IntegrationModule's normalisation: ManagementUrl "http://host:15672/rabbit" becomes a
        // BaseAddress ending with '/'.
        var baseAddress = new Uri("http://localhost:15672/rabbit/");
        var collector = CreateCollector(new FakeHttpClientFactory(handler, baseAddress));

        await collector.FetchManagementQueuesAsync(CancellationToken.None);

        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.AbsolutePath.Should().StartWith("/rabbit/api/queues/",
            "the path prefix must survive combining with the relative request path, not be replaced by it");
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_Unauthorized_ReturnsUnauthorizedStatus_StartsCooldown()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.Unauthorized, ""));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        fetch.Status.Should().Be(BrokerManagementStatus.Unauthorized);
        fetch.FaultQueues.Should().BeNull();
        collector.CachedManagementStatus.Should().Be(BrokerManagementStatus.Unauthorized);
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_ConnectionFailure_ReturnsUnreachable_StartsCooldown()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        fetch.Status.Should().Be(BrokerManagementStatus.Unreachable);
        collector.IsManagementCoolingDown(now).Should().BeTrue("a transport failure must start the cooldown");
    }

    /// <summary>
    /// A trailing slash on the vhost path ("rabbitmq://mq/cas/") used to give vhost "cas/", so the
    /// Management call asked for the non-existent vhost "cas%2F", got a 404, and the broker was reported
    /// Unreachable with a 10-minute cooldown forever. The slash must be trimmed; an empty path stays "/".
    /// </summary>
    [Theory]
    [InlineData("amqp://mq:5672", "/")]
    [InlineData("amqp://mq:5672/", "/")]
    [InlineData("amqp://mq:5672/cas", "cas")]
    [InlineData("amqp://mq:5672/cas/", "cas")]
    [InlineData("amqp://mq:5672/cas//", "cas")]
    [InlineData("amqp://mq:5672/%2F", "/")]
    public void ParseVirtualHost_TrimsTrailingSlash_KeepsDefaultVhost(string uri, string expected)
    {
        FailedMessageCollectorService.ParseVirtualHost(new Uri(uri)).Should().Be(expected);
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_VhostWithTrailingSlash_AsksForTheRealVhost()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        var collector = CreateCollector(new FakeHttpClientFactory(handler), rabbitHost: "rabbitmq://mq/cas/");

        await collector.FetchManagementQueuesAsync(CancellationToken.None);

        handler.LastRequestUri!.OriginalString.Should().Contain("api/queues/cas?")
            .And.NotContain("cas%2F");
    }

    /// <summary>
    /// BrokerManagementStatus has no "NotFound" state, so a 404 stays Unreachable — but LastError must say
    /// the real cause (the HTTP status and the vhost asked for), so an operator isn't left guessing.
    /// </summary>
    [Fact]
    public async Task FetchManagementQueuesAsync_NotFound_LastErrorCarriesTheHttpStatus()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.NotFound, ""));
        var collector = CreateCollector(new FakeHttpClientFactory(handler), rabbitHost: "amqp://mq:5672/cas");

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        fetch.Status.Should().Be(BrokerManagementStatus.Unreachable);
        fetch.LastError.Should().Contain("404").And.Contain("Not Found").And.Contain("'cas'");
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_DuringCooldown_KeepsTheLastRealError()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));

        await collector.FetchManagementQueuesAsync(CancellationToken.None);
        var second = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        handler.CallCount.Should().Be(1, "the second round is inside the cooldown — no HTTP call");
        second.LastError.Should().Contain("cooldown active")
            .And.Contain("connection refused", "the real error must stay visible while the cooldown hides the call");
    }

    /// <summary>
    /// A broker outage fails BOTH the Management fetch (cooldown) and the AMQP connection. When AMQP
    /// recovers the broker is back, so the cooldown must be dropped — otherwise queue health stays
    /// "Unreachable / no data" for up to 10 more minutes.
    /// </summary>
    [Fact]
    public async Task FetchManagementQueuesAsync_AmqpFailsThenRecovers_CooldownIsCleared_NextFetchCallsTheApi()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        await collector.FetchManagementQueuesAsync(CancellationToken.None);
        collector.IsManagementCoolingDown(now).Should().BeTrue();

        collector.RecordAmqpConnectResult(new InvalidOperationException("broker down"));
        collector.RecordAmqpConnectResult(null);

        collector.IsManagementCoolingDown(now).Should().BeFalse("AMQP recovery means the broker is back");
        await collector.FetchManagementQueuesAsync(CancellationToken.None);
        handler.CallCount.Should().Be(2, "the next round must hit the Management API again, not wait 10 minutes");
    }

    /// <summary>
    /// The steady-state "no management tag" case: AMQP never fails, so no recovery event fires and the
    /// 10-minute cooldown must hold even though every round reports a healthy connection.
    /// </summary>
    [Fact]
    public async Task FetchManagementQueuesAsync_Unauthorized_AmqpStaysHealthy_CooldownHoldsUntilItExpires()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.Unauthorized, ""));
        var clock = Substitute.For<IDateTimeProvider>();
        var start = new DateTime(2026, 1, 1, 9, 0, 0);
        clock.ApplicationNow.Returns(start);
        var collector = CreateCollector(new FakeHttpClientFactory(handler), clock);

        await collector.FetchManagementQueuesAsync(CancellationToken.None);
        for (var round = 0; round < 3; round++)
        {
            collector.RecordAmqpConnectResult(null);
            await collector.FetchManagementQueuesAsync(CancellationToken.None);
        }

        handler.CallCount.Should().Be(1, "a healthy AMQP connection is not a recovery — the cooldown stays");

        clock.ApplicationNow.Returns(start.AddMinutes(10).AddSeconds(1));
        await collector.FetchManagementQueuesAsync(CancellationToken.None);
        handler.CallCount.Should().Be(2, "once the cooldown expires the API is called again");
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_MalformedPayload_NoStatusChange_NoCooldown()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "{ not valid json"));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));
        var now = new DateTime(2026, 1, 1, 9, 0, 0);

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        fetch.Status.Should().BeNull("connection and auth both worked — a shape error changes nothing");
        fetch.FaultQueues.Should().BeNull();
        collector.IsManagementCoolingDown(now).Should()
            .BeFalse("a malformed payload must not start a cooldown — only THIS round falls back");
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_CancelledDuringShutdown_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        var handler = new ThrowingHttpMessageHandler(new OperationCanceledException());
        var collector = CreateCollector(new FakeHttpClientFactory(handler));
        await cts.CancelAsync();

        var act = async () => await collector.FetchManagementQueuesAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchManagementQueuesAsync_HealthyPayload_DerivesFaultQueuesAndQueuesJson_OneCallOnly()
    {
        const string body = """
            [
              {"name":"appraisal-sync","messages":0,"messages_ready":2,"messages_unacknowledged":0,"consumers":1},
              {"name":"appraisal-sync_error","messages":3},
              {"name":"appraisal-sync_skipped","messages":0}
            ]
            """;
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, body));
        var collector = CreateCollector(new FakeHttpClientFactory(handler));

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);

        fetch.Status.Should().Be(BrokerManagementStatus.Ok);
        fetch.FaultQueues.Should().ContainSingle().Which.Should().Be("appraisal-sync_error");
        fetch.QueuesJson.Should().Contain("appraisal-sync").And.NotContain("appraisal-sync_error");
        handler.CallCount.Should().Be(1, "exactly one Management API call for this fetch");
    }

    /// <summary>
    /// Snapshot writes straight from the SAME fetch Discover already consumed — no second
    /// Management API call. Combined with <see cref="FetchManagementQueuesAsync_HealthyPayload_DerivesFaultQueuesAndQueuesJson_OneCallOnly"/>
    /// (which proves the fetch itself is exactly one HTTP call), this proves a full round makes exactly
    /// one Management API call in total.
    /// </summary>
    [Fact]
    public async Task SnapshotAsync_WritesFromTheFetchResult_NoSecondHttpCall()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "[]"));
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 1, 1, 9, 0, 0));

        // The db name must be computed ONCE outside the options delegate — AddDbContext re-invokes the
        // delegate for every scope's DbContext instance, so a Guid.NewGuid() call INSIDE it would hand
        // each scope its own random (and therefore invisible-to-each-other) in-memory database.
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<IntegrationDbContext>(o => o.UseInMemoryDatabase(dbName));
        await using var provider = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "amqp://localhost:5672/",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest",
            })
            .Build();

        var collector = new FailedMessageCollectorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeHttpClientFactory(handler),
            Substitute.For<ILogger<FailedMessageCollectorService>>(),
            dateTimeProvider,
            Options.Create(new FailedMessagesOptions()),
            configuration,
            new ReceiveEndpointDiscoveryObserver());

        var fetch = await collector.FetchManagementQueuesAsync(CancellationToken.None);
        handler.CallCount.Should().Be(1);

        await collector.SnapshotAsync(fetch, CancellationToken.None);

        // Snapshot must not have made any HTTP call of its own.
        handler.CallCount.Should().Be(1);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var snapshot = await db.BrokerSnapshots.SingleAsync(TestContext.Current.CancellationToken);
        snapshot.ManagementStatus.Should().Be(BrokerManagementStatus.Ok);
    }
}
