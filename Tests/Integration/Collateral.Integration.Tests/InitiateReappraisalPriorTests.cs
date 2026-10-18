using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Collateral.Application.Features.Reappraisal.InitiateReappraisal;
using Collateral.CollateralMasters.Reappraisal;
using Collateral.Contracts.Reappraisal;
using Collateral.Data;
using Dapper;
using Integration.Contracts.Reappraisal;
using Integration.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Request.Contracts.Requests.Dtos;
using Shared.Data;

namespace Integration.Collateral.Integration.Tests;

/// <summary>
/// Initiate binds a book's prior appraisal only when it is COMPLETED (the request's prior is locked from then
/// on). A book that has an appraisal in CAS but none completed is skipped as PriorNotCompleted — it must not
/// fall into the legacy-book path, which is for numbers CAS does not know at all.
/// </summary>
[Collection("Integration")]
public class InitiateReappraisalPriorTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>A candidate for <paramref name="book"/>, made due by dating it with the newest file in the table.</summary>
    private async Task<Guid> SeedCandidateAsync(
        string book, string? collateralId = null, string? autoUpdate = null, bool due = true)
    {
        collateralId ??= $"8{Random.Shared.NextInt64(100_000_000_000, 999_999_999_999)}";
        var fileDate = new DateOnly(2020, 1, 31);
        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<IReappraisalIngestor>().IngestAsync(
            $"AS400_COLLATREV_{fileDate:yyyyMMdd}.txt", fileDate,
            new ParsedReappraisalFile(fileDate, [new ParsedDetailRecord(
                "hash", "1", new DateOnly(2026, 1, 1), collateralId, book, "11A", "RE", null, null,
                "1000000000000000001", null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                null, autoUpdate, autoUpdate is not null)]));

        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await factory.GetOpenConnection().ExecuteAsync(
            """
            UPDATE collateral.ReappraisalCandidates
            SET LastSeenFileDate = DATEADD(DAY, @offset, (SELECT MAX(LastSeenFileDate) FROM collateral.ReappraisalCandidates))
            WHERE CollateralId = @collateralId AND NormalizedSurveyNumber = @book
            """,
            new { collateralId, book, offset = due ? 0 : -1 });
        return await scope.ServiceProvider.GetRequiredService<CollateralDbContext>()
            .ReappraisalCandidates.AsNoTracking()
            .Where(c => c.CollateralId == collateralId && c.NormalizedSurveyNumber == book)
            .Select(c => c.Id).SingleAsync();
    }

    private async Task<Guid> SeedAppraisalAsync(AppraisalStatus status, bool deleted = false)
    {
        using var scope = CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var appraisal = CollateralResultSeed.NewCompleted(
            await CollateralResultSeed.InsertRequestAsync(factory, "01"), new DateTime(2026, 6, 1),
            AppraisalTypes.New, status: status);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync();
        if (deleted)
            await factory.GetOpenConnection().ExecuteAsync(
                "UPDATE appraisal.Appraisals SET IsDeleted = 1 WHERE Id = @Id", new { appraisal.Id });
        return appraisal.Id;
    }

    private async Task<string> NumberOfAsync(Guid appraisalId)
    {
        using var scope = CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppraisalDbContext>()
            .Appraisals.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == appraisalId)).AppraisalNumber!;
    }

    private async Task<InitiateReappraisalResult> InitiateAsync(Guid candidateId)
        => await SendAsync([candidateId], []);

    private async Task<InitiateReappraisalResult> SendAsync(List<Guid> candidateIds, List<Guid> nearbyIds)
    {
        using var scope = CreateScope();
        var user = new UserInfoDto("U1", "u1");
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new InitiateReappraisalCommand(candidateIds, nearbyIds, user, user), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ACompletedPrior_IsBound()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);

        var result = await InitiateAsync(await SeedCandidateAsync(book));

        var accepted = Assert.Single(result.Accepted!);
        Assert.Equal(prior, accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task ALivePriorThatIsNotCompleted_IsSkippedNotSentDownTheLegacyPath()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);

        var result = await InitiateAsync(await SeedCandidateAsync(book));

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        // Same shape as a nearby pick: the id of the appraisal that is not usable, never null ("not in CAS").
        Assert.Equal(prior, skipped.AppraisalId);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ALivePriorThatIsNotCompleted_IsReportedOncePerBookWhateverTheNumberOfCandidates()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        var candidates = new List<Guid>
        {
            await SeedCandidateAsync(book, collateralId: "301"),
            await SeedCandidateAsync(book, collateralId: "302"),
            await SeedCandidateAsync(book, collateralId: "303"),
        };

        var result = await SendAsync(candidates, []);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Equal(prior, skipped.AppraisalId);
    }

    [Fact]
    public async Task ABookWhoseOnlyAppraisalIsCancelled_TakesTheLegacyPath()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Cancelled);
        var book = await NumberOfAsync(prior);

        var result = await InitiateAsync(await SeedCandidateAsync(book));

        var accepted = Assert.Single(result.Accepted!);
        Assert.Null(accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
        Assert.Equal(book, (await PublishedEventForAsync(book)).PrevAppraisalNumber, ignoreCase: true);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("InProgress")]
    [InlineData("Cancelled")]
    public async Task ABookWhoseOnlyAppraisalIsSoftDeleted_TakesTheLegacyPathAgain(string status)
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.FromString(status), deleted: true);
        var book = await NumberOfAsync(prior);

        var result = await InitiateAsync(await SeedCandidateAsync(book));

        var accepted = Assert.Single(result.Accepted!);
        Assert.Null(accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
        Assert.Equal(book, (await PublishedEventForAsync(book)).PrevAppraisalNumber, ignoreCase: true);
    }

    [Fact]
    public async Task ANearbyPickOfASoftDeletedCompletedAppraisal_IsSkippedAndNotBound()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed, deleted: true);

        var result = await SendAsync([], [prior]);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Equal(prior, skipped.AppraisalId);
        Assert.Empty(result.Accepted ?? []);
    }

    // A block-project candidate whose project appraisal is cancelled: the candidate-units view finds no unit, so the
    // event carries no ProjectUnitId, and it has no PrevAppraisalId either (the book is not in CAS: legacy path).
    [Fact]
    public async Task ABlockCandidateWhoseProjectAppraisalIsCancelled_PublishesNoProjectUnitId()
    {
        var project = await SeedAppraisalAsync(AppraisalStatus.Cancelled);
        var book = await NumberOfAsync(project);
        var candidate = await SeedCandidateAsync(book);
        await ExecuteAsync(
            "UPDATE collateral.ReappraisalCandidates SET IsBlockUnit = 1 WHERE Id = @candidate; " +
            "INSERT INTO appraisal.Projects (Id, AppraisalId, ProjectType) VALUES (NEWID(), @project, 'U')",
            new { candidate, project });

        // The view filter itself: with the cancelled project excluded, the view has no row for the candidate at all
        // (without the filter it would return one with no matched unit).
        using (var scope = CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection()
                .ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM collateral.vw_ReappraisalCandidateUnits WHERE CandidateId = @candidate", new { candidate }));

        var result = await InitiateAsync(candidate);

        Assert.Single(result.Accepted!);
        var published = await PublishedEventForAsync(book);
        Assert.Null(published.ProjectUnitId);
        Assert.Null(published.PrevAppraisalId);
        Assert.Equal(book, published.PrevAppraisalNumber, ignoreCase: true);
    }

    // A NotDue candidate and a nearby pick of the same book end as ONE row, with the higher-precedence reason.
    [Fact]
    public async Task ANotDueCandidateAndANearbyPickThatIsAlreadyReviewed_GiveOneAlreadyReviewedRow()
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var notDue = await SeedCandidateAsync(book, due: false);
        using (var scope = CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            var reappraisal = CollateralResultSeed.NewCompleted(
                await CollateralResultSeed.InsertRequestAsync(factory, "03"), new DateTime(2026, 7, 1),
                AppraisalTypes.ReAppraisal, prevAppraisalId: prior, purpose: "03");
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.Appraisals.Add(reappraisal);
            await db.SaveChangesAsync();
        }

        var result = await SendAsync([notDue], [prior]);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("AlreadyReviewed", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    [Theory]
    [InlineData("Cancelled", false)]
    [InlineData("Completed", true)]
    public async Task ANotDueCandidateAndANearbyPickOfADeletedOrCancelledAppraisal_GiveOnePriorNotCompletedRow(
        string status, bool deleted)
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.FromString(status), deleted);
        var book = await NumberOfAsync(prior);
        var notDue = await SeedCandidateAsync(book, due: false);

        var result = await SendAsync([notDue], [prior]);

        // The candidate alone is NotDue (the book is not in CAS); the nearby pick of the unusable appraisal outranks it.
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Equal(prior, skipped.AppraisalId);
        Assert.Empty(result.Accepted ?? []);
    }

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", false)]
    public async Task TheDetailPageResolvesTheSelfAppraisalOnlyForABookInCas(string status, bool expectsId)
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.FromString(status));
        var book = await NumberOfAsync(prior);

        using var scope = CreateScope();
        var method = typeof(global::Collateral.Application.Features.Reappraisal.GetCandidateById.GetReappraisalCandidateByIdQueryHandler)
            .GetMethod("ResolveSelfAppraisalIdAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var id = await (Task<Guid>)method.Invoke(null, [
            scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection(), book, CancellationToken.None])!;

        Assert.Equal(expectsId ? prior : Guid.Empty, id);
    }

    [Fact]
    public async Task ANotDueCandidateOfAnInProgressBook_IsReportedOnceAsPriorNotCompleted_WithTheLiveId()
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        var notDue = await SeedCandidateAsync(book, due: false);

        var result = await InitiateAsync(notDue);

        var skipped = Assert.Single(result.Skipped);
        // PriorNotCompleted outranks NotDue, and carries the live appraisal's id.
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Equal(prior, skipped.AppraisalId);
    }

    [Fact]
    public async Task ANearbyCompletedAppraisal_IsBound()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);

        var result = await SendAsync([], [prior]);

        var accepted = Assert.Single(result.Accepted!);
        Assert.Equal(prior, accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
    }

    [Theory]
    [InlineData("InProgress")]
    [InlineData("Cancelled")]
    public async Task ANearbyAppraisalThatIsNotCompleted_IsSkipped(string status)
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.FromString(status));

        var result = await SendAsync([], [prior]);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Equal(prior, skipped.AppraisalId);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ABookSkippedOnTheCandidatePath_IsNotReportedAgainByTheNearbyPath()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);

        // The same book arrives as a candidate and as a nearby pick: one outcome.
        var result = await SendAsync([await SeedCandidateAsync(book)], [prior]);

        Assert.Single(result.Skipped);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ANotDueCandidate_DoesNotBlockANearbyPickOfItsCompletedBook()
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var notDue = await SeedCandidateAsync(book, due: false);

        var result = await SendAsync([notDue], [prior]);

        // One outcome per book: the accepted pick wins and the earlier NotDue row is dropped.
        Assert.Equal(prior, Assert.Single(result.Accepted!).PrevAppraisalId);
        Assert.Empty(result.Skipped);
    }

    // Block units are separate collaterals, so one unit can be initiated while another is waiting: the book then
    // appears in both lists (Accepted for the free unit, AlreadyInFlight for the waiting one). Pins current behaviour.
    [Fact]
    public async Task BlockUnitsOfOneBook_OneFreeAndOneWaiting_AreReportedPerUnit()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var free = await SeedCandidateAsync(book, collateralId: "601");
        var waiting = await SeedCandidateAsync(book, collateralId: "602");
        await ExecuteAsync(
            "UPDATE collateral.ReappraisalCandidates SET IsBlockUnit = 1 WHERE NormalizedSurveyNumber = @book", new { book });
        await WaitingRequestAsync(book, "602");

        var result = await SendAsync([free, waiting], []);

        Assert.Single(result.Accepted!);
        Assert.Equal("AlreadyInFlight", Assert.Single(result.Skipped).Reason);
    }

    // IX_Appraisals_AppraisalNumber is unique, so a number belongs to one appraisal and "book N has a completed A and an
    // in-progress B" cannot exist. The reachable overlap is a book accepted on the legacy path (its only appraisal is
    // soft-deleted) that the user also picks as a nearby row: one outcome, the legacy acceptance.
    [Fact]
    public async Task ABookAcceptedOnTheLegacyPath_IsNotAlsoSkippedByANearbyPickOfItsDeletedAppraisal()
    {
        var deleted = await SeedAppraisalAsync(AppraisalStatus.Completed, deleted: true);
        var book = await NumberOfAsync(deleted);

        var result = await SendAsync([await SeedCandidateAsync(book)], [deleted]);

        var accepted = Assert.Single(result.Accepted!);
        Assert.Null(accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
    }

    // Ordering is CollateralId as text, so '100' sorts before '20' whatever order the ids are sent in.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABookListedUnderTwoCollaterals_FreezesTheLowestCollateralIdAsText(bool lowFirst)
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var c100 = await SeedCandidateAsync(book, collateralId: "100", autoUpdate: "N");
        var c20 = await SeedCandidateAsync(book, collateralId: "20", autoUpdate: "Y");

        var result = await SendAsync(lowFirst ? [c100, c20] : [c20, c100], []);

        Assert.Single(result.Accepted!);
        var published = await PublishedEventForAsync(book);
        Assert.Equal("100", published.CollateralId);
        Assert.Equal("N", published.AutoUpdate);
    }

    [Fact]
    public async Task ANearbyPickMatchedToTwoPendingCandidatesOnTheSameFile_TakesTheLowestCollateralIdAsText()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        await SeedCandidateAsync(book, collateralId: "100", autoUpdate: "N");
        await SeedCandidateAsync(book, collateralId: "20", autoUpdate: "Y");

        var result = await SendAsync([], [prior]);

        Assert.Single(result.Accepted!);
        var published = await PublishedEventForAsync(book);
        Assert.Equal("100", published.CollateralId);
        Assert.Equal("N", published.AutoUpdate);
    }

    private sealed record PublishedEvent(
        string? CollateralId, string? AutoUpdate, string? PrevAppraisalNumber, string? PrevAppraisalId, string? ProjectUnitId);

    /// <summary>The Initiate event published for the book. The request copies its fields verbatim.</summary>
    private async Task<PublishedEvent> PublishedEventForAsync(string book)
    {
        using var scope = CreateScope();
        var payloads = (await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection()
            .QueryAsync<string>(
                "SELECT Payload FROM collateral.IntegrationEventOutbox WHERE EventType LIKE '%ReappraisalInitiated%' AND Payload LIKE @like",
                new { like = $"%{book}%" })).ToList();
        var json = System.Text.Json.JsonDocument.Parse(Assert.Single(payloads)).RootElement;
        string? Get(string name) => json.EnumerateObject()
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: System.Text.Json.JsonValueKind.String } v
            ? v.GetString() : null;
        return new PublishedEvent(
            Get("CollateralId"), Get("AutoUpdate"), Get("PrevAppraisalNumber"), Get("PrevAppraisalId"), Get("ProjectUnitId"));
    }

    private async Task ExecuteAsync(string sql, object args)
    {
        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection().ExecuteAsync(sql, args);
    }

    private async Task WaitingRequestAsync(string book, string collateralId)
    {
        using var scope = CreateScope();
        await CollateralResultSeed.InsertRequestAsync(
            scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(), "03", book, collateralId, null);
    }

    [Fact]
    public async Task ACandidateWithABlankBookNumber_IsSkippedAsNoBookNumber()
    {
        var candidate = await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");
        await ExecuteAsync(
            "UPDATE collateral.ReappraisalCandidates SET NormalizedSurveyNumber = N'   ', SurveyNumber = N'   ' WHERE Id = @candidate",
            new { candidate });

        var result = await InitiateAsync(candidate);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("NoBookNumber", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    // A book with one copy that is not due and one that is: the due copy is initiated and the book shows no skip row.
    [Theory]
    [InlineData("701")]
    [InlineData("702")]
    public async Task ABookWithANotDueCopyAndAnInitiatedCopy_ShowsAcceptedOnly(string notDueCollateral)
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var c701 = await SeedCandidateAsync(book, collateralId: "701", due: notDueCollateral != "701");
        var c702 = await SeedCandidateAsync(book, collateralId: "702", due: notDueCollateral != "702");

        var result = await SendAsync([c701, c702], []);

        Assert.Single(result.Accepted!);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public async Task ANonBlockBookSelectedUnderTwoCollaterals_GivesOneAcceptedAndNoSkipRow()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var first = await SeedCandidateAsync(book, collateralId: "601");
        var second = await SeedCandidateAsync(book, collateralId: "602");

        var result = await SendAsync([first, second], []);

        Assert.Single(result.Accepted!);
        Assert.Empty(result.Skipped);
    }

    // Two block units of one book: one has a request waiting (AlreadyInFlight), the other is not due (NotDue). The book
    // is reported once, as AlreadyInFlight, whichever unit comes first.
    [Theory]
    [InlineData("601")]
    [InlineData("602")]
    public async Task BlockUnitsOfOneBook_FollowThePrecedenceNotTheOrder(string unitWithAWaitingRequest)
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        var book = await NumberOfAsync(prior);
        var c601 = await SeedCandidateAsync(book, collateralId: "601", due: unitWithAWaitingRequest != "602");
        var c602 = await SeedCandidateAsync(book, collateralId: "602", due: unitWithAWaitingRequest == "602");
        await ExecuteAsync(
            "UPDATE collateral.ReappraisalCandidates SET IsBlockUnit = 1 WHERE NormalizedSurveyNumber = @book", new { book });
        await WaitingRequestAsync(book, unitWithAWaitingRequest);

        var result = await SendAsync([c601, c602], []);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("AlreadyInFlight", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ANearbyPickThatIsNotUsable_IsAlreadyInFlightWhenARequestIsWaiting()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        await WaitingRequestAsync(book, "999");

        var result = await SendAsync([], [prior]);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("AlreadyInFlight", skipped.Reason);
    }

    [Fact]
    public async Task ANearbyBookWhoseReappraisalHasCompleted_IsAlreadyReviewed()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.Completed);
        using (var scope = CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
            var reappraisal = CollateralResultSeed.NewCompleted(
                await CollateralResultSeed.InsertRequestAsync(factory, "03"), new DateTime(2026, 7, 1),
                AppraisalTypes.ReAppraisal, prevAppraisalId: prior, purpose: "03");
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            db.Appraisals.Add(reappraisal);
            await db.SaveChangesAsync();
        }

        var result = await SendAsync([], [prior]);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("AlreadyReviewed", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    // The candidate list and the detail page use the same rule as Initiate: a cancelled (or deleted) appraisal is not
    // "in CAS", so such a book is not labelled 'CAS'.
    [Theory]
    [InlineData("Completed", "CAS")]
    [InlineData("Cancelled", "Unknown")]
    public async Task TheCandidateViewLabelsAPriorBookCasOnlyWhenItsAppraisalIsNotCancelled(string status, string expected)
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.FromString(status));
        var book = await NumberOfAsync(prior);
        var candidate = await SeedCandidateAsync(book);

        using var scope = CreateScope();
        var source = await scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection()
            .QuerySingleAsync<string>(
                "SELECT PriorAppraisalSource FROM collateral.vw_ReappraisalCandidates WHERE Id = @candidate", new { candidate });

        Assert.Equal(expected, source);
    }

    [Fact]
    public async Task AMixOfDueAndNotDueCandidatesForOneUnusableBook_GivesOneSkip()
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        var due = await SeedCandidateAsync(book, collateralId: "401");
        var notDue = await SeedCandidateAsync(book, collateralId: "402", due: false);

        var result = await SendAsync([due, notDue], []);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ANotDueCandidateAndANearbyPickOfItsInProgressAppraisal_GiveOneSkip()
    {
        await SeedCandidateAsync($"99A{Random.Shared.Next(10000, 99999)}");   // keeps a newer file in the table
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        var notDue = await SeedCandidateAsync(book, due: false);

        var result = await SendAsync([notDue], [prior]);

        // PriorNotCompleted outranks NotDue, and the nearby row for the same book is not reported again.
        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("PriorNotCompleted", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task AnExistingWaitingRequest_OutranksAnUnusablePrior()
    {
        var prior = await SeedAppraisalAsync(AppraisalStatus.InProgress);
        var book = await NumberOfAsync(prior);
        var candidate = await SeedCandidateAsync(book);
        using (var scope = CreateScope())
            await CollateralResultSeed.InsertRequestAsync(
                scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(), "03", book, "999", null);

        var result = await InitiateAsync(candidate);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal("AlreadyInFlight", skipped.Reason);
        Assert.Empty(result.Accepted ?? []);
    }

    [Fact]
    public async Task ABookCasDoesNotKnow_StillTakesTheLegacyPath()
    {
        var book = $"99A{Random.Shared.Next(10000, 99999)}";

        var result = await InitiateAsync(await SeedCandidateAsync(book));

        var accepted = Assert.Single(result.Accepted!);
        Assert.Null(accepted.PrevAppraisalId);
        Assert.Empty(result.Skipped);
        Assert.Equal(book, (await PublishedEventForAsync(book)).PrevAppraisalNumber, ignoreCase: true);
    }
}
