using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.CollateralMasters.Models;
using Collateral.Contracts.FileInterface;
using Collateral.Data;
using Dapper;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Which appraisals <c>collateral.vw_CollateralResultExport</c> picks up, and what collateral id and
/// Auto Update it gives them. Every test starts from an in-scope appraisal
/// (<see cref="CollateralResultSeed"/>) and changes only the thing it is about.
/// </summary>
[Collection("Integration")]
public class CollateralResultScopeTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>Seeds the request and a completed appraisal, and returns the appraisal id.</summary>
    private static async Task<Guid> SeedAsync(
        IServiceScope scope, string purpose = "03", DateTime? completedAt = null,
        string? book = null, string? collateralId = null, string? autoUpdate = null,
        string appraisalType = AppraisalTypes.ReAppraisal, Guid? prevAppraisalId = null,
        DateTime? valuationDate = null, decimal? appraisedValue = null,
        AppraisalStatus? status = null, bool deleted = false)
    {
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        var requestId = await CollateralResultSeed.InsertRequestAsync(factory, purpose, book, collateralId, autoUpdate);
        var appraisal = CollateralResultSeed.NewCompleted(
            requestId, completedAt ?? CollateralResultSeed.InScope, appraisalType, prevAppraisalId, status);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync();

        if (valuationDate is { } date)
        {
            var valuation = ValuationAnalysis.Create(appraisal.Id, "MarketComparison", date);
            if (appraisedValue is { } value)
                valuation.UpdateSummary("MarketComparison", date, value, null, null);
            db.ValuationAnalyses.Add(valuation);
            await db.SaveChangesAsync();
        }

        if (deleted)
            await Sql(scope).GetOpenConnection().ExecuteAsync(
                "UPDATE appraisal.Appraisals SET IsDeleted = 1 WHERE Id = @Id", new { appraisal.Id });

        return appraisal.Id;
    }

    /// <summary>
    /// Seeds a prior appraisal and an ACTIVE COLLATLINK row naming it, so a chain walk from its
    /// successor finds <paramref name="hostId"/>. Active means the newest file date in the table, so
    /// the row is dated far ahead and removed again by <see cref="RemoveLinkAsync"/>.
    /// </summary>
    private static async Task<Guid> SeedPriorWithLinkAsync(IServiceScope scope, string hostId)
    {
        var priorId = await SeedAsync(scope, purpose: "01", appraisalType: AppraisalTypes.New);
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var number = db.Appraisals.AsNoTracking().Single(a => a.Id == priorId).AppraisalNumber!;

        await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(
            """
            INSERT INTO collateral.HostCollateralLinks
                (Id, AppraisalNumber, HostCollateralId, IsRedeemed, UpdatedAt, LastSeenFileDate)
            VALUES (NEWID(), @number, @hostId, 0, SYSDATETIME(), '20991231')
            """,
            new { number, hostId });
        return priorId;
    }

    private static Task RemoveLinkAsync(IServiceScope scope, string hostId)
        => scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(
            "DELETE FROM collateral.HostCollateralLinks WHERE HostCollateralId = @hostId", new { hostId });

    private static async Task<IReadOnlyList<CollateralResultRow>> RowsAsync(IServiceScope scope)
        => await scope.ServiceProvider.GetRequiredService<ICollateralResultQuery>().GetUnsentRowsAsync();

    [Fact]
    public async Task PurposeNine_IsLeftOut()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, purpose: "09");

        Assert.DoesNotContain(await RowsAsync(scope), r => r.AppraisalId == id);
    }

    [Fact]
    public async Task CutoffIsInclusive_TheLastSecondBeforeItIsLeftOut()
    {
        using var scope = CreateScope();
        var before = await SeedAsync(scope, completedAt: new DateTime(2026, 10, 31, 23, 59, 59));
        var onCutoff = await SeedAsync(scope, completedAt: new DateTime(2026, 11, 1, 0, 0, 0));

        var rows = await RowsAsync(scope);

        Assert.DoesNotContain(rows, r => r.AppraisalId == before);
        Assert.Contains(rows, r => r.AppraisalId == onCutoff);
    }

    [Fact]
    public async Task AnyLedgerRow_LeavesTheAppraisalOut()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        Assert.Contains(await RowsAsync(scope), r => r.AppraisalId == id);

        // Even one sent with a blank collateral id counts as done.
        var collateralDb = scope.ServiceProvider.GetRequiredService<CollateralDbContext>();
        collateralDb.CollateralResultLogs.Add(new CollateralResultLog(
            id, "CRS-LEDGER", collateralId: "", sentAt: DateTime.Now, fileName: "test.txt"));
        await collateralDb.SaveChangesAsync();

        Assert.DoesNotContain(await RowsAsync(scope), r => r.AppraisalId == id);
    }

    [Fact]
    public async Task InitiatedRequest_SendsItsOwnCollateralIdAndTheFrozenFlag()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, book: "69900001", collateralId: "60099001", autoUpdate: "Y");

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal("60099001", row.CollateralId);
        Assert.Equal("Y", row.AutoUpdate);
    }

    [Fact]
    public async Task InitiatedRequest_WithNoFlagCaptured_SendsN()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, book: "69900002", collateralId: "60099002", autoUpdate: null);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal("60099002", row.CollateralId);
        Assert.Equal("N", row.AutoUpdate);
    }

    [Fact]
    public async Task ManualRequest_SendsN()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        // No COLLATLINK row names this appraisal, so the walk finds nothing: blank id, 'N'.
        Assert.Equal(string.Empty, row.CollateralId);
        Assert.Equal("N", row.AutoUpdate);
    }

    [Fact]
    public async Task InitiatedRequest_KeepsItsOwnIdEvenWhenThePriorAppraisalHasAnActiveLink()
    {
        using var scope = CreateScope();
        var hostId = $"WALK-{Guid.NewGuid():N}"[..14];
        try
        {
            var priorId = await SeedPriorWithLinkAsync(scope, hostId);
            var id = await SeedAsync(scope, book: "69900003", collateralId: "60099003", autoUpdate: "Y", prevAppraisalId: priorId);

            var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

            Assert.Equal("60099003", row.CollateralId);
            Assert.Equal("Y", row.AutoUpdate);
        }
        finally
        {
            await RemoveLinkAsync(scope, hostId);
        }
    }

    [Fact]
    public async Task InitiatedRequest_WithNoCollateralId_FallsBackToTheWalkAndSendsN()
    {
        using var scope = CreateScope();
        var hostId = $"WALK-{Guid.NewGuid():N}"[..14];
        try
        {
            var priorId = await SeedPriorWithLinkAsync(scope, hostId);
            var id = await SeedAsync(scope, book: "69900004", collateralId: null, autoUpdate: "Y", prevAppraisalId: priorId);

            var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

            Assert.Equal(hostId, row.CollateralId);
            Assert.Equal("N", row.AutoUpdate);
        }
        finally
        {
            await RemoveLinkAsync(scope, hostId);
        }
    }

    [Fact]
    public async Task InitiatedRequest_ThatIsDeleted_IsLeftOut()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, book: "69900005", collateralId: "60099005", autoUpdate: "Y");
        Assert.Contains(await RowsAsync(scope), r => r.AppraisalId == id);

        await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(
            "UPDATE r SET IsDeleted = 1 FROM request.Requests r JOIN appraisal.Appraisals a ON a.RequestId = r.Id WHERE a.Id = @id",
            new { id });

        Assert.DoesNotContain(await RowsAsync(scope), r => r.AppraisalId == id);
    }

    [Fact]
    public async Task PurposeThree_OfAnAppraisalThatIsNotAReAppraisal_IsLeftOut()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, appraisalType: AppraisalTypes.New);

        Assert.DoesNotContain(await RowsAsync(scope), r => r.AppraisalId == id);
    }

    [Fact]
    public async Task Rows_AreOrderedByValuationDate_NotByInsertionOrCompletion()
    {
        using var scope = CreateScope();
        // Inserted and completed first, but valued last.
        var late = await SeedAsync(scope, completedAt: new DateTime(2026, 11, 2), valuationDate: new DateTime(2026, 12, 20));
        var early = await SeedAsync(scope, completedAt: new DateTime(2026, 11, 9), valuationDate: new DateTime(2026, 12, 1));

        var rows = await RowsAsync(scope);

        var mine = rows.Where(r => r.AppraisalId == late || r.AppraisalId == early).Select(r => r.AppraisalId).ToList();
        Assert.Equal([early, late], mine);
        var dates = rows.Select(r => r.CurrentAppraisalDate).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
    }

    // ── Helpers for the walk, block and property-mix cases ─────────────────────────────────────

    private static ISqlConnectionFactory Sql(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

    /// <summary>A prior appraisal (not itself in scope) and its number, the key a COLLATLINK row names.</summary>
    private static async Task<(Guid Id, string Number)> SeedPriorAsync(IServiceScope scope)
    {
        var id = await SeedAsync(scope, purpose: "01", appraisalType: AppraisalTypes.New);
        var number = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>()
            .Appraisals.AsNoTracking().Single(a => a.Id == id).AppraisalNumber!;
        return (id, number);
    }

    /// <summary>An ACTIVE COLLATLINK row (dated ahead of everything else); removed by <see cref="RemoveLinksAsync"/>.</summary>
    private static Task InsertLinkAsync(IServiceScope scope, string appraisalNumber, string hostId, string? collateralName = null)
        => Sql(scope).GetOpenConnection().ExecuteAsync(
            """
            INSERT INTO collateral.HostCollateralLinks
                (Id, AppraisalNumber, HostCollateralId, IsRedeemed, UpdatedAt, LastSeenFileDate, CollateralName)
            VALUES (NEWID(), @appraisalNumber, @hostId, 0, SYSDATETIME(), '20991231', @collateralName)
            """,
            new { appraisalNumber, hostId, collateralName });

    private static Task RemoveLinksAsync(IServiceScope scope, string appraisalNumber)
        => Sql(scope).GetOpenConnection().ExecuteAsync(
            "DELETE FROM collateral.HostCollateralLinks WHERE AppraisalNumber = @appraisalNumber", new { appraisalNumber });

    /// <summary>A condo project on the appraisal with one unit per key, each priced.</summary>
    private static async Task AddProjectUnitsAsync(IServiceScope scope, Guid appraisalId, params (string Key, decimal Value)[] units)
    {
        var connection = Sql(scope).GetOpenConnection();
        var projectId = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            "INSERT INTO appraisal.Projects (Id, AppraisalId, ProjectType) VALUES (@projectId, @appraisalId, 'U')",
            new { projectId, appraisalId });

        var sequence = 1;
        foreach (var (key, value) in units)
        {
            var unitId = Guid.CreateVersion7();
            await connection.ExecuteAsync(
                """
                INSERT INTO appraisal.ProjectUnits (Id, ProjectId, UploadBatchId, SequenceNumber, CondoRegistrationNumber)
                VALUES (@unitId, @projectId, NEWID(), @sequence, @key);
                INSERT INTO appraisal.ProjectUnitPrices (Id, ProjectUnitId, TotalAppraisalValueRounded)
                VALUES (NEWID(), @unitId, @value);
                """,
                new { unitId, projectId, sequence = sequence++, key, value });
        }
    }

    /// <summary>Gives the units of the appraisal's project their land areas (square wa), in sequence order.</summary>
    private static async Task SetUnitLandAreasAsync(IServiceScope scope, Guid appraisalId, params decimal[] areas)
    {
        var connection = Sql(scope).GetOpenConnection();
        for (var i = 0; i < areas.Length; i++)
            await connection.ExecuteAsync(
                """
                UPDATE u SET LandArea = @area
                FROM appraisal.ProjectUnits u JOIN appraisal.Projects pr ON pr.Id = u.ProjectId
                WHERE pr.AppraisalId = @appraisalId AND u.SequenceNumber = @sequence
                """,
                new { area = areas[i], appraisalId, sequence = i + 1 });
    }

    /// <summary>A property of the given type; a building or condo detail row carries the age and area.</summary>
    private static async Task AddPropertyAsync(
        IServiceScope scope, Guid appraisalId, int sequence, string propertyType, int? age = null, decimal? area = null)
    {
        var connection = Sql(scope).GetOpenConnection();
        var propertyId = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            "INSERT INTO appraisal.AppraisalProperties (Id, AppraisalId, SequenceNumber, PropertyType) VALUES (@propertyId, @appraisalId, @sequence, @propertyType)",
            new { propertyId, appraisalId, sequence, propertyType });

        if (propertyType is "B" or "LB" or "LSB" or "LS")
            await connection.ExecuteAsync(
                "INSERT INTO appraisal.BuildingAppraisalDetails (AppraisalPropertyId, BuildingAge, TotalBuildingArea) VALUES (@propertyId, @age, @area)",
                new { propertyId, age, area });
        else if (propertyType is "U" or "LSU")
            await connection.ExecuteAsync(
                "INSERT INTO appraisal.CondoAppraisalDetails (AppraisalPropertyId, OwnerName, BuildingAge, UsableArea) VALUES (@propertyId, N'owner', @age, @area)",
                new { propertyId, age, area });
    }

    // ── Chain walk, block project, property mix ────────────────────────────────────────────────

    [Fact]
    public async Task ManualBlockAppraisal_SendsOneRowPerMatchedUnit_EachN()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        var host1 = $"BLK1-{Guid.NewGuid():N}"[..14];
        var host2 = $"BLK2-{Guid.NewGuid():N}"[..14];
        try
        {
            // Two collaterals of the prior book, each naming its unit as "CONDO.<key> <deed>".
            await InsertLinkAsync(scope, number, host1, $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, host2, $"CONDO.{key2} deed-2");

            var id = await SeedAsync(scope, prevAppraisalId: priorId);
            await AddProjectUnitsAsync(scope, id, (key1, 1_000_000m), (key2, 2_000_000m));

            var rows = (await RowsAsync(scope)).Where(r => r.AppraisalId == id).OrderBy(r => r.AppraisalValue).ToList();

            Assert.Equal(2, rows.Count);
            Assert.Equal([host1, host2], rows.Select(r => r.CollateralId));
            Assert.Equal([1_000_000m, 2_000_000m], rows.Select(r => r.AppraisalValue));
            Assert.All(rows, r => Assert.Equal("N", r.AutoUpdate));
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    [Fact]
    public async Task InitiatedAppraisalThatIsAlsoABlock_SendsOneRowWithTheInitiateIdAndFlag()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        var host1 = $"IB1-{Guid.NewGuid():N}"[..14];
        var host2 = $"IB2-{Guid.NewGuid():N}"[..14];
        try
        {
            // Two units that WOULD match on the prior book's collaterals, as in the manual block case.
            await InsertLinkAsync(scope, number, host1, $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, host2, $"CONDO.{key2} deed-2");

            var id = await SeedAsync(scope, book: "69900006", collateralId: "60099006", autoUpdate: "Y", prevAppraisalId: priorId);
            await AddProjectUnitsAsync(scope, id, (key1, 1_000_000m), (key2, 2_000_000m));

            var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

            Assert.Equal("60099006", row.CollateralId);
            Assert.Equal("Y", row.AutoUpdate);
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    /// <summary>
    /// The real send path with nothing hand-inserted: the query's rows go to the ledger, which writes
    /// them under the unique (AppraisalId, CollateralId) index — so this also checks that index against
    /// the view's grain (one row per appraisal, one per matched unit for a block). The ledger is what
    /// <c>CollateralResultExportJob</c> calls after writing the file; the job itself needs an active
    /// file-interface config and a file sink, and would mark every other pending row in the shared
    /// database, so only the query and the ledger are run.
    /// </summary>
    [Fact]
    public async Task SendingTheRows_FillsTheLedgerAndMakesTheNextRunReturnNone()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        var host1 = $"SND1-{Guid.NewGuid():N}"[..14];
        var host2 = $"SND2-{Guid.NewGuid():N}"[..14];
        try
        {
            await InsertLinkAsync(scope, number, host1, $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, host2, $"CONDO.{key2} deed-2");
            var ordinary = await SeedAsync(scope);
            var block = await SeedAsync(scope, prevAppraisalId: priorId);
            await AddProjectUnitsAsync(scope, block, (key1, 1_000_000m), (key2, 2_000_000m));
            var mine = new[] { ordinary, block };

            var rows = (await RowsAsync(scope)).Where(r => mine.Contains(r.AppraisalId)).ToList();
            Assert.Equal(3, rows.Count);

            await scope.ServiceProvider.GetRequiredService<ICollateralResultLedger>()
                .MarkSentAsync(rows, "COLLATERAL_RESULT_test.txt", DateTime.Now);

            var ledger = scope.ServiceProvider.GetRequiredService<CollateralDbContext>()
                .CollateralResultLogs.AsNoTracking().Where(l => mine.Contains(l.AppraisalId)).ToList();
            Assert.Equal(3, ledger.Count);
            Assert.DoesNotContain(await RowsAsync(scope), r => mine.Contains(r.AppraisalId));
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    // Pins what the view does today for an Initiated appraisal that is also a block (settled: leave as is): one row,
    // the Initiate id and flag, the WHOLE appraisal's value, no per-row land area, and the project's units summed
    // into the total.
    [Fact]
    public async Task InitiatedBlockAppraisal_PinsTheCurrentValueAndLandAreaShape()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        var host1 = $"PN1-{Guid.NewGuid():N}"[..14];
        var host2 = $"PN2-{Guid.NewGuid():N}"[..14];
        try
        {
            await InsertLinkAsync(scope, number, host1, $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, host2, $"CONDO.{key2} deed-2");
            var id = await SeedAsync(scope, book: "69900007", collateralId: "60099007", autoUpdate: "Y",
                prevAppraisalId: priorId, valuationDate: new DateTime(2026, 12, 1), appraisedValue: 9_000_000m);
            await AddProjectUnitsAsync(scope, id, (key1, 1_000_000m), (key2, 2_000_000m));
            await SetUnitLandAreasAsync(scope, id, 400m, 150m);

            var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

            Assert.Equal("60099007", row.CollateralId);
            Assert.Equal("Y", row.AutoUpdate);
            Assert.Equal(9_000_000m, row.AppraisalValue);          // the whole appraisal, not a unit's
            Assert.Null(row.LandAreaRai);                          // no per-row parts without land titles
            Assert.Null(row.LandAreaNgan);
            Assert.Null(row.LandAreaSquareWa);
            Assert.Equal(550m, row.LandAreaTotalSqWa);             // the units' areas summed
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    [Theory]
    [InlineData("InProgress", false)]
    [InlineData("Completed", true)]    // completed but soft-deleted
    public async Task AnAppraisalThatIsNotCompletedOrIsDeleted_IsLeftOut(string status, bool deleted)
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope, status: AppraisalStatus.FromString(status), deleted: deleted);

        Assert.DoesNotContain(await RowsAsync(scope), r => r.AppraisalId == id);
    }

    // Same CurrentAppraisalDate: ordered by AppraisalReportNumber, then CollateralId. The rows come out of the query
    // in that order — the assertions do not sort them.
    [Fact]
    public async Task RowsOnTheSameDate_AreOrderedByCollateralIdWithinOneAppraisal()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        var tag = Guid.NewGuid().ToString("N")[..6];
        var hostB = $"TB-B-{tag}";
        var hostA = $"TB-A-{tag}";
        try
        {
            // Inserted B first, so insertion order is the opposite of the expected output order.
            await InsertLinkAsync(scope, number, hostB, $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, hostA, $"CONDO.{key2} deed-2");
            var id = await SeedAsync(scope, prevAppraisalId: priorId, valuationDate: new DateTime(2026, 12, 2));
            await AddProjectUnitsAsync(scope, id, (key1, 1_000_000m), (key2, 2_000_000m));

            var rows = (await RowsAsync(scope)).Where(r => r.AppraisalId == id).ToList();

            Assert.Equal([hostA, hostB], rows.Select(r => r.CollateralId));
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    // The file zero-fills the id, so a shorter id sorts before a longer one: '99000001' before '100000001'.
    [Fact]
    public async Task CollateralIdsOfDifferentLengths_AreOrderedNumerically()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var key1 = $"K{Guid.NewGuid():N}"[..8];
        var key2 = $"K{Guid.NewGuid():N}"[..8];
        try
        {
            // The longer id is inserted first and sorts first as text ('1' < '9'), so only LEN fixes the order.
            await InsertLinkAsync(scope, number, "100000001", $"CONDO.{key1} deed-1");
            await InsertLinkAsync(scope, number, "99000001", $"CONDO.{key2} deed-2");
            var id = await SeedAsync(scope, prevAppraisalId: priorId, valuationDate: new DateTime(2026, 12, 4));
            await AddProjectUnitsAsync(scope, id, (key1, 1_000_000m), (key2, 2_000_000m));

            var rows = (await RowsAsync(scope)).Where(r => r.AppraisalId == id).ToList();

            Assert.Equal(["99000001", "100000001"], rows.Select(r => r.CollateralId));
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    [Fact]
    public async Task RowsOnTheSameDate_AreOrderedByAppraisalReportNumber()
    {
        using var scope = CreateScope();
        var date = new DateTime(2026, 12, 3);
        var first = await SeedAsync(scope, valuationDate: date);
        var second = await SeedAsync(scope, valuationDate: date);
        var numbers = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>().Appraisals.AsNoTracking()
            .Where(a => a.Id == first || a.Id == second).ToDictionary(a => a.Id, a => a.AppraisalNumber!);

        var rows = (await RowsAsync(scope)).Where(r => r.AppraisalId == first || r.AppraisalId == second).ToList();

        // Expected order from the numbers themselves (the test's own sort, not the rows').
        Assert.Equal(
            numbers.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key),
            rows.Select(r => r.AppraisalId));
    }

    [Fact]
    public async Task ManualAppraisal_WhoseNearestAncestorMatchesTwoCollaterals_SendsOneBlankRowN()
    {
        using var scope = CreateScope();
        var (priorId, number) = await SeedPriorAsync(scope);
        var host1 = $"MM1-{Guid.NewGuid():N}"[..14];
        var host2 = $"MM2-{Guid.NewGuid():N}"[..14];
        try
        {
            await InsertLinkAsync(scope, number, host1);
            await InsertLinkAsync(scope, number, host2);
            var id = await SeedAsync(scope, prevAppraisalId: priorId);

            var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

            Assert.Equal(string.Empty, row.CollateralId);
            Assert.Equal("N", row.AutoUpdate);
        }
        finally
        {
            await RemoveLinksAsync(scope, number);
        }
    }

    [Fact]
    public async Task ABuildingTakesItsAgeAndAreaBeforeACondoOnTheSameAppraisal()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        await AddPropertyAsync(scope, id, 1, "U", age: 5, area: 40m);
        await AddPropertyAsync(scope, id, 2, "B", age: 20, area: 100m);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal(20, row.BuildingAge);
        Assert.Equal(100m, row.AreaUtilization);
    }

    // Leasehold types read the same detail tables as their freehold twins (CondoAppraisalDetails for LSU,
    // BuildingAppraisalDetails for LSB / LS): the age and area must come out, not zero.
    [Fact]
    public async Task AnAppraisalWithTwoBuildings_TakesTheOldestAgeAndTheSumOfTheAreas()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        await AddPropertyAsync(scope, id, 1, "LB", age: 20, area: 100m);
        await AddPropertyAsync(scope, id, 2, "LB", age: 5, area: 40m);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal(20, row.BuildingAge);
        Assert.Equal(140m, row.AreaUtilization);
    }

    [Fact]
    public async Task ALeaseholdCondo_TakesItsAgeAndAreaFromTheCondoDetail()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        await AddPropertyAsync(scope, id, 1, "LSU", age: 7, area: 42.5m);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal(7, row.BuildingAge);
        Assert.Equal(42.5m, row.AreaUtilization);
    }

    [Fact]
    public async Task ALeaseholdBuilding_TakesItsAgeAndAreaFromTheBuildingDetail()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        await AddPropertyAsync(scope, id, 1, "LSB", age: 12, area: 88m);

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Equal(12, row.BuildingAge);
        Assert.Equal(88m, row.AreaUtilization);
    }

    [Fact]
    public async Task ALandOnlyAppraisal_SendsNoBuildingAgeOrArea()
    {
        using var scope = CreateScope();
        var id = await SeedAsync(scope);
        await AddPropertyAsync(scope, id, 1, "L");

        var row = Assert.Single(await RowsAsync(scope), r => r.AppraisalId == id);

        Assert.Null(row.BuildingAge);
        Assert.Null(row.AreaUtilization);
    }
}
