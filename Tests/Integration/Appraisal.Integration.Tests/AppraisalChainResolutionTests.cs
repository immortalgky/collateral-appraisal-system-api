using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Pins <see cref="ResolveLatestInAppraisalChainQuery"/> — the appraisal-side replacement for the
/// CollateralMaster lookups the construction-inspection flow used to depend on.
///
/// <b>Nothing here seeds a CollateralMaster or a CollateralEngagement.</b> That is the point: before
/// this query, a chain whose collateral rows had never been backfilled resolved to "no engagement",
/// which silently stamped every inspection as the 1st and left its fee empty. These tests assert the
/// chain alone carries enough information.
///
/// The case that drives the design is <see cref="ResolvesToLatestInspection_WhenUserPicksTheOriginal"/>:
/// walking ancestors from the picked appraisal would find nothing, so the handler walks UP to the
/// chain root and then DOWN over every descendant.
/// </summary>
[Collection("Integration")]
public class AppraisalChainResolutionTests(IntegrationTestFixture fixture)
{
    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    /// <summary>
    /// A completed appraisal. Status and CompletedAt are set by reflection because the real status
    /// flow needs a workflow; these tests exercise the read path only — the same shortcut
    /// ConstructionCurrentValueServiceTests takes.
    /// </summary>
    private static AppraisalAggregate CompletedAppraisal(
        string appraisalType, Guid? prevAppraisalId, DateTime completedAt)
    {
        var a = AppraisalAggregate.Create(
            Guid.NewGuid(), appraisalType, "Normal", DateTime.Now, prevAppraisalId: prevAppraisalId);
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..18]);
        typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(a, AppraisalStatus.Completed);
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, completedAt);
        return a;
    }

    /// <summary>Seeds original → 1st inspection → 2nd inspection and returns their ids in order.</summary>
    private static async Task<(Guid Original, Guid First, Guid Second)> SeedTwoInspectionChainAsync(
        AppraisalDbContext db)
    {
        var baseTime = DateTime.Now.AddYears(-1);

        var original = CompletedAppraisal(AppraisalTypes.New, null, baseTime);
        db.Appraisals.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var first = CompletedAppraisal(AppraisalTypes.Progressive, original.Id, baseTime.AddMonths(3));
        db.Appraisals.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var second = CompletedAppraisal(AppraisalTypes.Progressive, first.Id, baseTime.AddMonths(6));
        db.Appraisals.Add(second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (original.Id, first.Id, second.Id);
    }

    // ── The reason the handler walks down, not just up ────────────────────────

    [Fact]
    public async Task ResolvesToLatestInspection_WhenUserPicksTheOriginal()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var (original, _, second) = await SeedTwoInspectionChainAsync(db);

        // The user picks the ORIGINAL appraisal even though two inspections already exist — the exact
        // case that made counting on the collateral master preferable to an ancestor-only walk.
        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(original), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(second, result.AppraisalId);
        Assert.Equal(2, result.ProgressiveCount);   // → the new request is the 3rd inspection
    }

    [Fact]
    public async Task ResolvesToLatestInspection_WhenUserPicksTheNewestInspection()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var (_, _, second) = await SeedTwoInspectionChainAsync(db);

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(second), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(second, result.AppraisalId);
        // Same answer from either end of the chain — the round number must not depend on the pick.
        Assert.Equal(2, result.ProgressiveCount);
    }

    // ── Boundaries ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SingleAppraisalChain_ReportsNoInspectionsYet()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var original = CompletedAppraisal(AppraisalTypes.New, null, DateTime.Now.AddMonths(-2));
        db.Appraisals.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(original.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(original.Id, result.AppraisalId);
        Assert.Equal(0, result.ProgressiveCount);   // → the new request is the 1st inspection
        Assert.Null(result.CompanyId);              // nothing assigned, so no company to force
    }

    [Fact]
    public async Task CancelledInspection_DoesNotConsumeARoundNumber()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var baseTime = DateTime.Now.AddYears(-1);
        var original = CompletedAppraisal(AppraisalTypes.New, null, baseTime);
        db.Appraisals.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var abandoned = CompletedAppraisal(AppraisalTypes.Progressive, original.Id, baseTime.AddMonths(2));
        typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(abandoned, AppraisalStatus.Cancelled);
        db.Appraisals.Add(abandoned);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(original.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        // The cancelled request is skipped, so the next real inspection is still the 1st.
        Assert.Equal(0, result.ProgressiveCount);
        // ...and it is not a valid copy source either — the original is still the chain tip.
        Assert.Equal(original.Id, result.AppraisalId);
    }

    /// <summary>
    /// Regression: the recursive walks must pass THROUGH a soft-deleted node. Filtering IsDeleted in
    /// the JOIN pruned the whole subtree beyond it, so one deleted inspection mid-chain hid every
    /// later one and reset the round number to 1.
    /// </summary>
    [Fact]
    public async Task SoftDeletedInspectionMidChain_DoesNotHideTheOnesAfterIt()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var baseTime = DateTime.Now.AddYears(-1);

        var original = CompletedAppraisal(AppraisalTypes.New, null, baseTime);
        db.Appraisals.Add(original);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var deleted = CompletedAppraisal(AppraisalTypes.Progressive, original.Id, baseTime.AddMonths(3));
        db.Appraisals.Add(deleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var afterDeleted = CompletedAppraisal(AppraisalTypes.Progressive, deleted.Id, baseTime.AddMonths(6));
        db.Appraisals.Add(afterDeleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        deleted.Delete(Guid.NewGuid());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(original.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        // The walk reaches past the deleted node...
        Assert.Equal(afterDeleted.Id, result.AppraisalId);
        // ...but the deleted inspection itself does not count towards the round number.
        Assert.Equal(1, result.ProgressiveCount);
    }

    /// <summary>
    /// Regression: elect the newest assignment, THEN read its company — do not hunt backwards for the
    /// newest assignment that happens to carry one. Skipping a company-less latest assignment would
    /// force the next inspection onto a company that no longer holds the case.
    /// </summary>
    [Fact]
    public async Task ReassignedToInternal_ReportsNoCompany_RatherThanThePreviousExternalOne()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var appraisal = CompletedAppraisal(AppraisalTypes.New, null, DateTime.Now.AddMonths(-6));
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var external = AppraisalAssignment.Create(
            appraisal.Id, "External", assigneeCompanyId: Guid.NewGuid().ToString(), assignedBy: "test");
        SetAssignedAt(external, DateTime.Now.AddMonths(-5));

        // Routed back to a bank appraiser: newest assignment, no company.
        var internalAssignment = AppraisalAssignment.Create(
            appraisal.Id, "Internal", assigneeUserId: "P0001", assignedBy: "test");
        SetAssignedAt(internalAssignment, DateTime.Now.AddMonths(-4));

        db.AppraisalAssignments.AddRange(external, internalAssignment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(appraisal.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Null(result.CompanyId);
        Assert.Equal(string.Empty, result.CompanyName);
    }

    private static void SetAssignedAt(AppraisalAssignment assignment, DateTime when)
        => typeof(AppraisalAssignment).GetProperty("AssignedAt")!.SetValue(assignment, when);

    [Fact]
    public async Task UnknownAppraisal_ResolvesToNull()
    {
        using var scope = CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(
            new ResolveLatestInAppraisalChainQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}
