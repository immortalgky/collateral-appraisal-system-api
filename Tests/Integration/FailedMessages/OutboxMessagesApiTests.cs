using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Integration.Application.Features.FailedMessages.GetFailedMessagesSummary;
using Integration.Application.Features.OutboxMessages.GetOutboxMessage;
using Integration.Application.Features.OutboxMessages.GetOutboxMessages;
using Integration.Application.Features.OutboxMessages.ResendOutboxMessages;
using Integration.Fixtures;
using Integration.Helpers;
using Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Pagination;
using Shared.Time;

namespace Integration.FailedMessages;

/// <summary>
/// Exercises the outbox admin endpoints against real SQL across two module schemas
/// (docs/failed-messages/api-contract.md "Outbox module whitelist").
/// </summary>
public class OutboxMessagesApiTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static readonly string ResolvableEventType =
        typeof(AssignmentSlaRecalculatedIntegrationEvent).AssemblyQualifiedName!;

    private async Task<Guid> SeedAsync(
        string module, string status, DateTime occurredAt, string payload = "{}",
        string? eventType = null, DateTime? processedAt = null, DateTime? processingStartedAt = null,
        string? correlationId = null, string? error = null)
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();

        var id = Guid.CreateVersion7();
        // Table name is plain string concatenation (module is a test literal, never user input) —
        // ExecuteSqlInterpolatedAsync would parameterize a {module} hole too, producing
        // "INSERT INTO @p0.IntegrationEventOutbox" (invalid object name), so ExecuteSqlRawAsync with
        // positional placeholders is used instead, same as OutboxCleanupJob.
        // EF itself needs a plain C# null for a SQL NULL parameter here (DBNull.Value throws — EF's raw-SQL
        // parameter binder has no store type mapping for the DBNull CLR type), but ExecuteSqlRawAsync's
        // params overload declares a non-nullable object[] even though it accepts null elements — a gap in
        // the framework's own nullable annotations, not something a per-value `!` could honestly fix (these
        // values genuinely can be null). Built as the correctly-annotated object?[] and cast to object[] for
        // the call: both are the same array type at runtime (array covariance), so the cast is exact, not a
        // suppression of anything actually unsafe.
        object?[] parameters =
        [
            id, eventType ?? ResolvableEventType, payload, "{}", correlationId, occurredAt, processedAt, error,
            status, processingStartedAt
        ];
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO [" + module + "].[IntegrationEventOutbox] " +
            "(Id, EventType, Payload, Headers, CorrelationId, OccurredAt, ProcessedAt, Error, RetryCount, Status, ProcessingStartedAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, 0, {8}, {9})",
            (object[])parameters);
        return id;
    }

    [Fact]
    public async Task GetOutboxMessages_StatusFailed_ReturnsOnlyFailedAcrossModules_CamelCaseAndOmittedNulls()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var requestFailed = await SeedAsync("request", "Failed", now);
        var appraisalFailed = await SeedAsync("appraisal", "Failed", now, eventType: "Some.Renamed.Event, Nowhere");
        var requestProcessed = await SeedAsync("request", "Processed", now, processedAt: now);

        var response = await _client.GetAsync(
            "/admin/outbox-messages?status=Failed&pageSize=50", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);

        Assert.Equal(1, doc.RootElement.GetProperty("pageNumber").GetInt32());

        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        var ids = items.Select(i => Guid.Parse(i.GetProperty("id").GetString()!)).ToList();
        Assert.Contains(requestFailed, ids);
        Assert.Contains(appraisalFailed, ids);
        Assert.DoesNotContain(requestProcessed, ids);

        var requestFailedItem = items.First(i => Guid.Parse(i.GetProperty("id").GetString()!) == requestFailed);
        Assert.False(requestFailedItem.TryGetProperty("processedAt", out _), "null processedAt must be omitted, not null.");
        Assert.True(requestFailedItem.GetProperty("typeResolvable").GetBoolean());

        var appraisalFailedItem = items.First(i => Guid.Parse(i.GetProperty("id").GetString()!) == appraisalFailed);
        Assert.False(appraisalFailedItem.GetProperty("typeResolvable").GetBoolean());
    }

    /// <summary>
    /// The list query pages over (Module, Id, OccurredAt) only, then fetches Payload/display columns
    /// and newerSentCount ONE QUERY PER MODULE PRESENT ON THE PAGE — this page has two (request,
    /// appraisal). Also covers a real newerSentCount > 0: requestFailed shares a CorrelationId with a
    /// LATER Processed row in the same module.
    /// </summary>
    /// <summary>
    /// Rows sharing one OccurredAt across two modules must still page deterministically: the ORDER BY
    /// carries a Module + Id tie-breaker, so no row repeats or vanishes across OFFSET/FETCH pages.
    /// </summary>
    [Fact]
    public async Task GetOutboxMessages_EqualOccurredAt_PagesInStableOrderWithoutRepeatsOrGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var eventType = $"Test.TieBreak{Guid.NewGuid():N}";

        for (var i = 0; i < 4; i++)
        {
            await SeedAsync("appraisal", "Failed", now, eventType: eventType);
            await SeedAsync("request", "Failed", now, eventType: eventType);
        }

        // Union order: OccurredAt DESC, then Module, then Id DESC (SQL Server's own uniqueidentifier order).
        var expected = new List<Guid>();
        foreach (var module in new[] { "appraisal", "request" })
            expected.AddRange(await db.Database
                .SqlQueryRaw<Guid>(
                    "SELECT Id AS Value FROM [" + module + "].[IntegrationEventOutbox] WHERE EventType = {0} ORDER BY Id DESC",
                    eventType)
                .ToListAsync(ct));

        var paged = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var response = await _client.GetAsync(
                $"/admin/outbox-messages?status=Failed&search={eventType}&pageSize=2&pageNumber={page}", ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            paged.AddRange(doc.RootElement.GetProperty("items").EnumerateArray()
                .Select(i => Guid.Parse(i.GetProperty("id").GetString()!)));
        }

        Assert.Equal(expected, paged);
    }

    [Fact]
    public async Task GetOutboxMessages_TwoModulesOnOnePage_ComputesNewerSentCountPerRow()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var marker = $"B8Marker{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString();

        var requestFailed = await SeedAsync(
            "request", "Failed", now, eventType: $"Test.{marker}.Request", correlationId: correlationId);
        var appraisalFailed = await SeedAsync(
            "appraisal", "Failed", now, eventType: $"Test.{marker}.Appraisal");
        // A LATER Processed row sharing requestFailed's CorrelationId — not returned by this "Failed"
        // query itself, but still counted server-side (newerSentCount isn't scoped by the page filter).
        await SeedAsync(
            "request", "Processed", now.AddMinutes(1), processedAt: now.AddMinutes(1), correlationId: correlationId);

        var response = await _client.GetAsync(
            $"/admin/outbox-messages?status=Failed&search={marker}&pageSize=50", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(result);

        var requestItem = result!.Items.Single(i => i.Id == requestFailed);
        Assert.Equal("request", requestItem.Module);
        Assert.Equal(1, requestItem.NewerSentCount);

        var appraisalItem = result.Items.Single(i => i.Id == appraisalFailed);
        Assert.Equal("appraisal", appraisalItem.Module);
        Assert.Equal(0, appraisalItem.NewerSentCount);
    }

    /// <summary>
    /// OutboxCleanupJob deletes Processed rows after ProcessedRetentionDays but keeps Failed rows for 90 days, so on
    /// a Failed row older than the Processed retention window "0 newer sent" is unknowable — it must come
    /// back as an explicit null (not 0, not omitted) on both the list and the detail endpoint.
    /// </summary>
    [Fact]
    public async Task NewerSentCount_FailedRowOlderThanProcessedRetention_IsExplicitNullOnListAndDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var marker = $"RetentionMarker{Guid.NewGuid():N}";

        var old = await SeedAsync(
            "request", "Failed", now.AddDays(-(OutboxDeliveryPolicy.ProcessedRetentionDays + 1)),
            eventType: $"Test.{marker}.Old", correlationId: Guid.NewGuid().ToString());
        var recent = await SeedAsync(
            "request", "Failed", now, eventType: $"Test.{marker}.Recent", correlationId: Guid.NewGuid().ToString());
        // Old but with no CorrelationId: there is no sibling group at all, so 0 is a certainty, not a guess.
        var oldNoCorrelation = await SeedAsync(
            "request", "Failed", now.AddDays(-(OutboxDeliveryPolicy.ProcessedRetentionDays + 1)),
            eventType: $"Test.{marker}.OldNoCorrelation");

        var list = await _client.GetAsync($"/admin/outbox-messages?status=Failed&search={marker}&pageSize=50", ct);
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct));
        var items = listDoc.RootElement.GetProperty("items").EnumerateArray().ToList();
        JsonElement Item(Guid id) => items.Single(i => Guid.Parse(i.GetProperty("id").GetString()!) == id);

        Assert.Equal(JsonValueKind.Null, Item(old).GetProperty("newerSentCount").ValueKind);
        Assert.Equal(0, Item(recent).GetProperty("newerSentCount").GetInt32());
        Assert.Equal(0, Item(oldNoCorrelation).GetProperty("newerSentCount").GetInt32());

        var detail = await _client.GetAsync($"/admin/outbox-messages/request/{old}", ct);
        detail.EnsureSuccessStatusCode();
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(ct));
        Assert.Equal(JsonValueKind.Null, detailDoc.RootElement.GetProperty("newerSentCount").ValueKind);

        var recentDetail = await _client.GetAsync($"/admin/outbox-messages/request/{recent}", ct);
        using var recentDoc = JsonDocument.Parse(await recentDetail.Content.ReadAsStringAsync(ct));
        Assert.Equal(0, recentDoc.RootElement.GetProperty("newerSentCount").GetInt32());
    }

    /// <summary>
    /// The OccurredAt of the failed row is compared with a datetime2 column. Bound as a plain Dapper
    /// DateTime it becomes SqlDbType.DateTime (3.33 ms rounding), which can round the failed row's own
    /// OccurredAt UP past a sibling that really is newer. Here the failed row is at .0030000 (rounds up to
    /// .0033333 as a datetime) and the sibling at .0032000 is genuinely newer — it must still count.
    /// </summary>
    [Fact]
    public async Task NewerSentCount_SiblingWithinDatetimeRoundingWindow_IsStillCounted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var wholeSecond = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, now.Kind);
        var marker = $"Dt2Marker{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid().ToString();

        var failed = await SeedAsync(
            "request", "Failed", wholeSecond.AddTicks(30_000), eventType: $"Test.{marker}", correlationId: correlationId);
        await SeedAsync(
            "request", "Processed", wholeSecond.AddTicks(32_000), processedAt: wholeSecond.AddTicks(32_000),
            correlationId: correlationId);

        var list = await _client.GetAsync($"/admin/outbox-messages?status=Failed&search={marker}&pageSize=50", ct);
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct));
        var item = listDoc.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => Guid.Parse(i.GetProperty("id").GetString()!) == failed);
        Assert.Equal(1, item.GetProperty("newerSentCount").GetInt32());

        var detail = await _client.GetAsync($"/admin/outbox-messages/request/{failed}", ct);
        detail.EnsureSuccessStatusCode();
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(ct));
        Assert.Equal(1, detailDoc.RootElement.GetProperty("newerSentCount").GetInt32());
    }

    [Fact]
    public async Task GetOutboxMessages_StatusStuck_ReturnsOnlyProcessingOlderThanFixedThreshold()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        // The Stuck display threshold (2 min) is separate from and shorter than the live delivery
        // service's own 5-minute auto-reset — seeding at 3 minutes old is past Stuck but still untouched
        // by that background loop for the life of this test.
        var staleAge = OutboxDeliveryPolicy.StuckThreshold + TimeSpan.FromMinutes(1);
        Assert.True(staleAge < OutboxDeliveryPolicy.OrphanedProcessingThreshold);

        var stale = await SeedAsync("request", "Processing", now, processingStartedAt: now - staleAge);
        var fresh = await SeedAsync("request", "Processing", now, processingStartedAt: now.AddSeconds(-5));
        // Rolling-deploy safety: a NULL ProcessingStartedAt only counts as stuck when OccurredAt
        // is also old — a row that just entered Processing while the column is still being backfilled
        // must not flash "stuck" immediately.
        var nullStartedOld = await SeedAsync("request", "Processing", now - staleAge, processingStartedAt: null);
        var nullStartedRecent = await SeedAsync("request", "Processing", now, processingStartedAt: null);

        var response = await _client.GetAsync(
            "/admin/outbox-messages?status=Stuck&module=request&pageSize=50", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        var ids = result!.Items.Select(i => i.Id).ToList();

        Assert.Contains(stale, ids);
        Assert.Contains(nullStartedOld, ids);
        Assert.DoesNotContain(fresh, ids);
        Assert.DoesNotContain(nullStartedRecent, ids);
    }

    /// <summary>
    /// The Status/Module/Search predicates are pushed inside each per-module branch of the UNION — this proves
    /// they still select exactly the right rows: the Module filter leaves the other module's matching row out,
    /// Search matches EventType or CorrelationId, and a LIKE wildcard in the search text is matched literally.
    /// </summary>
    [Fact]
    public async Task GetOutboxMessages_ModuleAndSearchFilters_SelectOnlyMatchingRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var marker = $"FilterMarker{Guid.NewGuid():N}";
        var correlationId = $"cid-{marker}";

        var requestRow = await SeedAsync("request", "Failed", now, eventType: $"Test.{marker}.A");
        var appraisalRow = await SeedAsync("appraisal", "Failed", now, eventType: $"Test.{marker}.B");
        var byCorrelation = await SeedAsync("workflow", "Failed", now, eventType: "Test.Other", correlationId: correlationId);

        async Task<List<Guid>> IdsAsync(string query)
        {
            var response = await _client.GetAsync($"/admin/outbox-messages?status=Failed&pageSize=50&{query}", ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(JsonHelper.Options, ct);
            return result!.Items.Select(i => i.Id).ToList();
        }

        Assert.Equal([appraisalRow], await IdsAsync($"module=appraisal&search={marker}"));
        Assert.Equal([requestRow], await IdsAsync($"module=request&search={marker}"));
        Assert.Empty(await IdsAsync($"module=document&search={marker}"));
        Assert.Equivalent(new[] { requestRow, appraisalRow }, await IdsAsync($"search={marker}.")); // EventType only
        Assert.Equal([byCorrelation], await IdsAsync($"search={correlationId}"));
        Assert.Empty(await IdsAsync("search=%25" + marker)); // '%' is literal, not a wildcard
    }

    /// <summary>
    /// The list's Stuck tab and the summary's stuck count share ONE predicate (OutboxUnionSql.StuckPredicate),
    /// so for the same rows they must agree: stale Processing and NULL-started-and-old count, fresh ones do not.
    /// </summary>
    [Fact]
    public async Task StuckPredicate_ListAndSummaryAgree()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var staleAge = OutboxDeliveryPolicy.StuckThreshold + TimeSpan.FromMinutes(1);

        async Task<int> SummaryStuckAsync()
        {
            var response = await _client.GetAsync("/admin/failed-messages/summary", ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<FailedMessagesSummaryDto>(JsonHelper.Options, ct))!
                .OutboxStuckCount;
        }

        var before = await SummaryStuckAsync();

        var stale = await SeedAsync("document", "Processing", now, processingStartedAt: now - staleAge);
        await SeedAsync("document", "Processing", now, processingStartedAt: now.AddSeconds(-5));
        var nullStartedOld = await SeedAsync("document", "Processing", now - staleAge, processingStartedAt: null);
        await SeedAsync("document", "Processing", now, processingStartedAt: null);

        var after = await SummaryStuckAsync();

        var response = await _client.GetAsync("/admin/outbox-messages?status=Stuck&module=document&pageSize=50", ct);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(JsonHelper.Options, ct);

        Assert.Equal(2, after - before);
        Assert.Contains(list!.Items, i => i.Id == stale);
        Assert.Contains(list.Items, i => i.Id == nullStartedOld);
    }

    [Fact]
    public async Task GetOutboxMessages_BlankModule_MeansAllModules()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var marker = $"blank-module-{Guid.NewGuid():N}";
        var inRequest = await SeedAsync("request", "Failed", now, correlationId: marker);
        var inDocument = await SeedAsync("document", "Failed", now, correlationId: marker);

        var response = await _client.GetAsync(
            $"/admin/outbox-messages?status=Failed&module=&search={marker}&pageSize=50",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        Assert.Equal(
            new[] { inRequest, inDocument }.Order(),
            list!.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task GetOutboxMessage_Detail_ReturnsRawPayloadAndError()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var payload = """{"email":"jane@example.com","appraisalId":"8f2c7c2e-1111-4a11-9a11-0000000000aa"}""";
        var id = await SeedAsync("request", "Failed", now, payload, error: "Notify failed for 081-234-5678");

        var response = await _client.GetAsync($"/admin/outbox-messages/request/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<OutboxMessageDetailDto>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal(payload, dto.Payload);
        Assert.Equal("Notify failed for 081-234-5678", dto.Error);
    }

    [Fact]
    public async Task GetOutboxMessages_List_ReturnsRawError()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var id = await SeedAsync("request", "Failed", now, error: "Notify failed for 081-234-5678");

        var response = await _client.GetAsync(
            "/admin/outbox-messages?status=Failed&module=request&pageSize=50", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.Equal("Notify failed for 081-234-5678", list!.Items.Single(i => i.Id == id).Error);
    }

    [Fact]
    public async Task GetOutboxMessage_UnknownId_Returns404NotFoundException()
    {
        var response = await _client.GetAsync(
            $"/admin/outbox-messages/request/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("NotFoundException", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetOutboxMessage_UnknownModule_Returns400ValidationProblem()
    {
        var response = await _client.GetAsync(
            $"/admin/outbox-messages/not-a-real-module/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("ValidationException", doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ResendOutboxMessages_FlipsFailedToPending_SkipsNotFailedAndUnknownModule_ThenFindableAsResent()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;

        var failedId = await SeedAsync("request", "Failed", now);
        var processedId = await SeedAsync("request", "Processed", now, processedAt: now);

        var request = new ResendOutboxMessagesRequest(
            [
                new OutboxMessageRef("request", failedId),
                new OutboxMessageRef("request", processedId),
                new OutboxMessageRef("bogus-module", Guid.NewGuid())
            ],
            "retrying after LOS outage");

        var response = await _client.PostAsJsonAsync("/admin/outbox-messages/resend", request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OutboxResendResult>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains(result.Accepted, a => a.Id == failedId);
        Assert.Contains(result.Skipped, s => s.Id == processedId && s.Reason == "NotFailed");
        Assert.Contains(result.Skipped, s => s.Reason == "UnknownModule");

        var resentResponse = await _client.GetAsync(
            "/admin/outbox-messages?status=Resent&module=request&pageSize=50", TestContext.Current.CancellationToken);
        resentResponse.EnsureSuccessStatusCode();
        var resent = await resentResponse.Content.ReadFromJsonAsync<PaginatedResult<OutboxMessageListDto>>(
            JsonHelper.Options, TestContext.Current.CancellationToken);
        Assert.Contains(resent!.Items, i => i.Id == failedId);

        // The UPDATE and its audit row commit together — every accepted item has one.
        using var auditScope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        foreach (var acceptedItem in result.Accepted)
        {
            var audit = await auditDb.FailedMessageAuditLogs.AsNoTracking().FirstOrDefaultAsync(
                a => a.TargetId == acceptedItem.Id && a.Source == "Outbox" && a.Action == "OutboxResend",
                TestContext.Current.CancellationToken);
            Assert.NotNull(audit);
            Assert.Equal(acceptedItem.Module, audit!.OutboxModule);
        }
    }

    [Fact]
    public async Task ResendOutboxMessages_DuplicateItems_AreResentOnce_NotAcceptedAndSkipped()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var failedId = await SeedAsync("request", "Failed", now);

        var request = new ResendOutboxMessagesRequest(
            [new OutboxMessageRef("request", failedId), new OutboxMessageRef("request", failedId)], "dup");

        var response = await _client.PostAsJsonAsync("/admin/outbox-messages/resend", request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OutboxResendResult>(
            JsonHelper.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Single(result.Accepted);
        Assert.Empty(result.Skipped);
    }

    /// <summary>
    /// <c>{"items":[null]}</c> used to throw a NullReferenceException inside the handler loop (a 500) AFTER
    /// the rows before the null had already been committed. The validator must reject a null / blank item
    /// with a 400 before anything runs — proved here by a valid Failed row ahead of the bad item staying Failed.
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("""{"id":"{0}"}""")] // module missing
    [InlineData("""{"module":"","id":"{0}"}""")]
    [InlineData("""{"module":"request"}""")] // id missing -> Guid.Empty
    public async Task ResendOutboxMessages_NullOrBlankItem_Returns400_AndRunsNothing(string badItem)
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var failedId = await SeedAsync("request", "Failed", now);

        var json = $$"""{"items":[{"module":"request","id":"{{failedId}}"},{{badItem.Replace("{0}", Guid.NewGuid().ToString())}}]}""";
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(
            "/admin/outbox-messages/resend", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var verifyScope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var status = await db.Database.SqlQueryRaw<string>(
                "SELECT Status AS Value FROM [request].[IntegrationEventOutbox] WHERE Id = {0}", failedId)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Failed", status);
    }

    /// <summary>
    /// <c>failureClass</c> is derived server-side from the prefix the delivery service writes (see
    /// <c>OutboxFailureReasons</c>) AND what the row's EventType resolves to now, so the FE never matches error
    /// text. A "Disallowed type:" row written by the OLD delivery code (which merged unresolvable and disallowed)
    /// is Disallowed only if its type really resolves to a disallowed one. Always serialised — an unclassified
    /// Failed row carries an explicit <c>null</c>, never an omitted key. Rows are found by a unique correlation id
    /// (search matches EventType or CorrelationId) because a resolvable EventType is shared between rows.
    /// </summary>
    [Theory]
    [InlineData("Disallowed type: System.Guid", "disallowed", "Disallowed")]
    [InlineData("Disallowed type: Some.Old.Event, Nowhere", "unresolvable", "Unresolvable")] // legacy mislabel
    [InlineData("Disallowed type: Some.Event, Asm", "resolvable", "Unresolvable")] // type now resolves
    [InlineData("Unresolvable type: Some.Type, Asm", "unresolvable", "Unresolvable")]
    [InlineData("Deserialization failed: bad json", "resolvable", "Deserialization")]
    [InlineData("Deserialization returned null", "resolvable", "Deserialization")]
    [InlineData("System.Net.Http.HttpRequestException: Connection refused", "resolvable", null)]
    public async Task FailureClass_FailedRow_IsClassifiedOnListAndDetail(
        string error, string typeKind, string? expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var eventType = typeKind switch
        {
            "disallowed" => typeof(Guid).AssemblyQualifiedName!, // resolves, but outside the allowed namespace
            "unresolvable" => $"Test.FailureClass{Guid.NewGuid():N}, Nowhere",
            _ => ResolvableEventType
        };
        var correlationId = $"failure-class-{Guid.NewGuid():N}";
        var id = await SeedAsync("request", "Failed", now, eventType: eventType, correlationId: correlationId,
            error: error);

        var list = await _client.GetAsync($"/admin/outbox-messages?status=Failed&search={correlationId}", ct);
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct));
        var item = Assert.Single(listDoc.RootElement.GetProperty("items").EnumerateArray());
        AssertFailureClass(item, expected);

        var detail = await _client.GetAsync($"/admin/outbox-messages/request/{id}", ct);
        detail.EnsureSuccessStatusCode();
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(ct));
        AssertFailureClass(detailDoc.RootElement, expected);
    }

    /// <summary>Only a Failed row is classified: the same error text on a Pending row yields null.</summary>
    [Fact]
    public async Task FailureClass_NonFailedRowWithClassifiedErrorText_IsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var eventType = $"Test.FailureClass{Guid.NewGuid():N}";
        var id = await SeedAsync("request", "Pending", now, eventType: eventType, error: "Disallowed type: X, Asm");

        var list = await _client.GetAsync($"/admin/outbox-messages?status=All&search={eventType}", ct);
        list.EnsureSuccessStatusCode();
        using var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync(ct));
        AssertFailureClass(Assert.Single(listDoc.RootElement.GetProperty("items").EnumerateArray()), null);

        var detail = await _client.GetAsync($"/admin/outbox-messages/request/{id}", ct);
        detail.EnsureSuccessStatusCode();
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(ct));
        AssertFailureClass(detailDoc.RootElement, null);
    }

    private static void AssertFailureClass(JsonElement element, string? expected)
    {
        Assert.True(element.TryGetProperty("failureClass", out var value), "failureClass must always be serialised.");
        if (expected is null)
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        else
            Assert.Equal(expected, value.GetString());
    }
}
