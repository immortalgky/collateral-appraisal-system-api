using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Integration.Domain.FailedMessages;
using Integration.Infrastructure;
using Integration.Infrastructure.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Messaging.Services;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Per-node collector (design D10, no lease — every node runs this against its OWN broker). Every
/// round: discover fault queues, drain them into <see cref="FailedMessage"/> rows, republish rows an
/// admin marked <see cref="FailedMessageStatus.RetryRequested"/>, and refresh this node's
/// <see cref="BrokerSnapshot"/>. Each step has its own try/catch — nothing here can throw into the host
/// or affect MassTransit's own processing (design assumption 2).
/// </summary>
public class FailedMessageCollectorService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<FailedMessageCollectorService> logger,
    IDateTimeProvider dateTimeProvider,
    IOptions<FailedMessagesOptions> options,
    IConfiguration configuration,
    ReceiveEndpointDiscoveryObserver receiveEndpointDiscoveryObserver)
    : BackgroundService
{
    public const string ManagementHttpClientName = "FailedMessagesManagement";

    private static readonly JsonSerializerOptions QueuesJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // CreateChannelOptions has no parameterless overload in RabbitMQ.Client 7.1.2 — both confirmation
    // flags are required constructor args. Discover/Collect only read, so confirms stay off; Retry's
    // publish channel is built separately with confirms on (it needs to know the broker got the message).
    private static readonly CreateChannelOptions NoConfirmChannelOptions =
        new(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false);

    private readonly FailedMessagesOptions _options = options.Value;
    private readonly string _node = Environment.MachineName;
    // ToRabbitMqClientScheme: RabbitMQ.Client.ConnectionFactory.Uri (used below in GetConnectionAsync)
    // throws on a "rabbitmq"/"rabbitmqs" scheme even though MassTransit's own Host(Uri) — see
    // Bootstrapper/Api/Program.cs — accepts it interchangeably with "amqp"/"amqps" (verified against
    // MassTransit.RabbitMQ 8.4.1 and RabbitMQ.Client 7.1.2). Converting here, rather than only
    // documenting "use amqp://" for RABBITMQ_HOST, means an operator supplying the RabbitMQ-branded
    // scheme MassTransit already accepts doesn't take the collector down.
    // ParseRabbitHostUri replaces a bare `new Uri(...)!` — a missing or malformed
    // RabbitMQ:Host used to surface as a raw ArgumentNullException/UriFormatException at DI time with no
    // indication of which setting was wrong. This class is only ever constructed when FailedMessages:Enabled
    // is true (IntegrationModule only registers it as a hosted service then), so the check belongs HERE,
    // not in FailedMessagesOptions.Validate() — RabbitMQ:Host isn't one of that options class's own
    // properties (design: it deliberately reuses the app's existing RabbitMQ section), and a
    // ValidateOnStart on FailedMessagesOptions would run even when the collector itself is disabled.
    private readonly Uri _rabbitHost = ToRabbitMqClientScheme(ParseRabbitHostUri(configuration["RabbitMQ:Host"]));
    private readonly string _rabbitUsername = configuration["RabbitMQ:Username"]!;
    private readonly string _rabbitPassword = configuration["RabbitMQ:Password"]!;
    private readonly string _vhost = ParseVirtualHost(ToRabbitMqClientScheme(ParseRabbitHostUri(configuration["RabbitMQ:Host"])));

    private IConnection? _connection;

    // ---- Management API cooldown + missing-queue cache (plain fields on this singleton) -----

    /// <summary>After a 401/403/unreachable result, how long Discover/Snapshot skip the Management API
    /// entirely. (Passive-declare "queue missing" results use the shorter <see cref="MissingQueueRecheck"/>.)</summary>
    private static readonly TimeSpan ManagementCooldown = TimeSpan.FromMinutes(10);

    /// <summary>How long a passive-declare "queue missing" result is trusted. Much shorter than
    /// <see cref="ManagementCooldown"/>: MassTransit creates a fault queue only on the FIRST fault, so a
    /// 10-minute cache would hide a brand-new failure for up to 10 minutes — on the normal prod path
    /// (passive declare is the main discovery route), not a rare one.</summary>
    internal static readonly TimeSpan MissingQueueRecheck = TimeSpan.FromMinutes(1);

    private DateTime? _managementCooldownUntil;
    private string? _lastLoggedManagementStatus;
    private string _cachedManagementStatus = BrokerManagementStatus.Unreachable;
    private string? _lastManagementError;
    private readonly Dictionary<string, DateTime> _missingQueueCooldowns = new(StringComparer.Ordinal);

    /// <summary>Internal (rather than private) so a unit test can drive the cooldown/logging rules
    /// directly, without a broker or Management API.</summary>
    internal bool IsManagementCoolingDown(DateTime now) => _managementCooldownUntil is { } until && now < until;

    internal string CachedManagementStatus => _cachedManagementStatus;

    /// <summary>Logged only when the status actually changes (e.g. Ok → Unauthorized): a change TO Ok is
    /// Information (recovery, not a problem), a change to Unauthorized/Unreachable is Warning; otherwise
    /// Debug with no exception/stack trace, so a broker that stays down doesn't spam the log every round.</summary>
    internal void RecordManagementStatus(string status, DateTime now, Exception? exception = null)
    {
        _managementCooldownUntil = status == BrokerManagementStatus.Ok ? null : now + ManagementCooldown;
        _cachedManagementStatus = status;

        if (status != _lastLoggedManagementStatus)
        {
            var level = status == BrokerManagementStatus.Ok ? LogLevel.Information : LogLevel.Warning;
            logger.Log(level, exception, "[FAILED-MSG] Management API status changed to {Status} on node {Node}",
                status, _node);
            _lastLoggedManagementStatus = status;
        }
        else if (status != BrokerManagementStatus.Ok)
        {
            logger.LogDebug("[FAILED-MSG] Management API still {Status} on node {Node}", status, _node);
        }
    }

    // ---- AMQP connection status (log de-duplication) ---------------------------------------

    // null = not attempted yet; true/false = outcome of the latest GetConnectionAsync. The log rules
    // mirror RecordManagementStatus: a broker that stays down must not write a full-stack Error per step
    // every round into the in-app DB log store.
    private bool? _amqpConnectionOk;
    private Exception? _lastConnectFailure;

    /// <summary>Error with the stack once on the OK-or-unknown → failed transition, Information on
    /// recovery, Debug (no stack) while it stays down. Internal so a unit test can drive the transitions
    /// without a broker.</summary>
    internal void RecordAmqpConnectResult(Exception? failure)
    {
        if (failure is null)
        {
            if (_amqpConnectionOk == false)
            {
                logger.LogInformation("[FAILED-MSG] AMQP connection recovered on node {Node}", _node);
                // The broker is back, so a Management cooldown started by the outage is stale. Only on
                // this failed→OK edge: a steady healthy connection (e.g. a 401 for a missing management
                // tag) must keep its cooldown.
                _managementCooldownUntil = null;
            }
            _amqpConnectionOk = true;
            _lastConnectFailure = null;
            return;
        }

        _lastConnectFailure = failure;
        if (_amqpConnectionOk != false)
            logger.LogError(failure, "[FAILED-MSG] AMQP connection failed on node {Node}", _node);
        else
            logger.LogDebug("[FAILED-MSG] AMQP connection still down on node {Node}: {Reason}", _node, failure.Message);
        _amqpConnectionOk = false;
    }

    /// <summary>Step-level catch-all log. The exception <see cref="GetConnectionAsync"/> already recorded
    /// via <see cref="RecordAmqpConnectResult"/> is skipped (same reference) so each step doesn't log the
    /// same connection failure again every round.</summary>
    internal void LogStepFailure(string step, Exception ex)
    {
        if (ReferenceEquals(ex, _lastConnectFailure))
            return;
        logger.LogError(ex, "[FAILED-MSG] {Step} step failed on node {Node}", step, _node);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[FAILED-MSG] Collector started on node {Node}", _node);

        using var timer = new PeriodicTimer(_options.Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRoundAsync(stoppingToken);
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("[FAILED-MSG] Collector stopped on node {Node}", _node);
    }

    private Task RunRoundAsync(CancellationToken ct) => RunRoundAsync(ct, null);

    /// <summary>
    /// Internal (rather than private) so an integration test can drive one round directly against a
    /// real RabbitMQ testcontainer without waiting on <see cref="PeriodicTimer"/>. When
    /// <paramref name="knownFaultQueues"/> is supplied, Discover's own derivation from the fetch is
    /// skipped and this exact list is drained instead — the smallest way to feed the collector a queue
    /// the test just declared, without needing the broker's management port reachable from the test
    /// host. The fetch itself still runs every round — Snapshot always needs it.
    /// </summary>
    internal async Task RunRoundAsync(CancellationToken ct, IReadOnlyList<string>? knownFaultQueues)
    {
        // ONE Management API call per round — the same response both derives the fault-queue
        // list (Discover) and builds the broker snapshot (Snapshot), instead of each step fetching it
        // separately. Order: fetch → discover → collect → retry → write snapshot; each step keeps its
        // own try/catch so one failing never blocks the others.
        ManagementQueuesFetch? fetch = null;
        try
        {
            fetch = await FetchManagementQueuesAsync(ct);
        }
        // A cancelled ct here means the host is shutting down — rethrow so it
        // propagates as the normal, expected shutdown signal instead of being logged as an Error like a
        // genuine step failure. Placed before the catch-all in every step of this round.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FAILED-MSG] Management fetch step failed on node {Node}", _node);
        }

        var faultQueues = knownFaultQueues is not null ? new List<string>(knownFaultQueues) : new List<string>();
        if (knownFaultQueues is null)
            try
            {
                faultQueues = await DiscoverAsync(fetch, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogStepFailure("Discover", ex);
            }

        try
        {
            await CollectAsync(faultQueues, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogStepFailure("Collect", ex);
        }

        try
        {
            await RetryAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogStepFailure("Retry", ex);
        }

        try
        {
            await SnapshotAsync(fetch, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FAILED-MSG] Snapshot step failed on node {Node}", _node);
        }
    }

    // ---- Step 1: Fetch + Discover ---------------------------------------------------------

    /// <summary>Everything the single Management API response (or the cooldown/failure that stands in
    /// for it) produces this round — <see cref="FaultQueues"/> feeds Discover, <see cref="QueuesJson"/>
    /// feeds the broker snapshot. <see cref="Status"/> is null for a shape error: the
    /// connection and auth both worked, so the cached status must NOT change.</summary>
    /// <summary>Internal (rather than private) so a unit test can drive <see cref="FetchManagementQueuesAsync"/>
    /// directly with a fake <see cref="IHttpClientFactory"/>.</summary>
    internal sealed record ManagementQueuesFetch(string? Status, List<string>? FaultQueues, string? QueuesJson, string? LastError);

    /// <summary>
    /// The ONE Management API call per round (<c>lengths_age</c>/<c>lengths_incr</c> — a
    /// superset of what the old separate Discover call asked for, since it already returns every
    /// queue's <c>name</c> and <c>messages</c> too). Classifies failures: 401/403 →
    /// Unauthorized; a connection/timeout/other transport failure → Unreachable, with the cooldown;
    /// a malformed/unexpected payload shape → logged at Warning and treated as "use the fallback for
    /// THIS round only" — <see cref="RecordManagementStatus"/> is deliberately NOT called for that case,
    /// so it starts no cooldown and changes no cached status (the connection and auth both worked).
    /// </summary>
    internal async Task<ManagementQueuesFetch> FetchManagementQueuesAsync(CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;

        if (IsManagementCoolingDown(now))
            // Keep the last REAL error visible — the cooldown is why there's no fresh call, not the cause.
            return new ManagementQueuesFetch(_cachedManagementStatus, null, null,
                FaultMessageParser.Truncate(_lastManagementError is null
                    ? $"Management API cooldown active (cached status: {_cachedManagementStatus})"
                    : $"Management API cooldown active; last error: {_lastManagementError}", 2000));

        string body;
        try
        {
            var client = httpClientFactory.CreateClient(ManagementHttpClientName);
            // NO leading slash — a leading '/' is an absolute-path reference that
            // REPLACES BaseAddress's own path entirely (RFC 3986 §5.3), silently dropping a path prefix
            // like "http://host:15672/rabbit/". A relative reference (no leading '/') is appended after
            // BaseAddress's path instead, which is why the client is configured with a trailing '/'.
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"api/queues/{Uri.EscapeDataString(_vhost)}?lengths_age=1800&lengths_incr=60");
            request.Headers.Authorization = BuildBasicAuthHeader();

            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                RecordManagementStatus(BrokerManagementStatus.Unauthorized, now);
                _lastManagementError = $"Management API returned {(int)response.StatusCode}";
                return new ManagementQueuesFetch(BrokerManagementStatus.Unauthorized, null, null,
                    _lastManagementError);
            }

            if (!response.IsSuccessStatusCode)
            {
                // No distinct BrokerManagementStatus for "reachable but wrong path/vhost" (e.g. 404), so it
                // stays Unreachable — but LastError names the real cause instead of leaving operators guessing.
                var httpError = $"Management API returned {(int)response.StatusCode} {response.ReasonPhrase} " +
                                $"for vhost '{_vhost}'";
                RecordManagementStatus(BrokerManagementStatus.Unreachable, now);
                _lastManagementError = FaultMessageParser.Truncate(httpError, 2000);
                return new ManagementQueuesFetch(BrokerManagementStatus.Unreachable, null, null, _lastManagementError);
            }

            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // connect/timeout/any other transport failure — Unreachable, with the cooldown.
            RecordManagementStatus(BrokerManagementStatus.Unreachable, now, ex);
            _lastManagementError = FaultMessageParser.Truncate(ex.Message, 2000);
            return new ManagementQueuesFetch(BrokerManagementStatus.Unreachable, null, null, _lastManagementError);
        }

        try
        {
            var (faultQueues, queuesJson) = ParseManagementQueues(body, BuildOurFaultQueueNames());
            RecordManagementStatus(BrokerManagementStatus.Ok, now);
            _lastManagementError = null;
            return new ManagementQueuesFetch(BrokerManagementStatus.Ok, faultQueues, queuesJson, null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // The connection and auth both worked — a malformed/unexpected payload shape says
            // nothing about broker reachability. Log and fall back for THIS round only.
            logger.LogWarning(ex, "[FAILED-MSG] Malformed Management API payload on node {Node}", _node);
            return new ManagementQueuesFetch(null, null, null, FaultMessageParser.Truncate(ex.Message, 2000));
        }
    }

    /// <summary>Discover derives its fault-queue list from the SAME response Snapshot uses —
    /// falls back to passive declare when the fetch failed, is cooling down, or hit a shape error.</summary>
    private async Task<List<string>> DiscoverAsync(ManagementQueuesFetch? fetch, CancellationToken ct) =>
        fetch?.FaultQueues ?? await DiscoverViaPassiveDeclareAsync(ct);

    /// <summary>
    /// Discover-fallback candidate queues (design §3 step 1): every receive endpoint this node has
    /// actually opened, observed at runtime via <see cref="ReceiveEndpointDiscoveryObserver"/> — covers
    /// every consumer, not just the manually maintained <see cref="OrderedEndpoints"/> list — unioned
    /// with <see cref="OrderedEndpoints.Names"/> as a seed, in case the observer hasn't fired yet (e.g.
    /// right after a fresh start, before MassTransit reports every endpoint Ready). Pure/static so it's
    /// unit-testable without a broker.
    /// </summary>
    public static IReadOnlyList<string> BuildFallbackCandidates(IEnumerable<string> observedQueueNames)
    {
        var candidates = new HashSet<string>(observedQueueNames, StringComparer.Ordinal);
        candidates.UnionWith(OrderedEndpoints.Names);
        return candidates.ToList();
    }

    // A NOT_FOUND from a passive declare closes the CHANNEL at the AMQP protocol level
    // (not just an app-level error), so the channel must be reopened after every one — but opening a fresh
    // channel per QUEUE (the old behaviour) is wasteful now that this fallback is the NORMAL prod
    // path (until the bank adds the discovery tag), not a rare one. So ONE channel is reused and reopened
    // only after a NOT_FOUND. There is deliberately NO cap on reopens: the candidate list is finite and a
    // sparse broker legitimately 404s most of it (the real bus registers every consumer endpoint), so any
    // fixed cap just cuts the round short before it reaches the queues that DO exist. A dead connection
    // needs no cap either — CreateChannelAsync/the declare then throws and that stops the round.
    private async Task<List<string>> DiscoverViaPassiveDeclareAsync(CancellationToken ct) =>
        await DiscoverViaPassiveDeclareAsync(await GetConnectionAsync(ct), ct);

    // Internal so a unit test can drive the loop with a substitute IConnection.
    internal async Task<List<string>> DiscoverViaPassiveDeclareAsync(IConnection connection, CancellationToken ct)
    {
        var result = new List<string>();
        var now = dateTimeProvider.ApplicationNow;

        var channel = await connection.CreateChannelAsync(NoConfirmChannelOptions, ct);

        try
        {
            foreach (var endpoint in BuildFallbackCandidates(receiveEndpointDiscoveryObserver.QueueNames))
            foreach (var suffix in new[] { FaultMessageParser.ErrorSuffix, FaultMessageParser.SkippedSuffix })
            {
                var queue = endpoint + suffix;

                // A queue found missing recently is trusted for a while, so a broker with ~100
                // candidate queues doesn't declare every single one, every round. A queue found to EXIST
                // is always re-checked (removed from the cache below) — its message count changes.
                if (_missingQueueCooldowns.TryGetValue(queue, out var until) && now < until)
                    continue;

                try
                {
                    var ok = await channel.QueueDeclarePassiveAsync(queue, ct);
                    _missingQueueCooldowns.Remove(queue);
                    if (ok.MessageCount > 0)
                        result.Add(queue);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (IsChannelLevelError(ex))
                {
                    // 404: the queue genuinely doesn't exist. 403/405 (and any other soft channel error): the
                    // broker refused THIS queue (missing permission, exclusive queue held by another
                    // connection) — and in every case it closed only the CHANNEL, so the connection is fine and
                    // the rest of the candidates are still worth probing. Either way remember the queue for
                    // the recheck window so it isn't retried every round; a non-404 is also worth one warning
                    // per window (it is skipped silently otherwise, and a hidden fault queue is invisible).
                    _missingQueueCooldowns[queue] = now + MissingQueueRecheck;
                    if (!IsQueueNotFound(ex))
                        logger.LogWarning(ex,
                            "[FAILED-MSG] Passive declare of {Queue} refused ({ReplyCode}) on node {Node}; " +
                            "skipping it for {Window}",
                            queue, ex.ShutdownReason?.ReplyCode, _node, MissingQueueRecheck);

                    try
                    {
                        // The broker already closed this channel — Dispose is best-effort cleanup of the
                        // client-side object, not expected to talk to the broker again.
                        await channel.DisposeAsync();
                    }
                    catch
                    {
                        // Already closed by the broker; nothing more to do with the old handle.
                    }

                    channel = await connection.CreateChannelAsync(NoConfirmChannelOptions, ct);
                }
                catch (Exception ex)
                {
                    // A connection-level failure (broker down, forced close, etc.) says NOTHING about
                    // whether this specific queue exists — caching it as "missing" would hide a real queue
                    // for the whole recheck window. Stop this discovery round entirely instead, without
                    // caching anything; whatever was found so far this round is still returned.
                    logger.LogWarning(ex,
                        "[FAILED-MSG] Passive-declare discovery stopped early at {Queue} on node {Node}",
                        queue, _node);
                    return result;
                }
            }
        }
        finally
        {
            await channel.DisposeAsync();
        }

        return result;
    }

    /// <summary>
    /// True only for a REAL NOT_FOUND from the broker (AMQP reply code 404) —
    /// the queue genuinely doesn't exist, safe to cache as missing. Any other
    /// <see cref="RabbitMQ.Client.Exceptions.OperationInterruptedException"/> (a different channel-level
    /// protocol error) says nothing about whether THIS queue exists. Internal/static so a unit test can
    /// drive the classification directly, without a broker.
    /// </summary>
    internal static bool IsQueueNotFound(RabbitMQ.Client.Exceptions.OperationInterruptedException ex) =>
        ex.ShutdownReason?.ReplyCode == 404;

    /// <summary>
    /// True for an AMQP SOFT error — one that closes only the channel, never the connection (AMQP 0-9-1
    /// §1.2: 311 content-too-large, 312 no-route, 313 no-consumers, 403 access-refused, 404 not-found,
    /// 405 resource-locked, 406 precondition-failed). Every other reply code (320 connection-forced,
    /// 501-541 frame/syntax/internal errors, ...) is a HARD error that closes the connection, so discovery
    /// must stop there. Internal/static so a unit test can drive it without a broker.
    /// </summary>
    internal static bool IsChannelLevelError(RabbitMQ.Client.Exceptions.OperationInterruptedException ex) =>
        ex.ShutdownReason?.ReplyCode is 311 or 312 or 313 or 403 or 404 or 405 or 406;

    // ---- Step 2: Collect -------------------------------------------------------------------

    private async Task CollectAsync(IReadOnlyList<string> faultQueues, CancellationToken ct)
    {
        if (faultQueues.Count == 0)
            return;

        await CollectAsync(await GetConnectionAsync(ct), faultQueues, ct);
    }

    // Internal so a unit test can drive the per-queue loop with a substitute IConnection.
    internal async Task CollectAsync(IConnection connection, IReadOnlyList<string> faultQueues, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

        foreach (var queue in faultQueues)
        {
            // "queue" (with its _error/_skipped suffix) is the real broker queue to read from below;
            // "baseQueueName" (suffix stripped) is what gets stored as FailedMessage.SourceQueue — the
            // FE and the design (D2/§1/§3 in docs/failed-messages/design.md) key off the base name,
            // with Kind telling Error vs Skipped.
            var (baseQueueName, kind) = FaultMessageParser.FromFaultQueue(queue);
            if (kind is null)
                continue;

            IChannel channel;
            try
            {
                channel = await connection.CreateChannelAsync(NoConfirmChannelOptions, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One queue's channel failing to open (e.g. channel_max reached) says nothing about the
                // queues after it, so isolate it like a BasicGet failure — unless the connection itself is
                // gone, in which case every remaining queue would fail the same way.
                logger.LogWarning(ex, "[FAILED-MSG] Opening a channel for {Queue} failed on node {Node}",
                    queue, _node);
                if (!connection.IsOpen)
                    return;
                continue;
            }

            await using var _ = channel;

            for (var i = 0; i < _options.BatchPerQueue; i++)
            {
                RabbitMQ.Client.BasicGetResult? result;
                try
                {
                    result = await channel.BasicGetAsync(queue, autoAck: false, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[FAILED-MSG] BasicGet failed for {Queue} on node {Node}", queue, _node);
                    break;
                }

                if (result is null)
                    break; // queue drained for this round

                if (!await ProcessOneMessageAsync(dbContext, channel, baseQueueName, kind, queue, result, ct))
                    break; // ack failed on a dying channel, or the message could never be stored at all
            }
        }
    }

    /// <summary>
    /// One BasicGet result: store it (or resolve it as a duplicate), then ack/nack. Returns false when
    /// this queue's loop should stop (an ack failure — the message will simply be redelivered and dedup
    /// handles it — or a store failure severe enough that every message behind it would likely fail the
    /// same way). Internal so a unit test can drive it directly with a substitute channel.
    /// </summary>
    internal async Task<bool> ProcessOneMessageAsync(
        IntegrationDbContext dbContext, RabbitMQ.Client.IChannel channel, string sourceQueue, string kind,
        string queue, RabbitMQ.Client.BasicGetResult result, CancellationToken ct)
    {
        PersistOutcome outcome;
        try
        {
            outcome = await PersistMessageAsync(dbContext, sourceQueue, kind, queue, result, ct);
        }
        finally
        {
            // This DbContext is shared across the whole round (design), so always clear — a failed
            // insert must not get re-attempted by the next message's SaveChangesAsync.
            dbContext.ChangeTracker.Clear();
        }

        if (outcome == PersistOutcome.Ack)
        {
            // Once SaveChanges has committed, an ack failure must NOT fall into the Unparseable/store
            // path below — that would duplicate the row. The message is simply redelivered and dedup
            // (ResolveDuplicateAsync/AlreadyCollectedAsync) takes it from there next round.
            return await TryAckAsync(channel, result.DeliveryTag, queue, ct);
        }

        // BasicNackAsync(requeue: true) puts the message back at the HEAD of the queue, so the very
        // next BasicGet in this queue's loop would return the SAME message again — continuing spins
        // forever instead of moving on. Every nack must break out of this queue's loop; the next round
        // (or the next queue in this round) gets another attempt.
        await TryNackAsync(channel, result.DeliveryTag, queue, ct);
        return false;
    }

    /// <summary>Internal (rather than private) so a unit test can drive <see cref="SafeResolveDuplicateAsync"/>
    /// directly.</summary>
    internal enum PersistOutcome
    {
        /// <summary>Stored (or resolved as a duplicate of an already-Pending/RetryRequested row) — ack.</summary>
        Ack,

        /// <summary>
        /// The only other real outcome — every nack breaks out of this
        /// queue's loop regardless of "reason" (a requeued message lands back at the HEAD of the queue,
        /// so continuing would just re-fetch the same message and spin), so the old three-way split
        /// (Ack / NackAndContinue / NackAndStop) never actually behaved differently between the last two.
        /// The next round (or the next queue this round) gets another attempt.
        /// </summary>
        NackAndStop
    }

    /// <summary>
    /// Builds (or falls back to Unparseable), dedups, and saves ONE message. Never acks/nacks itself —
    /// see <see cref="ProcessOneMessageAsync"/> — so a later ack failure can never be mistaken for a
    /// store failure and re-trigger the Unparseable fallback.
    /// </summary>
    private async Task<PersistOutcome> PersistMessageAsync(
        IntegrationDbContext dbContext, string sourceQueue, string kind, string queue,
        RabbitMQ.Client.BasicGetResult result, CancellationToken ct)
    {
        FailedMessage entity;
        var isUnparseable = false;
        try
        {
            // BasicGet always returns the head of the queue, so a message that can never be parsed must
            // never be nacked back — that would block every message behind it, forever, every round.
            entity = BuildEntity(sourceQueue, kind, result);
        }
        catch (Exception parseEx)
        {
            logger.LogWarning(parseEx,
                "[FAILED-MSG] Failed to parse a message from {Queue} on node {Node}; storing as Unparseable",
                queue, _node);
            try
            {
                entity = BuildUnparseableEntity(sourceQueue, kind, result, parseEx);
            }
            catch (Exception buildEx)
            {
                // The fallback itself failed: nack this ONE message and stop only this queue. Letting it
                // escape would abort Collect for every queue after this one, on every round.
                logger.LogError(buildEx,
                    "[FAILED-MSG] Building the Unparseable fallback failed for {Queue} on node {Node}", queue, _node);
                return PersistOutcome.NackAndStop;
            }

            isUnparseable = true;
        }

        // An Unparseable row's MessageId is a synthetic content hash and its FaultedAt is real
        // collection time (not stable across redeliveries), so the FaultedAt-inclusive unique index
        // can't dedupe it. Check explicitly instead, so a redelivery of the exact same broken message
        // (ack lost after a prior insert) doesn't insert a second row.
        if (isUnparseable)
        {
            try
            {
                if (await AlreadyCollectedAsync(dbContext, entity, ct))
                    return await SafeResolveDuplicateAsync(dbContext, entity, queue, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A DB blip on this dedup check must only nack (requeue) and stop
                // THIS queue, same as every other transient DB failure in this method (see below) — not
                // propagate uncaught past ProcessOneMessageAsync into CollectAsync's queue loop, where
                // it would abort the Collect step for every remaining queue this round instead of just
                // this one.
                logger.LogError(ex, "[FAILED-MSG] AlreadyCollectedAsync failed for {Queue} on node {Node}",
                    queue, _node);
                return PersistOutcome.NackAndStop;
            }
        }

        try
        {
            dbContext.FailedMessages.Add(entity);
            await dbContext.SaveChangesAsync(ct);
            return PersistOutcome.Ack;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // UX_FailedMessages_Dedup violation. This key can belong to a row that already moved past
            // Pending (a retried message failing the same way again), so let ResolveDuplicateAsync decide
            // whether to skip or insert instead of just acking.
            return await SafeResolveDuplicateAsync(dbContext, entity, queue, ct);
        }
        catch (Exception ex) when (IsKnownDataError(ex))
        {
            // Only a KNOWN data error — a DbUpdateException wrapping a SqlException we recognise as
            // "this exact row can never be inserted as parsed" (NOT NULL 515, check/FK 547, truncation
            // 2628/8152; the dedup numbers 2601/2627 have their own catch above) — is worth degrading to
            // an Unparseable row instead of losing the message or blocking every message behind it.
            logger.LogError(ex,
                "[FAILED-MSG] Failed to store a message from {Queue} on node {Node}; storing as Unparseable",
                queue, _node);
            dbContext.ChangeTracker.Clear();
            try
            {
                var fallbackEntity = BuildUnparseableEntity(sourceQueue, kind, result, ex);
                if (await AlreadyCollectedAsync(dbContext, fallbackEntity, ct))
                    return await SafeResolveDuplicateAsync(dbContext, fallbackEntity, queue, ct);

                dbContext.FailedMessages.Add(fallbackEntity);
                await dbContext.SaveChangesAsync(ct);
                return PersistOutcome.Ack;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception fallbackEx)
            {
                logger.LogError(fallbackEx,
                    "[FAILED-MSG] Fallback Unparseable insert also failed for {Queue} on node {Node}",
                    queue, _node);
                return PersistOutcome.NackAndStop;
            }
        }
        catch (Exception ex)
        {
            // Everything else — a pool-timeout InvalidOperationException, network errors, an
            // unrecognised SQL error number, the database being down — is treated as TRANSIENT. Every
            // message behind this one would likely fail identically this round, so requeue (nack,
            // handled by the caller) and stop this queue rather than degrading to Unparseable.
            logger.LogError(ex, "[FAILED-MSG] Failed to store a message from {Queue} on node {Node}",
                queue, _node);
            return PersistOutcome.NackAndStop;
        }
    }

    /// <summary>
    /// An exception inside ResolveDuplicateAsync itself must only affect THIS message — nack it
    /// and move on — never abort the whole Collect step.
    /// </summary>
    internal async Task<PersistOutcome> SafeResolveDuplicateAsync(
        IntegrationDbContext dbContext, FailedMessage entity, string queue, CancellationToken ct)
    {
        try
        {
            return await ResolveDuplicateAsync(dbContext, entity, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[FAILED-MSG] ResolveDuplicateAsync failed for {Queue} on node {Node}; skipping this message",
                queue, _node);
            return PersistOutcome.NackAndStop;
        }
    }

    private async Task<bool> TryAckAsync(RabbitMQ.Client.IChannel channel, ulong deliveryTag, string queue, CancellationToken ct)
    {
        try
        {
            await channel.BasicAckAsync(deliveryTag, false, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[FAILED-MSG] Ack failed for {Queue} on node {Node} after storing; message will be redelivered",
                queue, _node);
            return false;
        }
    }

    private async Task TryNackAsync(RabbitMQ.Client.IChannel channel, ulong deliveryTag, string queue, CancellationToken ct)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, false, true, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A nack that itself fails on a dead channel must not escape and abort Collect.
            logger.LogError(ex, "[FAILED-MSG] Nack failed for {Queue} on node {Node}; channel likely dead",
                queue, _node);
        }
    }

    /// <summary>
    /// Distinguishes "this exact row can never be inserted" (worth storing degraded as Unparseable
    /// and moving on) from EVERYTHING else, which is treated as transient (a pool-timeout
    /// <see cref="InvalidOperationException"/>, network errors, an unrecognised SQL error number, the
    /// database being unreachable) and requeued instead. Only a <see cref="DbUpdateException"/> wrapping
    /// a <see cref="SqlException"/> with one of the well-known "this data violates a constraint" numbers —
    /// NOT NULL (515), check/FK (547), string-or-binary truncation (2628/8152) — counts as known; the
    /// dedup numbers (2601/2627) have their own catch above and never reach this check. Walks the WHOLE
    /// InnerException chain (not just one level) — EF can wrap a SqlException inside more than one layer
    /// (e.g. a retry-strategy or transaction wrapper exception in between).
    /// </summary>
    private static bool IsKnownDataError(Exception ex)
    {
        if (ex is not DbUpdateException)
            return false;

        for (var current = ex; current is not null; current = current.InnerException)
            if (current is SqlException sqlEx)
                return sqlEx.Number is 515 or 547 or 2628 or 8152;

        return false;
    }

    private FailedMessage BuildEntity(string sourceQueue, string kind, RabbitMQ.Client.BasicGetResult result)
    {
        var headers = FaultMessageParser.CoerceHeaders(result.BasicProperties.Headers);
        var amqpMessageId = Guid.TryParse(result.BasicProperties.MessageId, out var mid) ? mid : (Guid?)null;
        var parsed = FaultMessageParser.Parse(headers, result.Body, amqpMessageId);
        var (refType, refId, refNumber) = FailedMessageReferenceResolver.Resolve(parsed.MessagePayloadJson);

        var collectedAt = dateTimeProvider.ApplicationNow;
        var faultedAt = ResolveFaultedAt(parsed.FaultedAtUtc, parsed.EnvelopeSentTimeUtc, result.BasicProperties, collectedAt);

        return FailedMessage.Create(
            TruncateForColumn(_node, FailedMessageConfiguration.NodeMaxLength),
            // base queue name (no _error/_skipped suffix) — Kind tells them apart
            TruncateForColumn(sourceQueue, FailedMessageConfiguration.SourceQueueMaxLength),
            kind,
            parsed.MessageId,
            parsed.ConversationId,
            parsed.MessageType,
            parsed.ConsumerType,
            parsed.ExceptionType,
            parsed.ExceptionMessage,
            parsed.StackTrace,
            parsed.RetryCount,
            faultedAt,
            collectedAt,
            refType,
            refId,
            // RefNumber comes from the untrusted message payload — must fit the column or the
            // INSERT below fails and blocks every message behind this one in the queue.
            TruncateForColumn(refNumber, FailedMessageConfiguration.RefNumberMaxLength),
            result.Body.ToArray(),
            TruncateForColumn(result.BasicProperties.ContentType, FailedMessageConfiguration.ContentTypeMaxLength),
            headers);
    }

    /// <summary>Truncates a broker-controlled string to its column length before INSERT — see
    /// <see cref="FailedMessageConfiguration"/>. MessageType/ConsumerType/ExceptionType/ExceptionMessage
    /// are already truncated inside <see cref="FaultMessageParser.Parse"/>.</summary>
    [return: NotNullIfNotNull(nameof(value))]
    private static string? TruncateForColumn(string? value, int maxLength) =>
        FaultMessageParser.Truncate(value, maxLength);

    /// <summary>
    /// FaultedAt feeds the dedup unique index, so it must be the same value every time the same
    /// broker message is collected (a redelivery after a lost ack must land on the SAME row, not a new
    /// one). Preference order: the real MT-Fault-Timestamp (Error only, already resolved into
    /// <paramref name="faultedAtUtc"/>), the AMQP <c>Timestamp</c> property, the MassTransit envelope's
    /// own <c>sentTime</c>, and only then collection time — which is NOT stable across redeliveries and
    /// is a last resort for a Skipped message that carries none of the above.
    /// </summary>
    private DateTime ResolveFaultedAt(
        DateTime? faultedAtUtc, DateTime? envelopeSentTimeUtc, RabbitMQ.Client.IReadOnlyBasicProperties properties,
        DateTime collectedAt)
    {
        if (faultedAtUtc.HasValue)
            return dateTimeProvider.ToApplicationTime(faultedAtUtc.Value);

        if (properties.IsTimestampPresent())
            return dateTimeProvider.ToApplicationTime(
                DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime).UtcDateTime);

        if (envelopeSentTimeUtc.HasValue)
            return dateTimeProvider.ToApplicationTime(envelopeSentTimeUtc.Value);

        return collectedAt;
    }

    /// <summary>
    /// An Unparseable row has no real MassTransit MessageId — that's what made it Unparseable — so
    /// the dedup unique index (filtered on MessageId IS NOT NULL) would never catch a redelivery of the
    /// exact same broken message, inserting a fresh duplicate every time its ack is lost. This is a
    /// SYNTHETIC id, not a real MassTransit MessageId: SHA-256 of (sourceQueue, kind, raw body bytes, and
    /// a discriminator), first 16 bytes as a Guid, so the same broker message always hashes to the same id.
    /// The discriminator keeps two DIFFERENT broken messages with an identical (e.g. empty) body apart:
    /// the AMQP message-id when the broker message has one, otherwise the stored (coerced) header table,
    /// key-sorted so enumeration order cannot change the hash. A redelivery keeps both, so it still dedups.
    /// With neither (no message-id, no headers) the hash is the body-only one, as before.
    /// </summary>
    public static Guid DeterministicMessageId(
        string sourceQueue, string kind, byte[] body,
        IReadOnlyDictionary<string, string>? headers = null, string? amqpMessageId = null)
    {
        var discriminator = string.Empty;
        if (!string.IsNullOrWhiteSpace(amqpMessageId))
            discriminator = "mid\u0001" + amqpMessageId;
        else if (headers is { Count: > 0 })
            // Length-prefixed so no key/value content can fake a boundary.
            discriminator = "hdr\u0001" + string.Concat(headers
                .OrderBy(h => h.Key, StringComparer.Ordinal)
                .Select(h => $"{h.Key.Length}:{h.Key}{h.Value.Length}:{h.Value}"));

        var prefix = Encoding.UTF8.GetBytes(
            sourceQueue + '\u0001' + kind + '\u0001' + (discriminator.Length > 0 ? discriminator + '\u0001' : ""));
        var hash = SHA256.HashData([.. prefix, .. body]);
        return new Guid(hash[..16]);
    }

    /// <summary>
    /// A synthetic MessageId's FaultedAt is real collection time (see
    /// <see cref="BuildUnparseableEntity"/>) — NOT a sentinel like <see cref="DateTime.UnixEpoch"/>,
    /// which sorted every Unparseable row to the very bottom of the faultedAt-DESC list (effectively
    /// hidden) and made <c>oldestPendingAt</c> falsely report 1970. Because FaultedAt is no longer part
    /// of a stable dedup tuple for these rows, dedup is an explicit existence check on
    /// (MessageId, SourceQueue, Kind) instead of relying on the FaultedAt-inclusive unique index.
    /// </summary>
    internal static Task<bool> AlreadyCollectedAsync(IntegrationDbContext dbContext, FailedMessage entity, CancellationToken ct) =>
        entity.MessageId is { } messageId
            ? dbContext.FailedMessages.AnyAsync(
                m => m.MessageId == messageId && m.SourceQueue == entity.SourceQueue && m.Kind == entity.Kind, ct)
            : Task.FromResult(false);

    internal const string UnparseableExceptionType = "Unparseable";

    /// <summary>
    /// How long a collision with a RetryRequested row is nacked (see <see cref="ResolveDuplicateAsync"/>)
    /// since the row's last activity (request or retry claim) before the row is presumed abandoned by a dead owner
    /// node and the collision is rebase-inserted instead.
    /// Twice <see cref="FailedMessageRetryClaimPolicy.StaleAfter"/>: a live owner resolves it well within that.
    /// </summary>
    internal static readonly TimeSpan RetryRequestedNackWindow = TimeSpan.FromMinutes(10);

    /// <summary>The most recent sign of life on a RetryRequested row: the admin's request, or a collector's
    /// claim on it — whichever is later. A fresh claim means a collector is still working on the row even if
    /// the request itself is old (e.g. the owner node was down when the retry was requested).</summary>
    private static DateTime LastActivityAt(DateTime requestedAt, DateTime? retryClaimedAt) =>
        retryClaimedAt is { } claimedAt && claimedAt > requestedAt ? claimedAt : requestedAt;

    /// <summary>
    /// Called on every dedup hit: the UX_FailedMessages_Dedup unique-index violation on INSERT (parseable
    /// messages, keyed on MessageId+SourceQueue+Kind+FaultedAt) or <see cref="AlreadyCollectedAsync"/>'s
    /// proactive check (Unparseable messages, keyed on MessageId+SourceQueue+Kind only). An Error fault
    /// whose MT-Fault-Timestamp PARSED (so it became FaultedAt) is always a redelivery and is skipped (see
    /// the comment in the body). Otherwise (Skipped, Unparseable, or an Error whose timestamp is missing or
    /// unparseable) a message that is retried and then fails again keeps the same
    /// envelope, so it can land on the exact key of the ORIGINAL row, which has since moved past Pending;
    /// acking unconditionally would silently drop that second failure. Look the existing row up (ignoring
    /// FaultedAt, so this also matches an Unparseable row): if it is still Pending, this is a redelivery of
    /// the same unresolved failure — do nothing (<see cref="PersistOutcome.Ack"/>). If it is RetryRequested
    /// for less than <see cref="RetryRequestedNackWindow"/> (measured from its last activity: request or claim) it may be the retried message failing again, so the
    /// caller must NOT ack (<see cref="PersistOutcome.NackAndStop"/>) — it is requeued until the row leaves
    /// RetryRequested; a RetryRequested row older than the window (dead owner node) is handled like
    /// Retried/Discarded. If it is Retried/Discarded,
    /// the admin already resolved that failure and this is a NEW one reusing the same key — insert
    /// <paramref name="entity"/> (a fresh, not-yet-tracked instance) with FaultedAt bumped to collection
    /// time so the key no longer collides.
    /// </summary>
    internal async Task<PersistOutcome> ResolveDuplicateAsync(
        IntegrationDbContext dbContext, FailedMessage entity, CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear();

        // An Error row's key includes FaultedAt = MT-Fault-Timestamp, which is unique per fault, so a
        // collision can only be a redelivery of the SAME broker message (e.g. its ack was lost) — even if
        // the stored row has since been Retried/Discarded. Inserting another Pending row would let it be
        // retried, and republished, a second time. A genuine re-failure carries a new timestamp and so
        // never reaches here — but only if that timestamp PARSED: an unparseable one leaves FaultedAt at the
        // envelope sentTime (preserved by a retry), so it is NOT a unique-per-fault key and must fall through. Skipped has no such timestamp (its FaultedAt comes from the envelope/AMQP
        // time, which a retry preserves) and Unparseable's key excludes FaultedAt altogether, so for both
        // a collision with a resolved row can be a real new failure: they fall through to the check below.
        if (entity.Kind == FailedMessageKind.Error
            && entity.ExceptionType != UnparseableExceptionType
            && FaultMessageParser.TryParseFaultTimestamp(entity.Headers, out _))
            return PersistOutcome.Ack;

        var existing = await dbContext.FailedMessages
            .Where(m => m.MessageId == entity.MessageId && m.SourceQueue == entity.SourceQueue && m.Kind == entity.Kind)
            .OrderByDescending(m => m.FaultedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null || existing.Status == FailedMessageStatus.Pending)
            return PersistOutcome.Ack;

        // Still RetryRequested: this may be the retried message failing AGAIN (MarkRetried's save failed, or
        // an overlapped-recycle collector republished it) rather than a redelivery of the original. Acking
        // would silently drop that failure, and the rebase insert below is only right once the row has left
        // RetryRequested. Nack/requeue instead: the retry step resolves the row (MarkRetried, or its claim
        // goes stale within FailedMessageRetryClaimPolicy.StaleAfter and the next round re-claims it), and a
        // later round then reaches the Retried path below. BOUNDED by RetryRequestedNackWindow: a row whose
        // owner node is dead stays RetryRequested forever, and nacking it every round would block this
        // queue's head and starve NEW failures behind it. Once the row is older than the window it is
        // presumed abandoned and falls through to the rebase insert, so the re-failure is recorded and the
        // queue keeps moving.
        if (existing.Status == FailedMessageStatus.RetryRequested
            && existing.ActionAt is { } requestedAt
            && dateTimeProvider.ApplicationNow - LastActivityAt(requestedAt, existing.RetryClaimedAt)
                < RetryRequestedNackWindow)
            return PersistOutcome.NackAndStop;

        // Rebase to the entity's own CollectedAt (not a fresh ApplicationNow) so FaultedAt and CollectedAt
        // of a rebased row are the same instant.
        entity.RebaseFaultedAt(entity.CollectedAt);
        dbContext.FailedMessages.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return PersistOutcome.Ack;
    }

    private static string NonBlank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>
    /// Last-resort fallback when <see cref="BuildEntity"/> itself throws on genuinely malformed broker
    /// data — still captures the raw body/headers so nothing is lost, but skips every field that could
    /// itself be the cause of the failure (no re-parsing, no reference resolution).
    /// </summary>
    internal FailedMessage BuildUnparseableEntity(
        string sourceQueue, string kind, RabbitMQ.Client.BasicGetResult result, Exception parseException)
    {
        Dictionary<string, string> headers;
        try
        {
            headers = FaultMessageParser.CoerceHeaders(result.BasicProperties.Headers);
        }
        catch (Exception)
        {
            headers = new Dictionary<string, string>();
        }

        var collectedAt = dateTimeProvider.ApplicationNow;
        var body = result.Body.ToArray();
        var messageId = DeterministicMessageId(sourceQueue, kind, body, headers, result.BasicProperties.MessageId);
        // Real time (AMQP Timestamp if present, else collection time) — never a fixed sentinel like
        // DateTime.UnixEpoch, which sorted these rows to the bottom of every faultedAt-DESC list
        // (effectively hidden) and made summary oldestPendingAt falsely read 1970. AlreadyCollectedAsync
        // (not FaultedAt) is what keeps a redelivery of the same broken message from duplicating.
        var faultedAt = result.BasicProperties.IsTimestampPresent()
            ? dateTimeProvider.ToApplicationTime(
                DateTimeOffset.FromUnixTimeSeconds(result.BasicProperties.Timestamp.UnixTime).UtcDateTime)
            : collectedAt;

        // Every string FailedMessage.Create validates with ThrowIfNullOrWhiteSpace gets a non-blank fallback
        // here, so this last-resort builder cannot throw on a whitespace-only value.
        var message = NonBlank(parseException.Message, UnparseableExceptionType);
        message = TruncateForColumn(message, FailedMessageConfiguration.ExceptionMessageMaxLength);

        return FailedMessage.Create(
            TruncateForColumn(NonBlank(_node, "Unknown"), FailedMessageConfiguration.NodeMaxLength),
            TruncateForColumn(NonBlank(sourceQueue, "Unknown"), FailedMessageConfiguration.SourceQueueMaxLength),
            NonBlank(kind, "Unknown"),
            messageId,
            null,
            "Unknown",
            null,
            UnparseableExceptionType,
            message,
            parseException.StackTrace,
            0,
            faultedAt,
            collectedAt,
            null,
            null,
            null,
            body,
            TruncateForColumn(result.BasicProperties.ContentType, FailedMessageConfiguration.ContentTypeMaxLength),
            headers);
    }

    // ---- Step 3: Retry --------------------------------------------------------------------

    /// <summary>The MarkRetried save (after a CONFIRMED publish) is retried up
    /// to this many times before giving up and leaving the claim in place.</summary>
    private const int MarkRetriedSaveMaxAttempts = 3;

    private static readonly TimeSpan MarkRetriedSaveRetryDelay = TimeSpan.FromMilliseconds(200);

    private async Task RetryAsync(CancellationToken ct)
    {
        List<Guid> candidateIds;
        using (var listScope = scopeFactory.CreateScope())
        {
            var listDbContext = listScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            candidateIds = await listDbContext.FailedMessages
                .Where(m => m.Node == _node && m.Status == FailedMessageStatus.RetryRequested)
                .Select(m => m.Id)
                .ToListAsync(ct);
        }

        if (candidateIds.Count == 0)
            return;

        var connection = await GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await RetryCandidatesAsync(channel, candidateIds, ct);
    }

    /// <summary>
    /// The loop body of <see cref="RetryAsync"/>, extracted so a unit test can drive
    /// it directly with a substitute <see cref="RabbitMQ.Client.IChannel"/> — no real broker needed to
    /// prove the mid-loop channel-death handling below.
    /// </summary>
    internal async Task RetryCandidatesAsync(RabbitMQ.Client.IChannel channel, List<Guid> candidateIds, CancellationToken ct)
    {
        for (var i = 0; i < candidateIds.Count; i++)
        {
            var id = candidateIds[i];

            // A FRESH scope/DbContext per candidate — a failed candidate's tracked
            // entities (e.g. a RetryFailed audit row whose own save failed, still tracked and Added) must
            // never leak into the next candidate's SaveChangesAsync call.
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

            // Claim before publishing — a second collector sharing this node name (IIS overlapped
            // recycle, or a second process) must never publish the same row twice.
            var message = await ClaimForRetryAsync(dbContext, id, ct);
            if (message is null)
                continue;

            if (!channel.IsOpen)
            {
                // The channel died mid-loop (e.g. the broker
                // connection dropped). Publishing anyway would misclassify the result as UnknownOutcome
                // (an exception unrelated to PublishException) and keep the claim for the full 5-minute
                // stale window even though nothing was ever attempted. KNOWN not-sent instead: release
                // ONLY the claim THIS iteration just took for the current candidate, then stop; the next
                // round reconnects with a fresh channel. Every REMAINING candidate is left untouched —
                // this instance never claimed them, and clearing an id it doesn't own could wipe a claim
                // a DIFFERENT collector instance legitimately took on it between this round's initial
                // query and now, causing that other instance to publish and this one to publish again too.
                logger.LogWarning(
                    "[FAILED-MSG] Retry channel no longer open on node {Node}; releasing claim for {Id} " +
                    "without publishing; {Remaining} remaining candidate(s) left untouched", _node, id,
                    candidateIds.Count - i - 1);

                await ClearRetryClaimSafelyAsync(dbContext, id, CancellationToken.None);
                break;
            }

            var outcome = await PublishRetryAsync(channel, message, ct);
            switch (outcome)
            {
                case RetryPublishOutcome.Confirmed:
                    // ---- Publish CONFIRMED — the broker has this message. Central rule: from
                    // here on, NEVER clear the claim on failure. ----
                    await SaveMarkRetriedWithRetryAsync(dbContext, message, ct);
                    break;

                case RetryPublishOutcome.UnroutableQueueGone:
                    // Unroutable — the original queue no longer exists on the broker. KNOWN undelivered.
                    // Put the row back to Pending with a system-generated reason rather than leave
                    // it stuck RetryRequested on a node that may never revisit it (see design.md
                    // "node decommissioning").
                    await RevertRetryWithAuditAsync(dbContext, message, $"Retry failed on {_node}: queue not found");
                    break;

                case RetryPublishOutcome.KnownUndeliveredNacked:
                    // KNOWN undelivered, but only clearing the claim would republish this row every round
                    // forever when the broker keeps refusing it (e.g. x-overflow=reject-publish on a full
                    // queue), with no audit and nothing for an operator to see. Revert to Pending + RetryFailed
                    // audit, exactly like an unroutable retry, so a person decides when to try again.
                    await RevertRetryWithAuditAsync(
                        dbContext, message, $"Retry failed on {_node}: broker rejected the publish (nack)");
                    break;

                case RetryPublishOutcome.KnownUndeliveredChannelClosed:
                    // The broker refused this publish by closing the channel (soft error). Same revert as a nack so
                    // an operator sees it — but the channel is now dead, so STOP, exactly as for a channel that was
                    // found closed above: the remaining candidates are left unclaimed for the next round, which
                    // reconnects with a fresh channel.
                    await RevertRetryWithAuditAsync(
                        dbContext, message, $"Retry failed on {_node}: broker closed the channel (publish refused)");
                    return;

                case RetryPublishOutcome.FailedBeforePublish:
                    // Nothing ever reached the broker (a failure building headers/properties from this row's own
                    // data) — deterministic per row, so clearing the claim would retry it forever. Same revert.
                    await RevertRetryWithAuditAsync(
                        dbContext, message, $"Retry failed on {_node}: could not build the retry publish");
                    break;

                case RetryPublishOutcome.UnknownOutcomeTimedOut:
                    // The broker did not confirm within RetryPublishTimeout (alarm/overload). Keep this row's
                    // claim (outcome unknown — see UnknownOutcome below) AND stop: every further candidate would
                    // burn another full timeout (200 x 30s = 100 minutes with Collect/Snapshot blocked behind
                    // this loop). The remaining candidates are deliberately left unclaimed for the next round.
                    logger.LogWarning(
                        "[FAILED-MSG] Retry publish timed out on node {Node}; stopping this retry round with " +
                        "{Remaining} candidate(s) left unclaimed", _node, candidateIds.Count - i - 1);
                    return;

                case RetryPublishOutcome.UnknownOutcome:
                    // The broker may or may not have the message — KEEP the claim. The 5-minute
                    // stale-claim window then allows exactly one later republish attempt, which InboxGuard
                    // dedups by the MessageId this publish preserved.
                    break;
            }
        }
    }

    /// <summary>
    /// Puts a claimed row back to Pending with a server-generated reason and a <c>RetryFailed</c> audit entry
    /// (ActorCode null — no signed-in user, design D6; the node is in the reason). A failure saving this must not
    /// abort the whole retry loop — it is logged and the loop moves on; the scope (and its tracked audit row,
    /// saved or not) is discarded at the end of the iteration either way.
    /// </summary>
    private async Task RevertRetryWithAuditAsync(IntegrationDbContext dbContext, FailedMessage message, string reason)
    {
        var now = dateTimeProvider.ApplicationNow;
        message.RevertRetry(reason, now);
        dbContext.FailedMessageAuditLogs.Add(FailedMessageAuditLog.Create(
            FailedMessageAuditAction.RetryFailed, FailedMessageAuditSource.Consumer,
            message.Id, null, null, null, reason, now));

        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception saveEx)
        {
            logger.LogError(saveEx, "[FAILED-MSG] Failed to save retry revert for {Id} on node {Node}",
                message.Id, _node);
        }
    }

    /// <summary>Upper bound on one retry publish-with-confirm. A broker that never confirms (e.g. a memory
    /// or disk alarm) would otherwise stall the whole collector round forever. Internal and settable only
    /// so a test can shrink it; production keeps the default.</summary>
    internal TimeSpan RetryPublishTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>What happened trying to republish one claimed row, and whether the
    /// outcome is known well enough to safely clear the claim.</summary>
    internal enum RetryPublishOutcome
    {
        /// <summary>The broker confirmed it has the message.</summary>
        Confirmed,

        /// <summary>KNOWN undelivered — the original queue no longer exists (<c>PublishException.IsReturn == true</c>).</summary>
        UnroutableQueueGone,

        /// <summary>KNOWN undelivered — an explicit broker NACK (<c>PublishException.IsReturn == false</c>).
        /// The row is reverted to Pending with a RetryFailed audit entry.</summary>
        KnownUndeliveredNacked,

        /// <summary>KNOWN undelivered — the broker answered the publish by closing the CHANNEL with a soft error
        /// (<see cref="IsChannelLevelError"/>: e.g. 406 precondition-failed, 403 access-refused), which surfaces as an
        /// <c>OperationInterruptedException</c>, not a <c>PublishException</c>. A deterministic refusal of this
        /// row, so it is reverted to Pending with a RetryFailed audit entry like a nack — and the channel is dead,
        /// so the retry loop stops.</summary>
        KnownUndeliveredChannelClosed,

        /// <summary>Nothing reached the broker — building headers/properties failed before the publish call.
        /// The row is reverted to Pending with a RetryFailed audit entry.</summary>
        FailedBeforePublish,

        /// <summary>The broker never confirmed within <see cref="RetryPublishTimeout"/> — an UNKNOWN outcome like
        /// <see cref="UnknownOutcome"/> (claim kept), but a sign the broker itself is unhealthy, so the retry
        /// loop stops instead of burning a full timeout per remaining candidate.</summary>
        UnknownOutcomeTimedOut,

        /// <summary>Any other exception from the publish call itself — the broker may or may not have the
        /// message (e.g. the connection dropped while awaiting the confirm).</summary>
        UnknownOutcome
    }

    /// <summary>
    /// Publishes one claimed row and classifies the result. Internal (rather than private) so a unit
    /// test can drive it directly with a substitute <see cref="RabbitMQ.Client.IChannel"/> — no real
    /// broker needed to prove the KNOWN-vs-UNKNOWN classification the claim-clearing decision depends on.
    /// </summary>
    internal async Task<RetryPublishOutcome> PublishRetryAsync(
        RabbitMQ.Client.IChannel channel, FailedMessage message, CancellationToken ct)
    {
        string routingKey;
        BasicProperties properties;
        try
        {
            // SourceQueue is already the base queue name (no _error/_skipped suffix) — it IS the
            // routing key that gets a default-exchange publish back to the original queue.
            routingKey = message.SourceQueue;
            properties = new BasicProperties
            {
                ContentType = message.ContentType,
                Headers = BuildRetryHeaders(message.Headers),
                // A non-persistent republish is lost on a broker restart with no trace it ever
                // existed. Persistent = true sets DeliveryMode = 2 (RabbitMQ.Client 7.1.2).
                Persistent = true,
                Type = message.MessageType
            };
            // An Unparseable row's MessageId is a synthetic content hash, not an id the original ever
            // carried — stamping it would make consumers/InboxGuard dedup on an id that never existed on
            // the wire (and give two distinct messages with identical bodies one inbox key). The collector
            // does not store the original AMQP message-id (only the header table), so it is left unset.
            if (message.MessageId is { } messageId && message.ExceptionType != UnparseableExceptionType)
                properties.MessageId = messageId.ToString();

            // Best-effort: CorrelationId isn't stored on FailedMessage, so it's re-read from the
            // raw body already in hand (cheap — same envelope shape ParseEnvelope already reads).
            var correlationId = FaultMessageParser.TryParseCorrelationId(message.Body);
            if (correlationId is not null)
                properties.CorrelationId = correlationId.Value.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[FAILED-MSG] Failed to build retry publish for {Id} on node {Node} before publishing; " +
                "reverting to Pending", message.Id, _node);
            return RetryPublishOutcome.FailedBeforePublish;
        }

        using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        publishCts.CancelAfter(RetryPublishTimeout);

        try
        {
            // Confirms are enabled on this channel (CreateChannelOptions above), so reaching the
            // next line means the broker has acknowledged the publish (verified: RabbitMQ.Client
            // 7.1.2's IChannel exposes no separate WaitForConfirms — BasicPublishAsync itself
            // awaits the confirm, and throws if the broker rejects the publish). mandatory: true
            // is required too — otherwise an unroutable publish (original queue since deleted) is
            // silently dropped by the broker while still being confirmed, and MarkRetried below
            // would destroy the only copy. Verified live: RabbitMQ.Client 7.1.2 throws
            // RabbitMQ.Client.Exceptions.PublishException (IsReturn=true) from BasicPublishAsync
            // itself when a mandatory-confirmed publish is unroutable — handled by the caller by
            // reverting to Pending instead of leaving the row stuck RetryRequested forever.
            // Bounded: a broker that never confirms must not stall the whole collector round.
            await channel.BasicPublishAsync(string.Empty, routingKey, mandatory: true, properties, message.Body,
                publishCts.Token);
            return RetryPublishOutcome.Confirmed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown, not a broker problem — the claim is kept either way (stale window), so just
            // say so quietly and let the cancellation propagate.
            logger.LogInformation(
                "[FAILED-MSG] Retry publish for {Id} on node {Node} interrupted by shutdown; claim kept",
                message.Id, _node);
            throw;
        }
        catch (OperationCanceledException ex) when (publishCts.IsCancellationRequested)
        {
            // Not the host (handled above) — our own RetryPublishTimeout fired: the broker never confirmed.
            logger.LogWarning(ex,
                "[FAILED-MSG] Retry publish for {Id} on node {Node} was not confirmed within {Timeout}; keeping " +
                "claim (expires after {Window}, InboxGuard dedups by MessageId)",
                message.Id, _node, RetryPublishTimeout, FailedMessageRetryClaimPolicy.StaleAfter);
            return RetryPublishOutcome.UnknownOutcomeTimedOut;
        }
        catch (RabbitMQ.Client.Exceptions.PublishException ex) when (ex.IsReturn)
        {
            logger.LogWarning(ex,
                "[FAILED-MSG] Retry publish for {Id} on node {Node} was unroutable; reverting to Pending",
                message.Id, _node);
            return RetryPublishOutcome.UnroutableQueueGone;
        }
        catch (RabbitMQ.Client.Exceptions.PublishException ex)
        {
            // An explicit broker NACK (IsReturn == false) — RabbitMQ.Client 7.1.2 confirms this
            // publish was definitely NOT accepted. KNOWN undelivered; the caller reverts the row to
            // Pending with a RetryFailed audit entry so a broker that keeps refusing it can't loop forever.
            logger.LogWarning(ex,
                "[FAILED-MSG] Retry publish for {Id} on node {Node} was nacked by the broker; " +
                "reverting to Pending", message.Id, _node);
            return RetryPublishOutcome.KnownUndeliveredNacked;
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (IsChannelLevelError(ex))
        {
            // The broker answered the publish by closing the CHANNEL with a soft error (406 precondition-failed,
            // 403 access-refused, ...). That is a deterministic refusal of this publish — KNOWN undelivered —
            // but it surfaces as OperationInterruptedException, not PublishException, so without this clause it
            // fell through to UnknownOutcome: claim kept, republished every 5 minutes forever, never audited.
            // A connection-level close (320, 5xx) stays UnknownOutcome below: it says nothing about this message.
            logger.LogWarning(ex,
                "[FAILED-MSG] Retry publish for {Id} on node {Node} made the broker close the channel " +
                "({ReplyCode}); reverting to Pending", message.Id, _node, ex.ShutdownReason?.ReplyCode);
            return RetryPublishOutcome.KnownUndeliveredChannelClosed;
        }
        catch (Exception ex)
        {
            // Everything else from BasicPublishAsync (a dropped connection while awaiting the
            // confirm, our own RetryPublishTimeout firing, etc.) has an UNKNOWN outcome — the broker may or
            // may not have the message. The caller must KEEP the claim.
            logger.LogWarning(ex,
                "[FAILED-MSG] Retry publish outcome UNKNOWN for {Id} on node {Node}; keeping claim " +
                "(expires after {Window}, InboxGuard dedups by MessageId)",
                message.Id, _node, FailedMessageRetryClaimPolicy.StaleAfter);
            return RetryPublishOutcome.UnknownOutcome;
        }
    }

    /// <summary>Clears the claim so the next round retries promptly, guarding the update itself
    /// (a DB-down here must not abort the loop either — the claim simply expires on its own after
    /// <see cref="FailedMessageRetryClaimPolicy.StaleAfter"/>).</summary>
    private async Task ClearRetryClaimSafelyAsync(IntegrationDbContext dbContext, Guid id, CancellationToken ct)
    {
        try
        {
            await dbContext.FailedMessages.Where(m => m.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.RetryClaimedAt, (DateTime?)null), ct);
        }
        catch (Exception clearEx)
        {
            logger.LogError(clearEx,
                "[FAILED-MSG] Failed to clear retry claim for {Id} on node {Node}; it will expire after {Window}",
                id, _node, FailedMessageRetryClaimPolicy.StaleAfter);
        }
    }

    /// <summary>
    /// Saves <see cref="FailedMessage.MarkRetried"/> after a CONFIRMED publish —
    /// the broker already has the message, so losing the claim here would let another round (or another
    /// collector instance, once the claim expires) republish it. Retries the save
    /// <see cref="MarkRetriedSaveMaxAttempts"/> times with a short delay; if it still fails, logs at Error
    /// and leaves <see cref="FailedMessage.RetryClaimedAt"/> in place on purpose — it expires on its own
    /// after <see cref="FailedMessageRetryClaimPolicy.StaleAfter"/>, and InboxGuard dedups any eventual
    /// re-delivery by the MessageId this publish preserved. Internal so a unit test can drive it directly
    /// with a DbContext whose SaveChangesAsync always throws, without a real broker (the publish itself
    /// isn't this method's concern — by the time it's called, the broker already has the message).
    /// </summary>
    internal async Task SaveMarkRetriedWithRetryAsync(IntegrationDbContext dbContext, FailedMessage message, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MarkRetriedSaveMaxAttempts; attempt++)
        {
            try
            {
                message.MarkRetried(dateTimeProvider.ApplicationNow);
                // CancellationToken.None — the broker already has this message; shutdown must not
                // abandon this save.
                await dbContext.SaveChangesAsync(CancellationToken.None);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex, "[FAILED-MSG] Concurrent update retrying {Id} on node {Node}",
                    message.Id, _node);
                // Detach so this loop's later SaveChangesAsync calls (for other rows sharing this
                // DbContext) don't keep retrying this one's now-stale write. Claim is NOT cleared.
                dbContext.Entry(message).State = EntityState.Detached;
                return;
            }
            catch (Exception ex) when (attempt < MarkRetriedSaveMaxAttempts)
            {
                logger.LogWarning(ex,
                    "[FAILED-MSG] MarkRetried save attempt {Attempt}/{MaxAttempts} failed for {Id} on node {Node}; retrying",
                    attempt, MarkRetriedSaveMaxAttempts, message.Id, _node);
                await Task.Delay(MarkRetriedSaveRetryDelay, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[FAILED-MSG] MarkRetried save failed for {Id} on node {Node} after {MaxAttempts} attempts; " +
                    "leaving the claim in place (expires after {Window}, InboxGuard dedups by MessageId)",
                    message.Id, _node, MarkRetriedSaveMaxAttempts, FailedMessageRetryClaimPolicy.StaleAfter);
                dbContext.Entry(message).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Claims exactly one row before publishing — a single conditional UPDATE (only when
    /// <see cref="FailedMessage.RetryClaimedAt"/> is null or older than
    /// <see cref="FailedMessageRetryClaimPolicy.StaleAfter"/>) — so two collector instances sharing the
    /// same <see cref="Node"/> (an IIS overlapped recycle, or a second process) can never both publish the
    /// same row. Returns null when the claim affected zero rows (already claimed by someone else, already
    /// moved on, or discarded). The row is re-fetched AFTER the claim and attached with its post-claim
    /// <c>RowVersion</c> as the concurrency token's "original" value, so the caller's later
    /// MarkRetried/RevertRetry save compares against what the claim UPDATE just wrote, not the pre-claim
    /// value — which would otherwise always look like a conflicting concurrent write.
    /// </summary>
    internal async Task<FailedMessage?> ClaimForRetryAsync(IntegrationDbContext dbContext, Guid id, CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;
        var staleBefore = now - FailedMessageRetryClaimPolicy.StaleAfter;

        var claimed = await dbContext.FailedMessages
            .Where(m => m.Id == id && m.Node == _node && m.Status == FailedMessageStatus.RetryRequested &&
                        (m.RetryClaimedAt == null || m.RetryClaimedAt < staleBefore))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.RetryClaimedAt, now), ct);

        if (claimed != 1)
            return null;

        var message = await dbContext.FailedMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is null)
            return null;

        dbContext.Attach(message);
        return message;
    }

    /// <summary>Headers RabbitMQ itself writes on dead-lettering/delivery. Stored flattened to strings, they
    /// would be republished string-typed (x-death must be an array of tables) and can break the broker's
    /// handling on a later dead-letter; the broker re-creates them, so a republish must not carry them.</summary>
    private static bool IsBrokerOwnedHeader(string key) =>
        key == "x-death" || key == "x-delivery-count"
        || key.StartsWith("x-first-death-", StringComparison.Ordinal)
        || key.StartsWith("x-last-death-", StringComparison.Ordinal);

    /// <summary>Original headers minus every MT-Fault-*/MT-Reason header (design D10 step 3) and the
    /// broker-owned dead-letter headers (<see cref="IsBrokerOwnedHeader"/>).</summary>
    private static Dictionary<string, object?> BuildRetryHeaders(IReadOnlyDictionary<string, string> storedHeaders)
    {
        var headers = new Dictionary<string, object?>();
        foreach (var (key, value) in storedHeaders)
        {
            if (key.StartsWith("MT-Fault-", StringComparison.Ordinal) || key == "MT-Reason" || IsBrokerOwnedHeader(key))
                continue;
            headers[key] = value;
        }

        return headers;
    }

    /// <summary>
    /// The exact fault-queue names OUR endpoints could ever produce — every receive
    /// endpoint observed at runtime plus the <see cref="OrderedEndpoints"/> seed, each with the
    /// <c>_error</c>/<c>_skipped</c> suffix appended (same candidate set <see cref="DiscoverViaPassiveDeclareAsync"/>
    /// passive-declares). The Management API is a shared vhost — SECURITY/data: other applications' fault
    /// queues must never be drained. Their contents are never even a BasicGet candidate.
    /// </summary>
    private HashSet<string> BuildOurFaultQueueNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in BuildFallbackCandidates(receiveEndpointDiscoveryObserver.QueueNames))
        {
            names.Add(endpoint + FaultMessageParser.ErrorSuffix);
            names.Add(endpoint + FaultMessageParser.SkippedSuffix);
        }

        return names;
    }

    /// <summary>The fault-queue-derivation half of the single Management API response — pure so
    /// it can share a body string with <see cref="ParseManagementQueues"/>'s snapshot half without a
    /// second HTTP call. Faulted: only OUR queues (<paramref name="ourFaultQueueNames"/>) with a
    /// fault suffix AND at least one message (matches the old dedicated Discover call's own filter).
    /// The snapshot half is scoped to our own endpoints the same way, so other applications' queue names,
    /// depths and rates in the shared vhost never reach the screen.
    /// Internal (rather than private) so a unit test can drive it directly with a hand-built payload.</summary>
    internal static (List<string> FaultQueues, string QueuesJson) ParseManagementQueues(
        string managementApiJson, IReadOnlySet<string> ourFaultQueueNames)
    {
        using var doc = JsonDocument.Parse(managementApiJson);
        var faultQueues = new List<string>();
        var queues = new List<QueueSnapshotDto>();
        // The snapshot is shown to every FAILED_MESSAGE_VIEW user, so it only carries our own endpoints.
        var ourEndpoints = ourFaultQueueNames.Select(FaultMessageParser.StripFaultSuffix)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var q in doc.RootElement.EnumerateArray())
        {
            // A queue entry with no usable name can't be keyed by anything — skip just this entry,
            // not the whole payload.
            if (!q.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
                continue;
            var name = nameEl.GetString();
            if (name is null)
                continue;

            var messages = ReadNullableInt64(q, "messages") ?? 0;

            if (FaultMessageParser.DeriveKind(name) is not null)
            {
                // Only ever a candidate if it's one of OUR endpoints' own fault queues — never
                // another application's, even though the Management API returns every queue in the vhost.
                if (messages > 0 && ourFaultQueueNames.Contains(name))
                    faultQueues.Add(name);
                continue; // fault twins never appear as their own snapshot row (folded into FailedMessages counts)
            }

            // Another application's queue in the shared vhost: its name, depth and rates are not ours to show.
            if (!ourEndpoints.Contains(name))
                continue;

            // Every field below is independently nullable — a JSON null or a missing field becomes
            // an absent value (or an empty Samples list), never a reason to reject the whole payload.
            var ready = ReadNullableInt64(q, "messages_ready");
            var unacked = ReadNullableInt64(q, "messages_unacknowledged");
            var consumers = ReadNullableInt32(q, "consumers");

            double? publishRate = null;
            double? deliverRate = null;
            if (q.TryGetProperty("message_stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                if (stats.TryGetProperty("publish_details", out var pub) && pub.ValueKind == JsonValueKind.Object)
                    publishRate = ReadNullableDouble(pub, "rate");
                if (stats.TryGetProperty("deliver_get_details", out var del) && del.ValueKind == JsonValueKind.Object)
                    deliverRate = ReadNullableDouble(del, "rate");
            }

            var samples = new List<int>();
            if (q.TryGetProperty("messages_ready_details", out var readyDetails) &&
                readyDetails.ValueKind == JsonValueKind.Object &&
                readyDetails.TryGetProperty("samples", out var samplesEl) &&
                samplesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in samplesEl.EnumerateArray())
                {
                    // A null (or otherwise non-object) entry in the samples array is skipped, not
                    // a payload-wide failure.
                    if (s.ValueKind != JsonValueKind.Object)
                        continue;
                    var sample = ReadNullableInt32(s, "sample");
                    if (sample.HasValue)
                        samples.Add(sample.Value);
                }
                samples.Reverse(); // RabbitMQ returns newest-first; store oldest→newest
                if (samples.Count > 30)
                    samples = samples.GetRange(samples.Count - 30, 30);
            }

            queues.Add(new QueueSnapshotDto(name, ready, unacked, consumers, publishRate, deliverRate, samples));
        }

        return (faultQueues, JsonSerializer.Serialize(queues, QueuesJsonOptions));
    }

    /// <summary>null/missing/non-number all read as absent, never a thrown exception.</summary>
    private static long? ReadNullableInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var l) ? l : null;
    }

    private static int? ReadNullableInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;
    }

    private static double? ReadNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;
    }

    private record QueueSnapshotDto(
        string Name, long? Ready, long? Unacked, int? Consumers, double? PublishRate, double? DeliverRate,
        List<int> Samples);

    // ---- Step 4: Snapshot -------------------------------------------------------------------

    /// <summary>Writes the round's <see cref="BrokerSnapshot"/> row from the SAME fetch Discover already
    /// consumed — no second HTTP call. A null <see cref="ManagementQueuesFetch.Status"/> (a response whose
    /// shape could not be read) keeps the cached status; any round without fetched queue data (failure,
    /// cooldown, unreadable shape) writes an EMPTY queue list rather than the previous round's stale one.
    /// Internal (rather than private) so a unit test can drive it directly with an InMemory
    /// DbContext, without assembling a full round.</summary>
    internal async Task SnapshotAsync(ManagementQueuesFetch? fetch, CancellationToken ct)
    {
        var managementStatus = fetch?.Status ?? _cachedManagementStatus;
        // "[]" (never null) for a round without queue data: the column is NOT NULL.
        var queuesJson = fetch?.QueuesJson ?? "[]";
        var lastError = fetch?.LastError;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var collectedAt = dateTimeProvider.ApplicationNow;

        var existing = await dbContext.BrokerSnapshots.FindAsync([_node], ct);
        if (existing is null)
        {
            dbContext.BrokerSnapshots.Add(
                BrokerSnapshot.Create(_node, collectedAt, managementStatus, queuesJson, lastError));
        }
        else
        {
            // queuesJson is "[]" whenever this round's Management fetch did not succeed — NOT the
            // previous round's depths: CollectedAt is stamped now, so carrying the old
            // numbers forward would show stale depths/consumer counts as fresh. "[]" is the same
            // "no broker queue data" shape the summary/FE already handle (ready/unacked/... omitted, "—").
            existing.Refresh(collectedAt, managementStatus, queuesJson, lastError);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another process with this MachineName (IIS overlapped recycle) inserted this node's row
            // between our find and our insert. Its snapshot is as good as ours and the next round updates
            // it, so this is an expected race, not an Error.
            dbContext.ChangeTracker.Clear();
            logger.LogDebug("[FAILED-MSG] Snapshot row for node {Node} was inserted concurrently; skipping", _node);
        }
    }

    // ---- Shared: connection + auth -----------------------------------------------------------

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            RecordAmqpConnectResult(null);
            return _connection;
        }

        _connection?.Dispose();

        // Uri drives host/port/vhost/scheme (amqp vs amqps) — same source MassTransit itself uses
        // (Bootstrapper/Api/Program.cs: configurator.Host(new Uri(RabbitMQ:Host), ...)). Credentials
        // are separate config keys, so they're set after, same as MassTransit's host => callback.
        var factory = CreateConnectionFactory();

        try
        {
            _connection = await factory.CreateConnectionAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordAmqpConnectResult(ex);
            throw;
        }

        RecordAmqpConnectResult(null);
        return _connection;
    }

    /// <summary>The AMQP connection settings. Internal so a unit test can read them without a broker.</summary>
    internal ConnectionFactory CreateConnectionFactory() => new()
    {
        Uri = _rabbitHost,
        // Explicit and AFTER Uri: ConnectionFactory.Uri keeps the raw path segment WITH a trailing slash
        // ("amqp://mq/cas/" -> "cas/"), while every Management call uses the trimmed _vhost ("cas") — so
        // without this the two paths could target different vhosts.
        VirtualHost = _vhost,
        UserName = _rabbitUsername,
        Password = _rabbitPassword,
        ClientProvidedName = $"failed-messages-collector-{_node}"
    };

    private AuthenticationHeaderValue BuildBasicAuthHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_rabbitUsername}:{_rabbitPassword}")));

    /// <summary>
    /// Validates RabbitMQ:Host with a clear, actionable message instead of letting a
    /// missing/malformed value surface as a raw ArgumentNullException or UriFormatException from
    /// <c>new Uri(...)</c> at DI construction time. Accepts the same four schemes
    /// <see cref="ToRabbitMqClientScheme"/> handles (amqp/amqps pass through unchanged; rabbitmq/rabbitmqs
    /// are converted downstream) — anything else is rejected here before either field initializer that
    /// calls this ever reaches <see cref="ToRabbitMqClientScheme"/>.
    /// </summary>
    private static Uri ParseRabbitHostUri(string? configuredHost)
    {
        if (!string.IsNullOrWhiteSpace(configuredHost) &&
            Uri.TryCreate(configuredHost, UriKind.Absolute, out var uri) &&
            uri.Scheme is "amqp" or "amqps" or "rabbitmq" or "rabbitmqs")
            return uri;

        throw new InvalidOperationException(
            "RabbitMQ:Host must be an amqp(s):// or rabbitmq(s):// URI" +
            (string.IsNullOrWhiteSpace(configuredHost) ? "." : $", but was '{configuredHost}'."));
    }

    /// <summary>Default vhost "/" for a bare "amqp://host:port/" URI, else the (unescaped) path segment.
    /// Surrounding literal slashes are trimmed ("rabbitmq://mq/cas/" is vhost "cas", not "cas/" — the
    /// latter has no such vhost on the broker, so every Management call would 404). Trimming happens
    /// BEFORE unescaping so an explicit "%2F" (the vhost named "/") survives. Internal for unit tests.</summary>
    internal static string ParseVirtualHost(Uri hostUri)
    {
        var vhost = Uri.UnescapeDataString(hostUri.AbsolutePath.Trim('/'));
        return vhost.Length == 0 ? "/" : vhost;
    }

    /// <summary>
    /// "rabbitmq"/"rabbitmqs" → "amqp"/"amqps"; anything else is returned unchanged. See the
    /// <c>_rabbitHost</c> field comment above for why: RabbitMQ.Client 7.1.2's <c>ConnectionFactory.Uri</c>
    /// throws <c>ArgumentException</c> on "rabbitmq"/"rabbitmqs" (verified), while MassTransit.RabbitMQ
    /// 8.4.1's <c>Host(Uri)</c> parses either pair of schemes identically (also verified) — so this
    /// method is purely about satisfying RabbitMQ.Client here, not about what MassTransit needs.
    /// </summary>
    public static Uri ToRabbitMqClientScheme(Uri hostUri) => hostUri.Scheme switch
    {
        "rabbitmq" => new UriBuilder(hostUri) { Scheme = "amqp", Port = hostUri.Port }.Uri,
        "rabbitmqs" => new UriBuilder(hostUri) { Scheme = "amqps", Port = hostUri.Port }.Uri,
        _ => hostUri
    };

    public override void Dispose()
    {
        _connection?.Dispose();
        base.Dispose();
    }
}
