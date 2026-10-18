using Collateral.CollateralMasters.Reappraisal;
using Collateral.Contracts.Reappraisal;
using Collateral.Data;
using Dapper;
using Integration.Contracts.Reappraisal;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Time;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// AS400's Auto Update flag (COLLATREV position 650) on the candidate: stored when the book arrives,
/// refreshed while the book is still Pending, and left alone once the book has been reviewed.
///
/// Files are dated 2020 so these books never become the "latest file" other tests read.
/// </summary>
[Collection("Integration")]
public class ReappraisalIngestorAutoUpdateTests(IntegrationTestFixture fixture)
{
    private static readonly DateOnly FileDate = new(2020, 1, 31);

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>Unique per test: the candidate table is shared and never cleaned.</summary>
    private static (string CollateralId, string Book) NewBook()
        => ($"7{Random.Shared.NextInt64(100_000_000_000, 999_999_999_999)}", $"7{Random.Shared.Next(1_000_000, 9_999_999)}");

    private static ParsedDetailRecord Detail(
        string collateralId, string book, string rowHash, string? autoUpdate, bool has650 = true,
        string? collateralName = null) => new(
        RowHash: rowHash, ReviewType: "1", ReviewDate: new DateOnly(2026, 1, 1),
        CollateralId: collateralId, SurveyNumber: book, CollateralCode: "11A", CollateralCategory: "RE",
        CollateralName: collateralName, CollateralAddress: null, CifNumber: "1000000000000000001", CifName: null,
        AoCode: null, AoName: null, TitleNumber: null, CurrentValue: null, ValuationDate: null,
        InternalExternal: null, BusinessSize: null, BusinessSizeDesc: null, MortgageAmount: null,
        PastDueDay: null, ApplicationNumber: null, FacilityCode: null, FacilitySequence: null,
        CpNumber: null, CarCode: null, FacilityLimit: null, FlagLessAge4Y: null, FlagGreaterAge4Y: null,
        CountAgeingDate: null, CollateralDescription: null, ExternalValuerName: null,
        InternalValuerName: null, SllOver100M: null, SllDescription: null, Stage: null,
        IBGRetail: null, Group: null, EffectiveDateAppraisal: null, AutoUpdate: autoUpdate,
        HasAutoUpdatePosition: has650);

    private async Task IngestAsync(DateOnly fileDate, params ParsedDetailRecord[] details)
    {
        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReappraisalIngestor>().IngestAsync(
            $"AS400_COLLATREV_{fileDate:yyyyMMdd}.txt", fileDate,
            new ParsedReappraisalFile(fileDate, [.. details]));
    }

    private async Task<ReappraisalCandidate> LoadAsync(string collateralId)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CollateralDbContext>()
            .ReappraisalCandidates.AsNoTracking().SingleAsync(c => c.CollateralId == collateralId);
    }

    [Fact]
    public async Task ANewBook_StoresTheFlagFromTheFile()
    {
        var (collateralId, book) = NewBook();

        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", "Y"));

        Assert.Equal("Y", (await LoadAsync(collateralId)).AutoUpdate);
    }

    [Fact]
    public async Task APendingBookWithTheSameRowHash_PicksUpAFlagItDidNotHave()
    {
        var (collateralId, book) = NewBook();
        // Ingested before the flag was read: no flag stored.
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-same", null));
        Assert.Null((await LoadAsync(collateralId)).AutoUpdate);

        // The same line again (same hash), now carrying the flag.
        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-same", "Y"));

        Assert.Equal("Y", (await LoadAsync(collateralId)).AutoUpdate);
    }

    [Fact]
    public async Task APendingBook_FollowsTheFlagWhenAnotherFileChangesIt()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", "Y"));
        Assert.Equal("Y", (await LoadAsync(collateralId)).AutoUpdate);

        // The next file's line differs (new hash) and carries 'N'.
        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-2", "N"));

        Assert.Equal("N", (await LoadAsync(collateralId)).AutoUpdate);
    }

    // One file holding the book several times, in order. "649" is a row with no position 650; "650Y",
    // "650N" and "650-" are 650 rows with that flag (- = blank or unrecognised, both parse to null).
    // The last copy wins; a 649 last copy takes the flag of the latest copy that HAS a position 650,
    // null included, because a 650 row's blank is AS400's word.
    [Theory]
    [InlineData("650Y,649", "Y")]
    [InlineData("650Y,650-", null)]
    [InlineData("650Y,650N", "N")]
    [InlineData("650Y,650-,649", null)]
    [InlineData("650N,650Y,649", "Y")]
    [InlineData("649,650Y,649", "Y")]
    public async Task ABookListedSeveralTimesInOneFile_FallsBackOnlyFromACopyWithPosition650(
        string copies, string? expected)
    {
        var (collateralId, book) = NewBook();
        var rows = copies.Split(',').Select((c, i) => c == "649"
            ? Detail(collateralId, book, $"hash-{i}", null, has650: false)
            : Detail(collateralId, book, $"hash-{i}", c[3] is 'Y' or 'N' ? c[3].ToString() : null)).ToArray();

        await IngestAsync(FileDate, rows);

        Assert.Equal(expected, (await LoadAsync(collateralId)).AutoUpdate);
    }

    // The latest file is AS400's current word: a real 649 row (no position 650), or a 650 row with a blank flag,
    // clears a stored flag.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALaterFileWithNoFlag_ClearsAStoredFlag(bool has650)
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", "Y"));

        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-2", null, has650));

        Assert.Null((await LoadAsync(collateralId)).AutoUpdate);
    }

    [Fact]
    public async Task ReIngestingTheSameHashWithTheSameFlag_LeavesTheBookUntouched()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-same", "Y", collateralName: "first"));

        // Same hash and flag, but a different name: the row is skipped, not refreshed.
        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-same", "Y", collateralName: "second"));

        using var scope = CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<CollateralDbContext>()
            .ReappraisalCandidates.AsNoTracking().Where(c => c.CollateralId == collateralId).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("first", row.CollateralName);
        Assert.Equal("Y", row.AutoUpdate);
    }

    [Fact]
    public async Task ReIngestingAnIdenticalFileWhoseBookCollapsedInFile_SkipsTheRowAsUnchanged()
    {
        var (collateralId, book) = NewBook();
        // 650 'Y' then 649 in one file: the row kept is the 649 copy, carrying the 'Y'.
        await IngestAsync(FileDate,
            Detail(collateralId, book, "hash-a", "Y"),
            Detail(collateralId, book, "hash-b", null, has650: false, collateralName: "first"));
        Assert.Equal("Y", (await LoadAsync(collateralId)).AutoUpdate);

        await IngestAsync(FileDate.AddDays(1),
            Detail(collateralId, book, "hash-a", "Y"),
            Detail(collateralId, book, "hash-b", null, has650: false, collateralName: "second"));

        // Same collapsed hash and same flag: skipped, not refreshed.
        var row = await LoadAsync(collateralId);
        Assert.Equal("first", row.CollateralName);
        Assert.Equal("Y", row.AutoUpdate);
    }

    [Fact]
    public async Task ADeletedBook_IsRefreshedWithTheNewFlagAndStaysDeleted()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", "Y"));
        using (var scope = CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(
                "UPDATE collateral.ReappraisalCandidates SET Status = 'Deleted' WHERE CollateralId = @collateralId",
                new { collateralId });

        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-2", "N"));

        var candidate = await LoadAsync(collateralId);
        Assert.Equal("N", candidate.AutoUpdate);
        Assert.Equal(ReappraisalCandidateStatus.Deleted, candidate.Status);
    }

    private sealed class CapturingLogger : ILogger<ReappraisalIngestor>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private async Task<List<(LogLevel Level, string Message)>> IngestWithLoggerAsync(
        DateOnly fileDate, int unrecognised, params ParsedDetailRecord[] details)
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var logger = new CapturingLogger();
        var ingestor = new ReappraisalIngestor(
            sp.GetRequiredService<CollateralDbContext>(), sp.GetRequiredService<ISqlConnectionFactory>(),
            sp.GetRequiredService<IDateTimeProvider>(), logger);
        await ingestor.IngestAsync($"AS400_COLLATREV_{fileDate:yyyyMMdd}.txt", fileDate,
            new ParsedReappraisalFile(fileDate, [.. details], unrecognised));
        return logger.Entries;
    }

    [Fact]
    public async Task AFileWithUnrecognisedFlags_LogsOneWarningWithTheCount()
    {
        var (collateralId, book) = NewBook();

        var log = await IngestWithLoggerAsync(FileDate, unrecognised: 3, Detail(collateralId, book, "hash-1", null));

        var warning = Assert.Single(log, e => e.Level == LogLevel.Warning && e.Message.Contains("not Y, N or blank"));
        Assert.Contains("3 row(s)", warning.Message);
    }

    [Fact]
    public async Task ADuplicateBookWithConflictingFlags_LogsTheDifferenceInTheDuplicateWarning()
    {
        var (collateralId, book) = NewBook();

        var log = await IngestWithLoggerAsync(FileDate, unrecognised: 0,
            Detail(collateralId, book, "hash-a", "Y"), Detail(collateralId, book, "hash-b", "N"));

        var warning = Assert.Single(log, e => e.Level == LogLevel.Warning && e.Message.Contains("duplicate book row"));
        Assert.Contains("1 book(s) had copies with different Auto Update flags", warning.Message);
    }

    // A 649 copy carries no flag of its own, so [650 'Y', 649] is no conflict; a 650 copy with a blank flag is AS400's
    // word and does conflict with a 650 'Y'.
    [Theory]
    [InlineData(true, "0")]    // 650 'Y' then a 649 copy
    [InlineData(false, "1")]   // 650 'Y' then a 650 copy with a blank flag
    public async Task TheConflictCountOnlyCountsCopiesThatHavePosition650(bool lastIs649, string expectedConflicts)
    {
        var (collateralId, book) = NewBook();

        var log = await IngestWithLoggerAsync(FileDate, unrecognised: 0,
            Detail(collateralId, book, "hash-a", "Y"),
            Detail(collateralId, book, "hash-b", null, has650: !lastIs649));

        var warning = Assert.Single(log, e => e.Level == LogLevel.Warning && e.Message.Contains("duplicate book row"));
        Assert.Contains($"{expectedConflicts} book(s) had copies with different Auto Update flags", warning.Message);
    }

    [Fact]
    public async Task AnOlderFile_CannotOverwriteTheStoredFlag()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate.AddDays(5), Detail(collateralId, book, "hash-new", "Y"));

        // A backlog file dated before the one already seen, with a different hash and flag.
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-old", "N"));

        var row = await LoadAsync(collateralId);
        Assert.Equal("Y", row.AutoUpdate);
        Assert.Equal("hash-new", row.RowHash);
    }

    [Fact]
    public async Task AReopenedBook_IsRefreshedWithTheNewFlag()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", "Y"));
        using (var scope = CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(
                "UPDATE collateral.ReappraisalCandidates SET Status = 'Consumed' WHERE CollateralId = @collateralId",
                new { collateralId });

        // Nothing is reviewing the book any more (no request, no live reappraisal): it goes back to Pending
        // and takes this file's values.
        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-2", "N"));

        var candidate = await LoadAsync(collateralId);
        Assert.Equal(ReappraisalCandidateStatus.Pending, candidate.Status);
        Assert.Equal("N", candidate.AutoUpdate);
    }

    [Fact]
    public async Task AConsumedBook_IsNotRefreshed()
    {
        var (collateralId, book) = NewBook();
        await IngestAsync(FileDate, Detail(collateralId, book, "hash-1", null));

        using (var scope = CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            await factory.GetOpenConnection().ExecuteAsync(
                "UPDATE collateral.ReappraisalCandidates SET Status = 'Consumed' WHERE CollateralId = @collateralId",
                new { collateralId });
            // A request for the book still waiting: otherwise nothing is reviewing it and the ingestor
            // would reopen (and refresh) it.
            await CollateralResultSeed.InsertRequestAsync(factory, "03", book, collateralId, null);
        }

        await IngestAsync(FileDate.AddDays(1), Detail(collateralId, book, "hash-2", "Y"));

        var candidate = await LoadAsync(collateralId);
        Assert.Null(candidate.AutoUpdate);
        Assert.Equal(ReappraisalCandidateStatus.Consumed, candidate.Status);
    }
}
