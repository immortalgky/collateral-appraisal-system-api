using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;
using Address = Appraisal.Domain.Appraisals.Address;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// <see cref="GetConstructionChainStatusQuery"/> over a real database: the tree walk (up to the root, down over
/// every descendant), the open-request lookup on <c>request.Requests</c>, and the two SQL objects it stands on —
/// <c>vw_AppraisalConstructionProgress</c> and the backfill script that fills the Summary* columns of existing
/// inspections. The decision rules themselves are unit-tested in ConstructionChainStatusRulesTests.
///
/// Every test seeds its own tree, so nothing depends on what other tests left in the shared database.
/// </summary>
[Collection("Integration")]
public class ConstructionChainStatusTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceScope CreateScope() => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static readonly DateTime T0 = new(2026, 6, 1, 9, 0, 0);

    /// <summary>
    /// One appraisal in a tree. Status / CompletedAt are set by reflection — the real status flow needs a workflow,
    /// and only the read path is under test.
    /// </summary>
    private static async Task<AppraisalAggregate> SeedAsync(
        AppraisalDbContext db,
        string type,
        string purpose,
        Guid? prev,
        DateTime? completedAt,
        AppraisalStatus? status = null,
        decimal? summaryCurrentPct = null,
        decimal totalValue = 1_000_000m,
        int? inspectionNumber = null,
        Guid? requestId = null)
    {
        var a = AppraisalAggregate.Create(
            requestId ?? Guid.NewGuid(), type, "Normal", DateTime.Now, purpose: purpose, prevAppraisalId: prev);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..14]);
        if (inspectionNumber is { } n) a.SetInspectionNumber(n);
        if (completedAt is { } at)
        {
            typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(a, status ?? AppraisalStatus.Completed);
            typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, at);
        }
        else if (status is not null)
        {
            typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(a, status);
        }

        if (summaryCurrentPct is { } pct)
        {
            var prop = a.AddLandProperty();
            prop.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
            var inspection = ConstructionInspection.CreateSummary(
                Guid.Empty, totalValue, null, null, pct, null);
            prop.SetConstructionInspection(inspection);
            typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(inspection, prop.Id);
        }

        db.Appraisals.Add(a);
        await db.SaveChangesAsync(Ct);
        return a;
    }

    private static Task<ConstructionChainStatusResult?> StatusOf(IServiceScope scope, Guid id, Guid? excludeRequestId = null) =>
        scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetConstructionChainStatusQuery(id, excludeRequestId), Ct);

    private async Task InsertRequestAsync(Guid requestId, Guid prevAppraisalId, string purpose, DateTime? requestedAt, string status, string number)
    {
        await using var conn = new SqlConnection(fixture.ConnectionString);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO request.Requests (Id, Status, Requestor, RequestorName, Creator, CreatorName, CreatedAt, IsPma, IsDeleted,
                                          Purpose, RequestNumber, RequestedAt)
            VALUES (@requestId, @status, N'u', N'u', N'u', N'u', SYSDATETIME(), 0, 0, @purpose, @number, @requestedAt);
            INSERT INTO request.RequestDetails (RequestId, HasAppraisalBook, PrevAppraisalId)
            VALUES (@requestId, 0, @prevAppraisalId);
            """, new { requestId, status, purpose, number, requestedAt, prevAppraisalId }, cancellationToken: Ct));
    }

    // ── progress ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tree_without_inspections_is_NoData_and_a_missing_appraisal_is_null()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var original = await SeedAsync(db, AppraisalTypes.New, "01", null, T0);

        var result = await StatusOf(scope, original.Id);

        Assert.Equal(ConstructionChainStatuses.NoData, result!.Construction.Status);
        Assert.Null(result.BlockReason);
        Assert.Null(await StatusOf(scope, Guid.NewGuid()));
    }

    [Fact]
    public async Task Progress_is_reported_from_the_newest_appraisal_holding_inspections()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var original = await SeedAsync(db, AppraisalTypes.New, "01", null, T0, summaryCurrentPct: 10m);
        var ci = await SeedAsync(db, AppraisalTypes.Progressive, "06", original.Id, T0.AddDays(30),
            summaryCurrentPct: 72.69m, inspectionNumber: 1);

        // Picking the ORIGINAL still reads the latest round — the walk goes down as well as up.
        var result = await StatusOf(scope, original.Id);

        Assert.Equal(ConstructionChainStatuses.InProgress, result!.Construction.Status);
        Assert.Equal(72.69m, result.Construction.ProgressPct);
        Assert.Equal(ci.Id, result.Construction.SourceAppraisalId);
        Assert.Equal(ci.AppraisalNumber, result.Construction.SourceAppraisalNumber);
        Assert.Equal(1, result.Construction.SourceInspectionNumber);
        Assert.Equal(T0.AddDays(30), result.Construction.SourceCompletedAt);
        Assert.Equal(ConstructionChainBlockReasons.LaterInspectionExists, result.BlockReason);
        Assert.Equal(ci.Id, result.LaterInspection!.AppraisalId);
    }

    [Fact]
    public async Task The_selected_figure_is_the_picks_own_and_NoData_for_a_reappraisal_of_an_inspected_chain()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 6.75m, inspectionNumber: 1);
        var reappraisal = await SeedAsync(db, AppraisalTypes.ReAppraisal, "03", ci.Id, T0.AddDays(20));

        var pickedReappraisal = await StatusOf(scope, reappraisal.Id);

        Assert.Equal(ConstructionChainStatuses.NoData, pickedReappraisal!.Selected.Status);
        Assert.Null(pickedReappraisal.Selected.ProgressPct);
        Assert.Equal(6.75m, pickedReappraisal.Construction.ProgressPct);   // chain figure unchanged

        var pickedCi = await StatusOf(scope, ci.Id);

        Assert.Equal(ConstructionChainStatuses.InProgress, pickedCi!.Selected.Status);
        Assert.Equal(6.75m, pickedCi.Selected.ProgressPct);
        Assert.Equal(ci.Id, pickedCi.Construction.SourceAppraisalId);
        Assert.Null(pickedCi.LatestAppraisal!.ProgressPct);                // the reappraisal holds none
    }

    [Fact]
    public async Task A_finished_pick_is_Complete_and_the_latest_appraisal_carries_its_own_progress()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 100m, inspectionNumber: 1);
        var reappraisal = await SeedAsync(db, AppraisalTypes.ReAppraisal, "03", ci.Id, T0.AddDays(20), summaryCurrentPct: 40m);

        var result = await StatusOf(scope, ci.Id);

        Assert.Equal(ConstructionChainStatuses.Complete, result!.Selected.Status);
        Assert.Equal(100m, result.Selected.ProgressPct);
        Assert.Equal(reappraisal.Id, result.LatestAppraisal!.AppraisalId);
        Assert.Equal(40m, result.LatestAppraisal.ProgressPct);
    }

    [Fact]
    public async Task A_finished_building_blocks_with_ConstructionComplete()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 100m, inspectionNumber: 3);

        var result = await StatusOf(scope, ci.Id);

        Assert.Equal(ConstructionChainStatuses.Complete, result!.Construction.Status);
        Assert.Equal(100m, result.Construction.ProgressPct);
        Assert.Equal(ConstructionChainBlockReasons.ConstructionComplete, result.BlockReason);
    }

    // ── the sibling case, end to end ────────────────────────────────────────────

    [Fact]
    public async Task CI3_picked_with_CI4_and_a_reappraisal_beside_it_is_blocked_and_CI4_picked_is_offered_the_reappraisal()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci3 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, new DateTime(2026, 7, 1),
            summaryCurrentPct: 60m, inspectionNumber: 3);
        var ci4 = await SeedAsync(db, AppraisalTypes.Progressive, "06", ci3.Id, new DateTime(2026, 8, 10),
            summaryCurrentPct: 80m, inspectionNumber: 4);
        var reappraisal = await SeedAsync(db, AppraisalTypes.ReAppraisal, "03", ci3.Id, new DateTime(2026, 8, 15));

        var picked3 = await StatusOf(scope, ci3.Id);

        Assert.Equal(ConstructionChainBlockReasons.LaterInspectionExists, picked3!.BlockReason);
        Assert.Equal(ci4.Id, picked3.LaterInspection!.AppraisalId);
        Assert.Equal(4, picked3.LaterInspection.InspectionNumber);
        Assert.Equal(80m, picked3.Construction.ProgressPct);       // CI#4 is the newest with data, not CI#3
        Assert.Null(picked3.LatestAppraisal);

        var picked4 = await StatusOf(scope, ci4.Id);

        Assert.Null(picked4!.BlockReason);
        Assert.Equal(reappraisal.Id, picked4.LatestAppraisal!.AppraisalId);
        Assert.Equal("03", picked4.LatestAppraisal.PurposeCode);
    }

    [Fact]
    public async Task A_cancelled_inspection_is_not_a_later_inspection()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci1 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 30m);
        await SeedAsync(db, AppraisalTypes.Progressive, "06", ci1.Id, T0.AddDays(10),
            status: AppraisalStatus.Cancelled, summaryCurrentPct: 90m);

        var result = await StatusOf(scope, ci1.Id);

        Assert.Null(result!.LaterInspection);
        Assert.Null(result.BlockReason);
        Assert.Equal(30m, result.Construction.ProgressPct);
    }

    // ── open request ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_submitted_unfinished_request_on_the_tree_blocks_but_drafts_and_finished_ones_do_not()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci1 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 30m);

        // Not open: a draft (never submitted) ...
        await InsertRequestAsync(Guid.NewGuid(), ci1.Id, "06", null, "Draft", "REQ-DRAFT");
        // ... another purpose ...
        await InsertRequestAsync(Guid.NewGuid(), ci1.Id, "03", T0, "Submitted", "REQ-REAPPRAISAL");
        // ... a request whose appraisal already finished.
        var doneRequest = Guid.NewGuid();
        await InsertRequestAsync(doneRequest, ci1.Id, "11", T0, "Submitted", "REQ-DONE");
        await SeedAsync(db, AppraisalTypes.Progressive, "11", ci1.Id, null, status: AppraisalStatus.Cancelled, requestId: doneRequest);

        Assert.Null((await StatusOf(scope, ci1.Id))!.OpenInspectionRequest);

        // Open: a submitted request whose appraisal has not been created yet (the window after submit) ...
        var openRequest = Guid.NewGuid();
        await InsertRequestAsync(openRequest, ci1.Id, "11", T0.AddDays(1), "Submitted", "REQ-OPEN");

        var result = await StatusOf(scope, ci1.Id);

        Assert.Equal(openRequest, result!.OpenInspectionRequest!.RequestId);
        Assert.Equal("REQ-OPEN", result.OpenInspectionRequest.RequestNumber);
        Assert.Equal(ConstructionChainBlockReasons.OpenInspectionRequest, result.BlockReason);
    }

    [Fact]
    public async Task A_request_whose_appraisal_is_still_in_progress_is_open()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci1 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 30m);
        var requestId = Guid.NewGuid();
        await InsertRequestAsync(requestId, ci1.Id, "06", T0.AddDays(1), "Submitted", "REQ-INFLIGHT");
        await SeedAsync(db, AppraisalTypes.Progressive, "06", ci1.Id, null, requestId: requestId);

        var result = await StatusOf(scope, ci1.Id);

        Assert.Equal(requestId, result!.OpenInspectionRequest!.RequestId);
    }

    [Fact]
    public async Task The_request_being_validated_is_not_its_own_open_request_but_another_one_still_is()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var ci1 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, T0, summaryCurrentPct: 30m);
        var self = Guid.NewGuid();
        var other = Guid.NewGuid();
        await InsertRequestAsync(self, ci1.Id, "06", T0.AddDays(1), "Submitted", "REQ-SELF");

        // A replayed submit validates a request that is already submitted: alone on the chain, it must pass.
        Assert.Equal(self, (await StatusOf(scope, ci1.Id))!.OpenInspectionRequest!.RequestId);
        var replay = await StatusOf(scope, ci1.Id, excludeRequestId: self);
        Assert.Null(replay!.OpenInspectionRequest);
        Assert.Null(replay.BlockReason);

        // A different open request is still found.
        await InsertRequestAsync(other, ci1.Id, "11", T0.AddDays(2), "Submitted", "REQ-OTHER");
        Assert.Equal(other, (await StatusOf(scope, ci1.Id, excludeRequestId: self))!.OpenInspectionRequest!.RequestId);
    }

    // ── the HTTP contract the front end is built against ───────────────────────

    [Fact]
    public async Task The_endpoint_returns_the_agreed_json_shape_and_404_for_an_unknown_appraisal()
    {
        var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();
        Guid ci3Id;
        Guid ci4Id;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var ci3 = await SeedAsync(db, AppraisalTypes.Progressive, "06", null, new DateTime(2026, 7, 1),
                summaryCurrentPct: 60m, inspectionNumber: 3);
            var ci4 = await SeedAsync(db, AppraisalTypes.Progressive, "06", ci3.Id, new DateTime(2026, 8, 10, 14, 30, 0),
                summaryCurrentPct: 72.69m, inspectionNumber: 4);
            ci3Id = ci3.Id;
            ci4Id = ci4.Id;
        }

        var blocked = await client.GetAsync($"/appraisals/{ci3Id}/construction-chain-status", Ct);
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        var json = (await blocked.Content.ReadFromJsonAsync<JsonElement>(Ct));

        Assert.Equal(ci3Id, json.GetProperty("appraisalId").GetGuid());
        var construction = json.GetProperty("construction");
        Assert.Equal("InProgress", construction.GetProperty("status").GetString());
        Assert.Equal(72.69m, construction.GetProperty("progressPct").GetDecimal());
        Assert.Equal(ci4Id, construction.GetProperty("sourceAppraisalId").GetGuid());
        Assert.Equal(4, construction.GetProperty("sourceInspectionNumber").GetInt32());
        Assert.Equal("2026-08-10T14:30:00", construction.GetProperty("sourceCompletedAt").GetString()); // no zone suffix
        var selected = json.GetProperty("selected");
        Assert.Equal("InProgress", selected.GetProperty("status").GetString());
        Assert.Equal(60m, selected.GetProperty("progressPct").GetDecimal());   // CI#3's own, not CI#4's 72.69
        Assert.Equal(ci4Id, json.GetProperty("laterInspection").GetProperty("appraisalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("openInspectionRequest").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("latestAppraisal").ValueKind);
        Assert.Equal("LaterInspectionExists", json.GetProperty("blockReason").GetString());

        // A tree with no inspection at all: every key is still there, null where there is nothing.
        Guid bareId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            bareId = (await SeedAsync(db, AppraisalTypes.New, "01", null, T0)).Id;
        }
        var bare = await (await client.GetAsync($"/appraisals/{bareId}/construction-chain-status", Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("NoData", bare.GetProperty("construction").GetProperty("status").GetString());
        foreach (var key in new[] { "progressPct", "sourceAppraisalId", "sourceAppraisalNumber", "sourceInspectionNumber", "sourceCompletedAt" })
            Assert.Equal(JsonValueKind.Null, bare.GetProperty("construction").GetProperty(key).ValueKind);
        foreach (var key in new[] { "laterInspection", "openInspectionRequest", "latestAppraisal", "blockReason" })
            Assert.Equal(JsonValueKind.Null, bare.GetProperty(key).ValueKind);
        Assert.Equal("NoData", bare.GetProperty("selected").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, bare.GetProperty("selected").GetProperty("progressPct").ValueKind);

        // latestAppraisal.progressPct is pinned too: a latest appraisal with no inspection still writes the key.
        Guid originalId;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var original = await SeedAsync(db, AppraisalTypes.New, "01", null, T0);
            await SeedAsync(db, AppraisalTypes.ReAppraisal, "03", original.Id, T0.AddDays(5));
            originalId = original.Id;
        }
        var offer = await (await client.GetAsync($"/appraisals/{originalId}/construction-chain-status", Ct))
            .Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, offer.GetProperty("latestAppraisal").GetProperty("progressPct").ValueKind);

        var unknown = await client.GetAsync($"/appraisals/{Guid.NewGuid()}/construction-chain-status", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // ── vw_AppraisalConstructionProgress ────────────────────────────────────────

    private async Task<(decimal CurrentPct, decimal RawPct, bool UnderConstruction)> ViewRowAsync(Guid appraisalId)
    {
        await using var conn = new SqlConnection(fixture.ConnectionString);
        var row = await conn.QuerySingleAsync<(decimal Cur, decimal Raw, bool Under)>(new CommandDefinition("""
            SELECT CurrentProgressPct AS Cur, RawCurrentProgressPct AS Raw, IsUnderConstruction AS Under
            FROM appraisal.vw_AppraisalConstructionProgress WHERE AppraisalId = @appraisalId
            """, new { appraisalId }, cancellationToken: Ct));
        return (row.Cur, row.Raw, row.Under);
    }

    private static void AddInspection(AppraisalAggregate a, decimal totalValue, decimal currentPct)
    {
        var prop = a.AddLandProperty();
        prop.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        var inspection = ConstructionInspection.CreateSummary(Guid.Empty, totalValue, null, null, currentPct, null);
        prop.SetConstructionInspection(inspection);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(inspection, prop.Id);
    }

    [Fact]
    public async Task The_view_weights_buildings_by_value_and_averages_them_when_there_is_no_value_base()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var weighted = await SeedAsync(db, AppraisalTypes.New, "01", null, T0);
        AddInspection(weighted, 3_000_000m, 50m);
        AddInspection(weighted, 1_000_000m, 100m);

        var unweighted = await SeedAsync(db, AppraisalTypes.New, "01", null, T0);
        AddInspection(unweighted, 0m, 20m);
        AddInspection(unweighted, 0m, 60m);
        await db.SaveChangesAsync(Ct);

        Assert.Equal((62.5m, 62.5m, true), await ViewRowAsync(weighted.Id));   // (3M x 50 + 1M x 100) / 4M
        Assert.Equal((40m, 40m, true), await ViewRowAsync(unweighted.Id));     // plain average
    }

    [Theory]
    [InlineData(99.996, 100.00, true)]    // displays as 100.00 yet is not finished
    [InlineData(100, 100.00, false)]
    [InlineData(120, 100.00, false)]      // clamped for display; nothing validates the upper bound
    public async Task The_view_clamps_and_rounds_for_display_but_decides_completeness_on_the_raw_figure(
        decimal raw, decimal displayed, bool underConstruction)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var a = await SeedAsync(db, AppraisalTypes.New, "01", null, T0, summaryCurrentPct: raw);

        var (current, _, under) = await ViewRowAsync(a.Id);

        Assert.Equal(displayed, current);
        Assert.Equal(underConstruction, under);
    }

    // ── the backfill script ─────────────────────────────────────────────────────

    private const string ScriptName = "20261021120000_Backfill_ConstructionInspectionSummaryFigures.sql";

    private async Task RunBackfillAsync()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Database", "Migration", "Scripts", ScriptName)))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var sql = File.ReadAllText(Path.Combine(dir!.FullName, "Database", "Migration", "Scripts", ScriptName));

        await using var conn = new SqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(Ct);
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                     .Where(b => !string.IsNullOrWhiteSpace(b)))
            await conn.ExecuteAsync(new CommandDefinition(batch, cancellationToken: Ct));
    }

    private async Task<(decimal? PrevPct, decimal? PrevVal, decimal? CurPct, decimal? CurVal)> FiguresAsync(Guid inspectionId)
    {
        await using var conn = new SqlConnection(fixture.ConnectionString);
        var r = await conn.QuerySingleAsync<(decimal? A, decimal? B, decimal? C, decimal? D)>(new CommandDefinition("""
            SELECT SummaryPreviousProgressPct, SummaryPreviousValue, SummaryCurrentProgressPct, SummaryCurrentValue
            FROM appraisal.ConstructionInspections WHERE Id = @inspectionId
            """, new { inspectionId }, cancellationToken: Ct));
        return (r.A, r.B, r.C, r.D);
    }

    [Fact]
    public async Task The_backfill_reproduces_what_the_aggregate_writes_for_both_modes_and_is_idempotent()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var a = await SeedAsync(db, AppraisalTypes.New, "01", null, T0);
        var group = Guid.NewGuid();

        // Full detail: rows with awkward fractions.
        var detailProp = a.AddLandProperty();
        detailProp.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        detailProp.LandDetail.AddTitle(LandTitle.Create(detailProp.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        var detail = ConstructionInspection.CreateFullDetail(Guid.Empty, 1_234_567m, null);
        detail.AddWorkDetail(group, "foundation", 1, 33.3333m, 12.5m, 60.25m);
        detail.AddWorkDetail(group, "structure", 2, 33.3333m, 0m, 33.3333m);
        detail.AddWorkDetail(group, "finishing", 3, 33.3334m, 7m, 1.1111m);
        detail.ComputeAllValues();
        detailProp.SetConstructionInspection(detail);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(detail, detailProp.Id);

        // Summary mode, one with a half-baht boundary and one never keyed.
        var summaryProp = a.AddLandProperty();
        summaryProp.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        summaryProp.LandDetail.AddTitle(LandTitle.Create(summaryProp.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        var summary = ConstructionInspection.CreateSummary(Guid.Empty, 1_001m, null, 10m, 50m, null);
        summaryProp.SetConstructionInspection(summary);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(summary, summaryProp.Id);

        var unkeyedProp = a.AddLandProperty();
        unkeyedProp.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        unkeyedProp.LandDetail.AddTitle(LandTitle.Create(unkeyedProp.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        var unkeyed = ConstructionInspection.CreateSummary(Guid.Empty, 5_000m, null, null, null, null);
        unkeyedProp.SetConstructionInspection(unkeyed);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(unkeyed, unkeyedProp.Id);

        await db.SaveChangesAsync(Ct);

        // What the app wrote on save ...
        var written = new[] { detail.Id, summary.Id, unkeyed.Id };
        var expected = new List<(decimal?, decimal?, decimal?, decimal?)>();
        foreach (var id in written) expected.Add(await FiguresAsync(id));

        // ... then take it back to how rows looked before this release (nothing in full detail, values 0 in summary).
        await using (var conn = new SqlConnection(fixture.ConnectionString))
            await conn.ExecuteAsync(new CommandDefinition("""
                UPDATE appraisal.ConstructionInspections
                SET SummaryPreviousProgressPct = CASE WHEN IsFullDetail = 1 THEN NULL ELSE SummaryPreviousProgressPct END,
                    SummaryCurrentProgressPct  = CASE WHEN IsFullDetail = 1 THEN NULL ELSE SummaryCurrentProgressPct END,
                    SummaryPreviousValue = CASE WHEN IsFullDetail = 1 THEN NULL ELSE 0 END,
                    SummaryCurrentValue  = CASE WHEN IsFullDetail = 1 THEN NULL ELSE 0 END
                WHERE Id IN @ids
                """, new { ids = written }, cancellationToken: Ct));

        await RunBackfillAsync();
        for (var i = 0; i < written.Length; i++)
            Assert.Equal(expected[i], await FiguresAsync(written[i]));

        await RunBackfillAsync(); // idempotent
        for (var i = 0; i < written.Length; i++)
            Assert.Equal(expected[i], await FiguresAsync(written[i]));

        // Spot-check the numbers themselves, not just the agreement.
        Assert.Equal(501m, (await FiguresAsync(summary.Id)).CurVal);          // 1,001 x 50% = 500.5
        Assert.Null((await FiguresAsync(unkeyed.Id)).CurVal);
    }
}
