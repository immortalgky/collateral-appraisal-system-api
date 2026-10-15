using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Application.Features.FailedMessages;
using Integration.Application.Features.FailedMessages.DiscardFailedMessages;
using Integration.Application.Features.FailedMessages.GetFailedMessage;
using Integration.Application.Features.FailedMessages.GetFailedMessages;
using Integration.Application.Features.FailedMessages.GetFailedMessagesSummary;
using Integration.Application.Features.FailedMessages.RetryFailedMessages;
using Integration.Application.Features.OutboxMessages;
using Integration.Domain.FailedMessages;
using Integration.FailedMessages;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Infrastructure;
using Integration.WebApplicationFactories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Outbox;
using Shared.Pagination;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Exercises the failed-messages admin endpoints (consumer <c>_error</c>/<c>_skipped</c> side) against
/// real SQL, seeding <see cref="FailedMessage"/>/<see cref="BrokerSnapshot"/> rows directly via
/// <see cref="IntegrationDbContext"/>.
/// </summary>
public class FailedMessagesApiTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<Guid> SeedAsync(
        IntegrationDbContext db, string node, string sourceQueue, string kind, string exceptionType,
        DateTime faultedAt, Guid? messageId = null, string status = FailedMessageStatus.Pending,
        string messageType = "Test.FakeEvent", string exceptionMessage = "boom")
    {
        var message = FailedMessage.Create(
            node, sourceQueue, kind, messageId ?? Guid.CreateVersion7(), null, messageType, "Test.FakeConsumer",
            exceptionType, exceptionMessage, null, 0, faultedAt, faultedAt,
            null, null, null, "{\"email\":\"jane@example.com\"}"u8.ToArray(), "application/json", null);

        if (status != FailedMessageStatus.Pending)
        {
            // Drive through the domain methods so Status/ActionBy/ActionAt end up consistent.
            if (status == FailedMessageStatus.RetryRequested)
                message.RequestRetry("seed-actor", null, faultedAt);
            else if (status == FailedMessageStatus.Retried)
            {
                message.RequestRetry("seed-actor", null, faultedAt);
                message.MarkRetried(faultedAt);
            }
            else if (status == FailedMessageStatus.Discarded)
                message.Discard("seed-actor", "seeded as discarded", faultedAt);
        }

        db.FailedMessages.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private IServiceScope CreateScope() => Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>Seeds one raw IntegrationEventOutbox row in the given module's
    /// schema, the same pattern OutboxMessagesApiTests.SeedAsync uses — needed here only to prove the
    /// summary's outbox UNION filter (Status IN ('Failed','Processing')) excludes Pending/Processed rows
    /// without an EF DbContext for every module's own schema.</summary>
    private static async Task SeedOutboxRowAsync(
        IntegrationDbContext db, string module, string status, DateTime occurredAt,
        DateTime? processingStartedAt = null)
    {
        object?[] parameters =
        [
            Guid.CreateVersion7(), "Test.SummaryOutboxProbe", "{}", "{}", null, occurredAt, null, null, status,
            processingStartedAt
        ];
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO [" + module + "].[IntegrationEventOutbox] " +
            "(Id, EventType, Payload, Headers, CorrelationId, OccurredAt, ProcessedAt, Error, RetryCount, Status, ProcessingStartedAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, 0, {8}, {9})",
            (object[])parameters);
    }

    private async Task<FailedMessagesSummaryDto> GetSummaryAsync()
    {
        var response = await _client.GetAsync("/admin/failed-messages/summary", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"summary call failed: {(int)response.StatusCode} {raw}");
        var result = JsonSerializer.Deserialize<FailedMessagesSummaryDto>(raw, JsonHelper.Options);
        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task GetFailedMessagesSummary_TopGroupsAndSyntheticQueueForMissingSnapshot()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        // TopGroups is a global (not node-scoped) aggregate, so this needs a queue name no other test's
        // leftover Pending rows can collide with — unlike the node-scoped assertions below.
        var groupQueue = $"appraisal-sync-{Guid.NewGuid():N}"[..24];

        await SeedAsync(db, node, groupQueue, FailedMessageKind.Error, "System.TimeoutException", now);
        await SeedAsync(db, node, groupQueue, FailedMessageKind.Error, "System.TimeoutException", now);
        await SeedAsync(db, node, "webhook-dispatch", FailedMessageKind.Error, "System.TimeoutException", now);

        db.BrokerSnapshots.Add(BrokerSnapshot.Create(
            node, now, BrokerManagementStatus.Ok,
            """[{"name":"webhook-dispatch","ready":4,"unacked":0,"consumers":2,"publishRate":1.2,"deliverRate":1.1,"samples":[1,2,3]}]""",
            null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Baseline read BEFORE seeding outbox rows — the DB is shared with other
        // tests, so asserting an absolute OutboxFailedCount would be flaky; a delta proves the filter.
        var before = await GetSummaryAsync();

        await SeedOutboxRowAsync(db, "request", "Failed", now);
        await SeedOutboxRowAsync(db, "request", "Pending", now);
        await SeedOutboxRowAsync(db, "request", "Processed", now);

        var summary = await GetSummaryAsync();

        Assert.Contains(summary.TopGroups, g => g.Queue == groupQueue && g.ExceptionType == "System.TimeoutException" && g.Count == 2);

        var nodeSnapshot = summary.Nodes.Single(n => n.Node == node);
        var webhookQueue = nodeSnapshot.Queues.Single(q => q.Name == "webhook-dispatch");
        Assert.Equal(4, webhookQueue.Ready);
        Assert.Equal(1, webhookQueue.ErrorCount);

        var syntheticQueue = nodeSnapshot.Queues.Single(q => q.Name == groupQueue);
        Assert.Null(syntheticQueue.Ready);
        Assert.Empty(syntheticQueue.Samples);
        Assert.Equal(2, syntheticQueue.ErrorCount);

        // Only the Failed row moves the count — the Pending and Processed rows seeded alongside it must
        // not, proving OutboxUnionSql's per-branch Status filter excludes them rather than just failing
        // to be counted by the CASE (which would pass this assertion even with no filter at all).
        Assert.Equal(1, summary.OutboxFailedCount - before.OutboxFailedCount);
    }

    /// <summary>
    /// The summary's last-24h counts and outbox Failed/Stuck counts must equal what the original
    /// formulations compute over the SAME database: one combined SUM(CASE...) over the Failed/Processing
    /// UNION, and a Total with no status list. Seeds every status inside and outside the 24 h window, and
    /// outbox rows for every Stuck/not-Stuck case (stale ProcessingStartedAt, fresh, NULL + old OccurredAt,
    /// NULL + fresh) plus Failed/Pending/Processed rows across several module schemas.
    /// </summary>
    [Fact]
    public async Task GetFailedMessagesSummary_Counts_EqualTheLegacyFormulations_OnSeededData()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];

        foreach (var status in FailedMessageStatus.All)
        {
            await SeedAsync(db, node, "webhook-dispatch", FailedMessageKind.Error, "System.TimeoutException",
                now.AddHours(-1), status: status);
            await SeedAsync(db, node, "webhook-dispatch", FailedMessageKind.Error, "System.TimeoutException",
                now.AddHours(-48), status: status);
        }

        var stuckSince = now.AddMinutes(-10);
        await SeedOutboxRowAsync(db, "request", "Failed", now);
        await SeedOutboxRowAsync(db, "appraisal", "Failed", now.AddDays(-3));
        await SeedOutboxRowAsync(db, "workflow", "Failed", now);
        await SeedOutboxRowAsync(db, "request", "Processing", now, processingStartedAt: stuckSince);
        await SeedOutboxRowAsync(db, "appraisal", "Processing", now, processingStartedAt: now.AddSeconds(-10));
        await SeedOutboxRowAsync(db, "workflow", "Processing", stuckSince, processingStartedAt: null);
        await SeedOutboxRowAsync(db, "document", "Processing", now, processingStartedAt: null);
        await SeedOutboxRowAsync(db, "collateral", "Pending", now);
        await SeedOutboxRowAsync(db, "reporting", "Processed", now);

        var summary = await GetSummaryAsync();

        var since = summary.ServerTime.AddHours(-24);
        var threshold = summary.ServerTime - OutboxDeliveryPolicy.StuckThreshold;
        var legacyUnion = OutboxUnionSql.Build(
            "Status, ProcessingStartedAt, OccurredAt", "Status IN ('Failed', 'Processing')");

        async Task<int> Scalar(string sql) => await db.Database
            .SqlQueryRaw<int>(sql,
                new SqlParameter("Since", System.Data.SqlDbType.DateTime2) { Value = since },
                new SqlParameter("StuckThreshold", System.Data.SqlDbType.DateTime2) { Value = threshold })
            .SingleAsync(ct);

        var legacyTotal = await Scalar(
            "SELECT COUNT(*) AS Value FROM integration.FailedMessages WHERE FaultedAt >= @Since");
        var legacyRetried = await Scalar(
            "SELECT COUNT(*) AS Value FROM integration.FailedMessages WHERE Status = 'Retried' AND ActionAt >= @Since");
        var legacyDiscarded = await Scalar(
            "SELECT COUNT(*) AS Value FROM integration.FailedMessages WHERE Status = 'Discarded' AND ActionAt >= @Since");
        var legacyFailed = await Scalar(
            $"SELECT ISNULL(SUM(CASE WHEN o.Status = 'Failed' THEN 1 ELSE 0 END), 0) AS Value FROM ({legacyUnion}) o");
        var legacyStuck = await Scalar(
            $"SELECT ISNULL(SUM(CASE WHEN {OutboxUnionSql.StuckPredicate} THEN 1 ELSE 0 END), 0) AS Value FROM ({legacyUnion}) o");

        Assert.Equal(legacyTotal, summary.Last24h.Total);
        Assert.Equal(legacyRetried, summary.Last24h.Retried);
        Assert.Equal(legacyDiscarded, summary.Last24h.Discarded);
        Assert.Equal(legacyFailed, summary.OutboxFailedCount);
        Assert.Equal(legacyStuck, summary.OutboxStuckCount);

        // Not vacuous: the seeded rows really do land in each count (>= : the DB is shared).
        Assert.True(summary.Last24h.Total >= 4);
        Assert.True(summary.Last24h.Retried >= 1);
        Assert.True(summary.Last24h.Discarded >= 1);
        Assert.True(summary.OutboxFailedCount >= 3);
        Assert.True(summary.OutboxStuckCount >= 2);
    }

    /// <summary>A node with Pending FailedMessages rows but NO BrokerSnapshot row at
    /// all (its collector has never completed a Snapshot round) still appears in nodes[] — with
    /// managementStatus/collectedAt omitted, and queue health built purely from the counts.</summary>
    [Fact]
    public async Task GetFailedMessagesSummary_NodeWithPendingRowsButNoSnapshot_AppearsAsGhostNode()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var ghostNode = $"ghost-node-{Guid.NewGuid():N}"[..20];
        var ghostQueue = $"ghost-queue-{Guid.NewGuid():N}"[..24];

        await SeedAsync(db, ghostNode, ghostQueue, FailedMessageKind.Error, "System.TimeoutException", now);

        var response = await _client.GetAsync("/admin/failed-messages/summary", TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"summary call failed: {(int)response.StatusCode} {raw}");
        var summary = JsonSerializer.Deserialize<FailedMessagesSummaryDto>(raw, JsonHelper.Options);

        Assert.NotNull(summary);
        var ghost = summary.Nodes.Single(n => n.Node == ghostNode);
        Assert.Null(ghost.ManagementStatus);
        Assert.Null(ghost.CollectedAt);
        Assert.Null(ghost.LastError);

        var queue = ghost.Queues.Single(q => q.Name == ghostQueue);
        Assert.Null(queue.Ready);
        Assert.Null(queue.Unacked);
        Assert.Null(queue.Consumers);
        Assert.Empty(queue.Samples);
        Assert.Equal(1, queue.ErrorCount);
    }

    [Fact]
    public async Task GetFailedMessages_DefaultsToPending_ComputesOrderedNonTransientAndSiblingCount()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var sharedMessageId = Guid.CreateVersion7();

        var pendingOrderedNonTransient = await SeedAsync(
            db, node, "webhook-dispatch", FailedMessageKind.Error, "Shared.Exceptions.ConflictException", now,
            messageId: sharedMessageId);
        var sibling = await SeedAsync(
            db, node, "appraisal-status-dashboard", FailedMessageKind.Error, "System.TimeoutException", now,
            messageId: sharedMessageId);
        var retried = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now,
            status: FailedMessageStatus.Retried);

        var response = await _client.GetAsync(
            $"/admin/failed-messages?node={node}&pageSize=50", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FailedMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        var ids = result.Items.Select(i => i.Id).ToList();
        Assert.Contains(pendingOrderedNonTransient, ids);
        Assert.Contains(sibling, ids);
        Assert.DoesNotContain(retried, ids); // default status filter is Pending

        var item = result.Items.Single(i => i.Id == pendingOrderedNonTransient);
        Assert.True(item.IsOrderedQueue);
        Assert.True(item.IsNonTransient);
        Assert.Equal(1, item.SiblingCount);
    }

    /// <summary>
    /// Rows sharing one FaultedAt (a batch faulting in the same second) must still page deterministically:
    /// the ORDER BY carries an Id tie-breaker, so no row repeats or vanishes across OFFSET/FETCH pages.
    /// </summary>
    [Fact]
    public async Task GetFailedMessages_EqualFaultedAt_PagesInStableOrderWithoutRepeatsOrGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];

        for (var i = 0; i < 9; i++)
            await SeedAsync(db, node, "webhook-dispatch", FailedMessageKind.Error, "System.TimeoutException", now);

        var expected = await db.FailedMessages.AsNoTracking().Where(f => f.Node == node)
            .OrderByDescending(f => f.FaultedAt).ThenByDescending(f => f.Id).Select(f => f.Id).ToListAsync(ct);

        var paged = new List<Guid>();
        for (var page = 1; page <= 5; page++)
        {
            var response = await _client.GetAsync($"/admin/failed-messages?node={node}&status=All&pageSize=2&pageNumber={page}", ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FailedMessageListDto>>(JsonHelper.Options, ct);
            paged.AddRange(result!.Items.Select(i => i.Id));
        }

        Assert.Equal(expected, paged);
    }

    /// <summary>Search runs on the real exception message (bound parameter, LIKE-escaped) — a phone
    /// number inside the message is found by searching for it.</summary>
    [Fact]
    public async Task GetFailedMessages_Search_MatchesTheRealExceptionMessage()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var queue = $"queue-{Guid.NewGuid():N}"[..24];

        var id = await SeedAsync(
            db, node, queue, FailedMessageKind.Error, "System.TimeoutException", now,
            exceptionMessage: "Duplicate contact 081-234-5678 already registered");

        var response = await _client.GetAsync(
            $"/admin/failed-messages?node={node}&status=All&search=081-234-5678&pageSize=50",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FailedMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.Contains(result!.Items, i => i.Id == id && i.ExceptionMessage.Contains("081-234-5678"));
    }

    /// <summary>A literal SQL LIKE wildcard in the search box (%, _, [) must match literally, not
    /// as a wildcard — proven with a queue name containing "%" that only an escaped search finds.</summary>
    [Fact]
    public async Task GetFailedMessages_Search_EscapesLikeWildcards()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var marker = $"WildcardMarker{Guid.NewGuid():N}";

        // Without ESCAPE, "100%2" is interpreted as "100" + wildcard-any + "2" — which would also match
        // withoutLiteralPercent below (it has "100" ... "2" but never the literal substring "100%2").
        var withLiteralPercent = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now,
            exceptionMessage: $"{marker} 100%2 done");
        var withoutLiteralPercent = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now,
            exceptionMessage: $"{marker} 100 retries then attempt 2 done");

        var response = await _client.GetAsync(
            $"/admin/failed-messages?node={node}&status=All&search={Uri.EscapeDataString(marker + " 100%2")}&pageSize=50",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<FailedMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.Contains(result!.Items, i => i.Id == withLiteralPercent);
        Assert.DoesNotContain(result.Items, i => i.Id == withoutLiteralPercent);
    }

    [Fact]
    public async Task GetFailedMessage_Detail_ReturnsRawBody_IncludesSiblingsAndHistoryAfterRetry()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var sharedMessageId = Guid.CreateVersion7();
        // Past InboxGuard's 5-minute stale-claim window so the retry below is accepted, not TooSoon.
        var faultedAt = now.AddMinutes(-10);

        var id = await SeedAsync(db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException",
            faultedAt, messageId: sharedMessageId);
        var siblingId = await SeedAsync(db, node, "appraisal-status-dashboard", FailedMessageKind.Error,
            "System.TimeoutException", faultedAt, messageId: sharedMessageId);

        var retryResponse = await _client.PostAsJsonAsync(
            "/admin/failed-messages/retry", new { ids = new[] { id }, reason = "manual retry" },
            TestContext.Current.CancellationToken);
        retryResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/admin/failed-messages/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<FailedMessageDetailDto>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Contains("jane@example.com", detail.Body);
        Assert.DoesNotContain("***", detail.Body);
        Assert.Contains(detail.Siblings, s => s.Id == siblingId);
        Assert.Contains(detail.History, h => h.Action == "Retry");
    }

    [Fact]
    public async Task GetFailedMessage_UnknownId_Returns404NotFoundException()
    {
        var response = await _client.GetAsync(
            $"/admin/failed-messages/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("NotFoundException", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task RetryFailedMessages_AcceptsPending_SecondRetrySkippedNotPending()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        // Retry refuses a row inside InboxGuard's 5-minute stale-claim window (TooSoon) — this test
        // is about the Pending/NotPending transition, not that window, so seed a FaultedAt already past it.
        var id = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now.AddMinutes(-10));

        var firstResponse = await _client.PostAsJsonAsync(
            "/admin/failed-messages/retry", new { ids = new[] { id } }, TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<FailedMessageActionResult>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        Assert.Contains(id, first!.Accepted);

        var secondResponse = await _client.PostAsJsonAsync(
            "/admin/failed-messages/retry", new { ids = new[] { id } }, TestContext.Current.CancellationToken);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<FailedMessageActionResult>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(id, second!.Accepted);
        var skipped = second.Skipped.Single(s => s.Id == id);
        Assert.Equal("NotPending", skipped.Reason);
        Assert.NotNull(skipped.By);
        Assert.NotNull(skipped.At);
    }

    [Fact]
    public async Task DiscardFailedMessages_WithoutReason_Returns400ValidationProblem()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var id = await SeedAsync(db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now);

        var response = await _client.PostAsJsonAsync(
            "/admin/failed-messages/discard", new { ids = new[] { id }, reason = "" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("ValidationException", doc.RootElement.GetProperty("title").GetString());
    }

    /// <summary>
    /// A RetryRequested row the collector has just claimed (about to publish, or mid-publish) must
    /// not be discarded out from under it. Simulates "the collector is mid-publish" with the exact same
    /// conditional UPDATE shape the collector's claim uses — real SQL Server, no broker needed.
    /// </summary>
    [Fact]
    public async Task DiscardFailedMessages_RetryRequestedRowWithActiveClaim_IsSkippedAsPublishing()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        var id = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now,
            status: FailedMessageStatus.RetryRequested);

        await db.FailedMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.RetryClaimedAt, now), TestContext.Current.CancellationToken);

        var response = await _client.PostAsJsonAsync(
            "/admin/failed-messages/discard", new { ids = new[] { id }, reason = "duplicate, safe to drop" },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FailedMessageActionResult>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.DoesNotContain(id, result!.Accepted);
        Assert.Contains(result.Skipped, s => s.Id == id && s.Reason == "Publishing");

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var reloaded = await verifyDb.FailedMessages.AsNoTracking()
            .SingleAsync(m => m.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(FailedMessageStatus.RetryRequested, reloaded.Status);
    }

    [Fact]
    public async Task RetryFailedMessages_MoreThan200Ids_Returns400ValidationProblem()
    {
        var ids = Enumerable.Range(0, 201).Select(_ => Guid.CreateVersion7()).ToArray();

        var response = await _client.PostAsJsonAsync(
            "/admin/failed-messages/retry", new { ids }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The detail endpoint returns the raw stack trace and the full stored header set
    /// (including MT-Fault-Message/MT-Fault-StackTrace) directly.</summary>
    [Fact]
    public async Task GetFailedMessage_Detail_ReturnsStackTraceAndFullHeaders()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];

        var message = FailedMessage.Create(
            node, "appraisal-sync", FailedMessageKind.Error, Guid.CreateVersion7(), null,
            "Test.FakeEvent", "Test.FakeConsumer", "System.TimeoutException", "boom",
            "System.TimeoutException: boom\n   at Somewhere()", 0, now, now,
            null, null, null, "{\"appraisalId\":\"8f2c\"}"u8.ToArray(), "application/json",
            new Dictionary<string, string>
            {
                ["MT-Message-Type"] = "urn:message:Test:FakeEvent",
                ["MT-Fault-Message"] = "boom",
                ["MT-Fault-StackTrace"] = "System.TimeoutException: boom\n   at Somewhere()"
            });
        db.FailedMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/admin/failed-messages/{message.Id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<FailedMessageDetailDto>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Contains("8f2c", detail.Body);
        Assert.Equal("System.TimeoutException: boom\n   at Somewhere()", detail.StackTrace);
        Assert.Equal("boom", detail.Headers["MT-Fault-Message"]);
        Assert.Equal("System.TimeoutException: boom\n   at Somewhere()", detail.Headers["MT-Fault-StackTrace"]);
    }

    /// <summary>A non-JSON body is returned as its raw text, not rejected or replaced.</summary>
    [Fact]
    public async Task GetFailedMessage_Detail_NonJsonBody_ReturnsRawText()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];

        var message = FailedMessage.Create(
            node, "appraisal-sync", FailedMessageKind.Error, Guid.CreateVersion7(), null,
            "Test.FakeEvent", "Test.FakeConsumer", "System.TimeoutException", "boom",
            null, 0, now, now,
            null, null, null, "not an envelope"u8.ToArray(), "text/plain",
            new Dictionary<string, string> { ["MT-Fault-Message"] = "boom" });
        db.FailedMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await _client.GetAsync(
            $"/admin/failed-messages/{message.Id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<FailedMessageDetailDto>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal("not an envelope", detail.Body);
        Assert.Null(detail.StackTrace);
    }

    [Fact]
    public async Task GetFailedMessagesSummary_WithoutPermission_Returns403()
    {
        await using var noPermissionFactory = new NoFailedMessagePermissionWebApplicationFactory(
            Fixture.ConnectionString, Fixture.RabbitMq.GetConnectionString());
        using var client = noPermissionFactory.CreateClient();

        var response = await client.GetAsync("/admin/failed-messages/summary", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Every write endpoint (both consumer-side and outbox-side) must deny a
    /// FAILED_MESSAGE_VIEW-only caller — permission failure is handled by the auth middleware before
    /// MediatR runs (api-contract.md "403"), so a dummy id/module never needs to exist for this to 403.
    /// Uses the fixture's one shared ViewOnly factory (see its doc comment) rather than a fresh
    /// WebApplicationFactory per case.</summary>
    [Theory]
    [InlineData("retry")]
    [InlineData("discard")]
    [InlineData("resend")]
    public async Task WriteEndpoints_ViewOnlyPermission_Return403(string endpoint)
    {
        using var client = Fixture.ViewOnlyFailedMessagePermissionWebApplicationFactory.CreateClient();
        var dummyId = Guid.CreateVersion7();
        var ct = TestContext.Current.CancellationToken;

        var response = endpoint switch
        {
            "retry" => await client.PostAsJsonAsync(
                "/admin/failed-messages/retry", new { ids = new[] { dummyId } }, ct),
            "discard" => await client.PostAsJsonAsync(
                "/admin/failed-messages/discard", new { ids = new[] { dummyId }, reason = "test" }, ct),
            "resend" => await client.PostAsJsonAsync(
                "/admin/outbox-messages/resend",
                new { items = new[] { new { module = "request", id = dummyId } } }, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Two admins retrying the same row at once must not both "win" — the handler saves each row on
    /// its own and relies on the SQL Server rowversion concurrency token (RetryFailedMessagesCommandHandler)
    /// to make exactly one UPDATE succeed; the loser reports NotPending. This is deterministic (not a race
    /// to assert around) because the two UPDATEs are serialized by SQL Server itself — whichever commits
    /// first invalidates the other's rowversion — so no retry loop is needed here.
    /// </summary>
    [Fact]
    public async Task RetryFailedMessages_ConcurrentRetries_ExactlyOneAcceptedOneSkippedNotPending()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var node = $"NODE-{Guid.NewGuid():N}"[..12];
        // Past InboxGuard's 5-minute stale-claim window, same reasoning as the test above.
        var id = await SeedAsync(
            db, node, "appraisal-sync", FailedMessageKind.Error, "System.TimeoutException", now.AddMinutes(-10));

        var ct = TestContext.Current.CancellationToken;
        var firstCall = _client.PostAsJsonAsync("/admin/failed-messages/retry", new { ids = new[] { id } }, ct);
        var secondCall = _client.PostAsJsonAsync("/admin/failed-messages/retry", new { ids = new[] { id } }, ct);
        var responses = await Task.WhenAll(firstCall, secondCall);

        foreach (var response in responses)
            response.EnsureSuccessStatusCode();

        var results = await Task.WhenAll(responses.Select(r =>
            r.Content.ReadFromJsonAsync<FailedMessageActionResult>(JsonHelper.Options, ct)));

        var acceptedCount = results.Sum(r => r!.Accepted.Count(a => a == id));
        var notPendingSkips = results.SelectMany(r => r!.Skipped).Where(s => s.Id == id && s.Reason == "NotPending").ToList();

        Assert.Equal(1, acceptedCount);
        Assert.Single(notPendingSkips);

        using var verifyScope = CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var reloaded = await verifyDb.FailedMessages.AsNoTracking().SingleAsync(m => m.Id == id, ct);
        Assert.Equal(FailedMessageStatus.RetryRequested, reloaded.Status);
    }
}
