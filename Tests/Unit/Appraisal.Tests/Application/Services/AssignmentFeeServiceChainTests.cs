using Appraisal.Infrastructure;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Exceptions;
using NSubstitute;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// The construction-inspection (Progressive) fee chain and the admin-override rules of
/// <see cref="AssignmentFeeService"/>: which figure the base fee is read from, that a quotation outranks it, and
/// that an admin's own base item is never recomputed.
/// </summary>
public class AssignmentFeeServiceChainTests
{
    // ── Source of the chained inspection fee ──

    [Theory]
    [InlineData(AppraisalTypes.New)]
    [InlineData(AppraisalTypes.Progressive)]
    public async Task KeyedInspectionFee_WinsForAnyPriorType(string priorType)
    {
        await using var db = NewDb();
        var prior = await AddAppraisalAsync(db, priorType, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(db, prior.Id, assigned: true);
        var priorFee = await AddFeeAsync(db, priorAssignment.Id, inspectionFee: 3_000m);
        priorFee.AddItem("01", "own fee", 9_999m); // the prior's own fee must NOT be what is read

        var source = await BuildService(db).ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, new AssignmentFeeSource.TierBased(), CancellationToken.None);

        var ci = Assert.IsType<AssignmentFeeSource.ConstructionInspection>(source);
        Assert.Equal(3_000m, ci.Amount);
        Assert.Equal("AP-PRIOR", ci.PriorAppraisalNumber);
    }

    [Fact]
    public Task NoKeyedInspectionFee_ResolvesNoAmount_EvenWhenThePriorHasItsOwnAppraisalFee() => NoAmountAsync(null);

    [Fact]
    public Task ZeroKeyedInspectionFee_ResolvesNoAmount_EvenWhenThePriorHasItsOwnAppraisalFee() => NoAmountAsync(0m);

    private static async Task NoAmountAsync(decimal? keyed)
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var prior = await AddAppraisalAsync(db, AppraisalTypes.Progressive, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(db, prior.Id, assigned: true);
        var priorFee = await AddFeeAsync(db, priorAssignment.Id, inspectionFee: keyed);
        priorFee.AddItem("01", "own fee", 2_500m); // must NOT stand in for the missing inspection fee
        priorFee.AddItem("02", "travel", 400m);
        await db.SaveChangesAsync();
        var service = BuildService(db);

        var source = await service.ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, new AssignmentFeeSource.TierBased(), CancellationToken.None);
        var resolved = await service.ResolveBaseFeeAsync(source, 5_000_000m, CancellationToken.None);

        var ci = Assert.IsType<AssignmentFeeSource.ConstructionInspection>(source);
        Assert.Null(ci.Amount);
        Assert.Equal("AP-PRIOR", ci.PriorAppraisalNumber);
        Assert.Null(resolved.Amount);
        Assert.Equal(FeeBaseKinds.None, resolved.Kind);
        Assert.Equal("AP-PRIOR", resolved.PriorAppraisalNumber);

        // Materialisation: the current appraisal's fee row gets no system base item.
        var current = await AddAppraisalAsync(db, AppraisalTypes.Progressive, prevId: prior.Id);
        var assignment = await AddAssignmentAsync(db, current.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id);
        await service.EnsureAssignmentFeeItemsAsync(current.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();
        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync(f => f.AssignmentId == assignment.Id);
        Assert.Empty(fee.Items);
    }

    [Fact]
    public async Task PriorWithoutAnyFee_ResolvesNoBaseAndNeverFallsBackToTier()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var prior = await AddAppraisalAsync(db, AppraisalTypes.New);
        await AddAssignmentAsync(db, prior.Id, assigned: true);
        var service = BuildService(db);

        var source = await service.ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, new AssignmentFeeSource.TierBased(), CancellationToken.None);
        var resolved = await service.ResolveBaseFeeAsync(source, 5_000_000m, CancellationToken.None);

        Assert.Null(resolved.Amount);
        Assert.Equal(FeeBaseKinds.None, resolved.Kind);
    }

    [Fact]
    public async Task Quotation_OutranksTheChainedFee_ForProgressiveToo()
    {
        await using var db = NewDb();
        var prior = await AddAppraisalAsync(db, AppraisalTypes.New);
        var priorAssignment = await AddAssignmentAsync(db, prior.Id, assigned: true);
        await AddFeeAsync(db, priorAssignment.Id, inspectionFee: 3_000m);
        var quotation = new AssignmentFeeSource.Quotation(4_200m, Guid.NewGuid(), "Q-1");

        var source = await BuildService(db).ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, quotation, CancellationToken.None);

        Assert.Same(quotation, source);
    }

    [Fact]
    public async Task ResolveBaseFee_ReportsTierBounds_AndWritesNothing()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        await db.SaveChangesAsync();

        var resolved = await BuildService(db).ResolveBaseFeeAsync(
            new AssignmentFeeSource.TierBased(AppraisalTypes.New), 8_000_000m, CancellationToken.None);

        Assert.Equal(FeeBaseKinds.Tier, resolved.Kind);
        Assert.Equal(3_000m, resolved.Amount);
        Assert.Equal(8_000_000m, resolved.SellingPrice);
        Assert.Equal(7_000_001m, resolved.TierMin);
        Assert.Equal(10_000_000m, resolved.TierMax);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    // ── Materialisation: the admin override and the HasItems bug ──

    [Fact]
    public async Task AdminBaseItem_IsNeverRecomputed_ByTierOrQuotation()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        fee.AddItem("01", "admin base", 3_500m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        foreach (var source in new AssignmentFeeSource[]
                 {
                     new AssignmentFeeSource.TierBased(AppraisalTypes.New),
                     new AssignmentFeeSource.Quotation(9_000m, Guid.NewGuid(), "Q-9")
                 })
        {
            await BuildService(db).EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);
        }

        var reloaded = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        var item = Assert.Single(reloaded.Items);
        Assert.Equal(FeeItemSource.Admin, item.Source);
        Assert.Equal(3_500m, item.FeeAmount);
        Assert.Equal(3_500m, reloaded.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task OtherItemsAlone_DoNotBlockTheBaseFee()
    {
        // Used to skip the whole materialisation whenever ANY item existed (HasItems).
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        fee.AddItem("02", "travel by admin", 400m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true,
            CancellationToken.None);

        var reloaded = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(2_500m, reloaded.Items.Single(i => i.FeeCode == "01").FeeAmount);
        Assert.Equal(2_900m, reloaded.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task Replay_DoesNotAddASecondBaseItem()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var source = new AssignmentFeeSource.TierBased(AppraisalTypes.New);

        await BuildService(db).EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();
        await BuildService(db).EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
    }

    [Theory]
    [InlineData("05")]
    [InlineData("06")]
    [InlineData("07")]
    public async Task FullAbsorb_StillRuns_WhenAnAdminBaseItemAndExtrasAlreadyExist(string paymentType)
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: paymentType);
        fee.AddItem("01", "admin base", 3_000m).MarkAsAdminSet();
        fee.AddItem("02", "admin travel", 1_000m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true, CancellationToken.None);

        var reloaded = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        Assert.Equal(4_280m, reloaded.TotalFeeAfterVAT);          // (3,000 + 1,000) * 1.07
        Assert.Equal(4_280m, reloaded.BankAbsorbAmount);
        Assert.Equal(0m, reloaded.CustomerPayableAmount);
    }

    [Fact]
    public async Task FullAbsorb_StillRuns_WhenOnlyExtrasExistAndNoBaseResolves()
    {
        // No tier configured: nothing resolves, but the admin's extra item is billable and must be absorbed.
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: "05");
        fee.AddItem("02", "admin travel", 1_000m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true, CancellationToken.None);

        var reloaded = await db.AppraisalFees.SingleAsync();
        Assert.Equal(1_070m, reloaded.BankAbsorbAmount);
    }

    [Fact]
    public async Task FullAbsorb_IsNotRepeated_OnAReplay_SoItemsAddedAfterAssignmentStayPayable()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: "05");
        fee.AddItem("01", "admin base", 3_000m).MarkAsAdminSet();
        await db.SaveChangesAsync();
        var service = BuildService(db);
        var source = new AssignmentFeeSource.TierBased(AppraisalTypes.New);

        await service.EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: true, CancellationToken.None);
        Assert.Equal(3_210m, fee.BankAbsorbAmount); // the whole bill, at the genuine assignment

        // The assignee adds a line afterwards; a replay / route-back re-fire must not swallow it into the absorb.
        fee.AddItem("02", "assignee travel", 1_000m).MarkAsUserAdded();
        await db.SaveChangesAsync();
        await service.EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);

        var reloaded = await db.AppraisalFees.AsNoTracking().SingleAsync();
        Assert.Equal(3_210m, reloaded.BankAbsorbAmount);
        Assert.Equal(1_070m, reloaded.CustomerPayableAmount); // the new line stays the customer's
    }

    [Fact]
    public async Task ChainedInspectionFee_IsMaterialisedAsTheBaseItem()
    {
        await using var db = NewDb();
        var prior = await AddAppraisalAsync(db, AppraisalTypes.Progressive, number: "AP-PRIOR");
        var priorAssignment = await AddAssignmentAsync(db, prior.Id, assigned: true);
        await AddFeeAsync(db, priorAssignment.Id, inspectionFee: 3_000m);
        var current = await AddAppraisalAsync(db, AppraisalTypes.Progressive, prevId: prior.Id);
        var assignment = await AddAssignmentAsync(db, current.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id);
        var service = BuildService(db);

        var source = await service.ResolveSourceForAppraisalAsync(current, new AssignmentFeeSource.TierBased(), CancellationToken.None);
        await service.EnsureAssignmentFeeItemsAsync(current.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);

        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync(f => f.AssignmentId == assignment.Id);
        var item = Assert.Single(fee.Items);
        Assert.Equal(3_000m, item.FeeAmount);
        Assert.Equal(FeeItemSource.System, item.Source);
    }

    [Fact]
    public async Task ReassignedShell_CopiesTheKeyedInspectionFee()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var first = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, first.Id, sellingPrice: 1_000_000m, inspectionFee: 3_000m, paymentType: "01");
        var second = await AddAssignmentAsync(db, appraisal.Id, assigned: true);

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, second.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        var shell = await db.AppraisalFees.SingleAsync(f => f.AssignmentId == second.Id);
        Assert.Equal(3_000m, shell.ConstructionInspectionFeeAmount);
        Assert.Equal("01", shell.FeePaymentType);
    }

    // ── A system base is re-priced to the current source (route-back to Pending) ──

    [Fact]
    public async Task SystemBase_FromAnEarlierSource_IsReplacedByTheCurrentOne()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var service = BuildService(db);

        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();
        var firstId = (await db.AppraisalFeeItems.SingleAsync()).Id;

        // Routed back, then assigned by quotation: the quoted price must replace the tier fee, not sit behind it.
        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.Quotation(4_200m, Guid.NewGuid(), "Q-7"), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        var item = Assert.Single(fee.Items);
        Assert.Equal(firstId, item.Id);
        Assert.Equal(4_200m, item.FeeAmount);
        Assert.Equal(FeeItemSource.System, item.Source);
        Assert.Contains("Q-7", item.FeeDescription);
        Assert.Equal(4_200m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task SystemBase_IsDropped_WhenTheCurrentSourcePricesNothing()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var service = BuildService(db);
        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        // Now a construction inspection whose prior keyed no fee: nothing may keep billing the old tier fee.
        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.ConstructionInspection(null), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        Assert.Empty(fee.Items);
        Assert.Equal(0m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task ChainedInspectionFee_ComesFromTheNewestLiveAssignmentsFee()
    {
        // A re-assigned prior bills from the assignment that did the work: the newest one, not the first.
        await using var db = NewDb();
        var prior = await AddAppraisalAsync(db, AppraisalTypes.New, number: "AP-PRIOR");
        var first = await AddAssignmentAsync(db, prior.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        await AddFeeAsync(db, first.Id, inspectionFee: 1_000m);
        var second = await AddAssignmentAsync(db, prior.Id, assigned: true, createdAt: new DateTime(2026, 2, 1));
        await AddFeeAsync(db, second.Id, inspectionFee: 3_000m);
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignedAt))!
            .SetValue(first, new DateTime(2026, 1, 2));
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignedAt))!
            .SetValue(second, new DateTime(2026, 2, 2));
        await db.SaveChangesAsync();

        var source = await BuildService(db).ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, new AssignmentFeeSource.TierBased(), CancellationToken.None);

        Assert.Equal(3_000m, Assert.IsType<AssignmentFeeSource.ConstructionInspection>(source).Amount);
    }

    [Fact]
    public async Task ChainedInspectionFee_IgnoresACancelledNewerAssignment_ViaTheGlobalFilter()
    {
        await using var db = NewDb();
        var prior = await AddAppraisalAsync(db, AppraisalTypes.New, number: "AP-PRIOR");
        var live = await AddAssignmentAsync(db, prior.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        await AddFeeAsync(db, live.Id, inspectionFee: 3_000m);
        var cancelled = await AddAssignmentAsync(db, prior.Id, assigned: true, createdAt: new DateTime(2026, 2, 1));
        await AddFeeAsync(db, cancelled.Id, inspectionFee: 9_999m);
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignedAt))!
            .SetValue(live, new DateTime(2026, 1, 2));
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignedAt))!
            .SetValue(cancelled, new DateTime(2026, 2, 2));
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignmentStatus))!
            .SetValue(cancelled, AssignmentStatus.Cancelled);
        await db.SaveChangesAsync();

        var source = await BuildService(db).ResolveSourceForAppraisalAsync(
            AppraisalTypes.Progressive, prior.Id, new AssignmentFeeSource.TierBased(), CancellationToken.None);

        Assert.Equal(3_000m, Assert.IsType<AssignmentFeeSource.ConstructionInspection>(source).Amount);
    }

    // ── A re-fire on an already-assigned row leaves existing items alone ──

    [Fact]
    public async Task SystemBase_IsNotTouched_ByACallThatIsNotAGenuineReassignment()
    {
        // InternalAssigned re-fires on a later route-back (checker -> keyin) with the row long since Assigned: the base
        // may already be billed, so neither a different source nor "prices nothing" may rewrite or drop it.
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var service = BuildService(db);
        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        foreach (var source in new AssignmentFeeSource[]
                 {
                     new AssignmentFeeSource.Quotation(4_200m, Guid.NewGuid(), "Q-7"),
                     new AssignmentFeeSource.ConstructionInspection(null)
                 })
        {
            await service.EnsureAssignmentFeeItemsAsync(appraisal.Id, assignment.Id, source, repriceSystemBase: false, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        var item = Assert.Single(fee.Items);
        Assert.Equal(2_500m, item.FeeAmount);
        Assert.Equal(2_500m, fee.TotalFeeBeforeVAT);
    }

    // ── A tier needs a selling price ──

    [Fact]
    public async Task IbgRequestRow_WithANullSellingPrice_StillGetsTheTierBase_PricedAtZero()
    {
        // IBG requests legitimately carry no selling price (LoanDetail.Validate), but always a payment type.
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: null, paymentType: "01");

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        var item = Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
        Assert.Equal(2_500m, item.FeeAmount); // the lowest band, as before
    }

    [Theory]
    [InlineData("tier")]
    [InlineData("quotation")]
    [InlineData("ci")]
    public async Task AdminCreatedBlankRow_GetsNoBase_ExactlyAsWhenNoFeeRowExisted(string kind)
    {
        // The row an admin's first edit creates for a request with no fee data carries no payment type, notes or
        // price. Assignment used to skip such an appraisal entirely (no row), whatever the source.
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: null);
        fee.AddItem("02", "admin travel", 400m).MarkAsAdminSet();
        await db.SaveChangesAsync();
        AssignmentFeeSource source = kind switch
        {
            "tier" => new AssignmentFeeSource.TierBased(AppraisalTypes.New),
            "quotation" => new AssignmentFeeSource.Quotation(4_200m, Guid.NewGuid(), "Q-1"),
            _ => new AssignmentFeeSource.ConstructionInspection(3_000m)
        };

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, source, repriceSystemBase: true, CancellationToken.None);
        await db.SaveChangesAsync();

        var item = Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
        Assert.Equal("02", item.FeeCode);
    }

    // ── One place decides whether an unusable quotation price matters ──

    private static AssignmentFeeSource.Quotation Unpriced(string error = "no usable price") =>
        new(null, Guid.NewGuid(), "Q-9", error);

    [Fact]
    public async Task QuotationWithoutAPrice_NeverCreatesABaseAtZero_AndDoesNotThrow()
    {
        // The assign guard waived the price because an admin base existed, but it was removed before the consumer
        // ran. The workflow has already moved on, so this must not throw (retry -> error queue, row stuck Pending):
        // it creates no base, never a 01 at 0, and carries on.
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m, paymentType: "01");

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Unpriced("winner has no usable price"), repriceSystemBase: true,
            CancellationToken.None);

        Assert.Empty((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
    }

    [Fact]
    public async Task QuotationWithoutAPrice_IsFine_WhenTheAdminsOwnBaseOutranksIt()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m, paymentType: "01");
        fee.AddItem("01", "admin base", 3_500m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Unpriced(), repriceSystemBase: true, CancellationToken.None);

        var item = Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
        Assert.Equal((FeeItemSource.Admin, 3_500m), (item.Source, item.FeeAmount));
    }

    // A re-assignment (repriceSystemBase) whose new source prices nothing must not keep billing the PREVIOUS source's
    // system base: the fee preview drops it on reprice, and so must the result. Admin / user items are never touched.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UnpricedQuotation_OnReassignmentDropsTheStaleSystemBase_ButAReplayKeepsIt(bool reprice, bool baseKept)
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m, paymentType: "01");
        fee.AddItem("01", "system base", 2_500m);
        fee.AddItem("02", "admin travel", 400m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Unpriced(), repriceSystemBase: reprice, CancellationToken.None);
        await db.SaveChangesAsync();

        var items = (await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items;
        Assert.Equal(baseKept, items.Any(i => i.FeeCode == "01"));
        Assert.Equal(400m, Assert.Single(items, i => i.FeeCode == "02").FeeAmount); // never touched
        var billable = baseKept ? 2_900m : 400m;
        Assert.Equal(billable, (await db.AppraisalFees.SingleAsync()).TotalFeeBeforeVAT);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BlankShell_OnReassignmentDropsTheStaleSystemBase_ButAReplayKeepsIt(bool reprice, bool baseKept)
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: null); // blank shell
        fee.AddItem("01", "system base", 2_500m);
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(), repriceSystemBase: reprice,
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(baseKept, (await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items.Any());
    }

    [Fact]
    public async Task UnpricedQuotation_OnReassignmentNeverRemovesAnAdminBase()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, paymentType: null);
        fee.AddItem("01", "admin base", 3_500m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Unpriced(), repriceSystemBase: true, CancellationToken.None);

        Assert.Equal(3_500m, Assert.Single(fee.Items).FeeAmount);
    }

    // The admin touches the stored system base while the consumer is re-pricing it (between its load and its save):
    // simulated from the description lookup, which runs inside that window.
    private static (AppraisalDbContext Db, AssignmentFeeService Service) ServiceWithConcurrentEdit(
        string dbName, Func<AppraisalDbContext, Task> concurrentEdit)
    {
        AppraisalDbContext Open() => new(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(dbName).Options);

        var lookup = Substitute.For<IParameterLookupService>();
        lookup.GetDescriptionAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await using var other = Open();
                await concurrentEdit(other);
                return (string?)null;
            });
        var db = Open();
        return (db, new AssignmentFeeService(db, lookup, NullLogger<AssignmentFeeService>.Instance));
    }

    [Fact]
    public async Task BaseDeletedWhileItWasRepriced_IsSkipped_NotAConcurrencyException()
    {
        var name = Guid.NewGuid().ToString();
        var (db, service) = ServiceWithConcurrentEdit(name, async other =>
        {
            var gone = await other.AppraisalFeeItems.SingleAsync(i => i.FeeCode == "01");
            other.AppraisalFeeItems.Remove(gone);
            await other.SaveChangesAsync();
        });
        await using var _ = db;
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        fee.AddItem("01", "stale system base", 9_000m);
        fee.AddItem("02", "admin travel", 400m).MarkAsAdminSet();
        await db.SaveChangesAsync();

        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync(); // used to throw DbUpdateConcurrencyException

        await using var check = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(name).Options);
        var items = await check.AppraisalFeeItems.ToListAsync();
        Assert.Equal("02", Assert.Single(items).FeeCode);
        Assert.Equal(400m, (await check.AppraisalFees.SingleAsync()).TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task BaseOverriddenByTheAdminWhileItWasRepriced_KeepsTheAdminsOverride()
    {
        var name = Guid.NewGuid().ToString();
        var (db, service) = ServiceWithConcurrentEdit(name, async other =>
        {
            var item = await other.AppraisalFeeItems.SingleAsync(i => i.FeeCode == "01");
            item.Update("01", "admin base", 7_000m);
            item.MarkAsAdminSet();
            await other.SaveChangesAsync();
        });
        await using var _ = db;
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        fee.AddItem("01", "stale system base", 9_000m);
        await db.SaveChangesAsync();

        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        await using var check = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(name).Options);
        var item = await check.AppraisalFeeItems.SingleAsync();
        Assert.Equal((FeeItemSource.Admin, 7_000m), (item.Source, item.FeeAmount));
    }

    [Fact]
    public void QuotationResolution_NeverYieldsAnAmount_WithoutAUsablePrice()
    {
        var service = BuildService(NewDb());

        Assert.All(new decimal?[] { null, 0m, -5m }, amount =>
        {
            var resolved = service.ResolveBaseFeeAsync(
                new AssignmentFeeSource.Quotation(amount, Guid.NewGuid(), "Q-1"), 0m, CancellationToken.None).Result;
            Assert.Null(resolved.Amount);
            Assert.Equal(FeeBaseKinds.QuotationPending, resolved.Kind);
        });
    }

    // ── A re-fire must not add a base to a fee that already has items ──

    [Fact]
    public async Task ReFire_DoesNotAddABase_ToAFeeThatAlreadyHasItems()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m, paymentType: "01");
        fee.AddItem("02", "travel", 400m).MarkAsUserAdded();
        await db.SaveChangesAsync();

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal("02", Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items).FeeCode);
    }

    [Fact]
    public async Task ReFire_StillAddsTheBase_ToAnEmptyFee()
    {
        await using var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m, paymentType: "01");

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: false, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(2_500m, Assert.Single((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items).FeeAmount);
    }

    // ── An admin 01 that lands while assignment runs ──

    [Fact]
    public async Task AdminBase_AddedWhileAssignmentRuns_WinsOverTheSystemBase()
    {
        var options = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppraisalDbContext(options);
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);

        // The fee name lookup runs after the fee is loaded and before the system base is added: the admin's write
        // (another request, another context) lands exactly there.
        var lookup = Substitute.For<IParameterLookupService>();
        lookup.GetDescriptionAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                using var other = new AppraisalDbContext(options);
                var rival = other.AppraisalFees.Include(f => f.Items).Single();
                rival.AddItem("01", "admin base", 3_000m).MarkAsAdminSet();
                other.SaveChanges();
                return Task.FromResult<string?>(null);
            });
        var service = new AssignmentFeeService(db, lookup, NullLogger<AssignmentFeeService>.Instance);

        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var reloaded = await db.AppraisalFees.AsNoTracking().Include(f => f.Items).SingleAsync(f => f.Id == fee.Id);
        var item = Assert.Single(reloaded.Items);
        Assert.Equal(FeeItemSource.Admin, item.Source);
        Assert.Equal(3_000m, reloaded.TotalFeeBeforeVAT); // totals follow the admin line, not a stale snapshot
    }

    [Fact]
    public async Task AdminOverrideOfTheVeryItemBeingRepriced_IsKept_NotDeletedByTheRaceCheck()
    {
        // The admin converted THE system base (their override of that item) while assignment re-priced it. The race
        // check must not mistake that converted row for "an admin base that appeared" and delete it.
        var options = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppraisalDbContext(options);
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var stored = fee.AddItem("01", "old system base", 9_000m);
        await db.SaveChangesAsync();

        var lookup = Substitute.For<IParameterLookupService>();
        lookup.GetDescriptionAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                using var other = new AppraisalDbContext(options);
                var rival = other.AppraisalFees.Include(f => f.Items).Single();
                var mine = rival.UpdateItem(stored.Id, "01", "admin base", 4_000m);
                mine.MarkAsAdminSet();
                other.SaveChanges();
                return Task.FromResult<string?>(null);
            });
        var service = new AssignmentFeeService(db, lookup, NullLogger<AssignmentFeeService>.Instance);

        await service.EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var reloaded = await db.AppraisalFees.AsNoTracking().Include(f => f.Items).SingleAsync();
        var item = Assert.Single(reloaded.Items);
        Assert.Equal((FeeItemSource.Admin, 4_000m), (item.Source, item.FeeAmount)); // theirs, not ours (2,500)
        Assert.Equal(4_000m, reloaded.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task RemovingTheBaseOfAnOtherwiseEmptyFee_KeepsTheRequestedAbsorb()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        fee.AddItem("01", "old base", 2_500m);
        fee.RestoreRequestedBankAbsorb(3_000m);
        await db.SaveChangesAsync();

        // The current source prices nothing: the stale base goes and the fee ends empty.
        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.ConstructionInspection(null), repriceSystemBase: true,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var reloaded = await db.AppraisalFees.AsNoTracking().Include(f => f.Items).SingleAsync();
        Assert.Empty(reloaded.Items);
        Assert.Equal(3_000m, reloaded.BankAbsorbAmount); // not clamped to 0 by the removal
    }

    [Fact]
    public async Task AFeeHoldingOnlyNonBillableItems_KeepsTheRequestedAbsorb_InsteadOfCappingItAtZero()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        var fee = await AddFeeAsync(db, assignment.Id, sellingPrice: 1_000_000m);
        var pending = fee.AddItem("02", "assignee travel", 400m);
        pending.MarkAsUserAdded();
        pending.MakePendingApproval(); // not billable: the total is 0
        fee.RecalculateFromItems();
        fee.RestoreRequestedBankAbsorb(3_000m);
        await db.SaveChangesAsync();
        Assert.Equal(0m, fee.TotalFeeAfterVAT);

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(), repriceSystemBase: false,
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(3_000m, (await db.AppraisalFees.AsNoTracking().SingleAsync()).BankAbsorbAmount);
    }

    [Theory]
    [InlineData("noRow")]
    [InlineData("blankShell")]
    public async Task UnpricedQuotation_DoesNotThrow_WhenThereIsNoFeeRowOrOnlyABlankShell(string shape)
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        if (shape == "blankShell")
            await AddFeeAsync(db, assignment.Id, paymentType: null);

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, Unpriced("winner has no usable price"), repriceSystemBase: true,
            CancellationToken.None);

        Assert.Empty(db.AppraisalFees.Include(f => f.Items).SelectMany(f => f.Items));
    }

    [Fact]
    public async Task PricedQuotation_OnABlankShell_IsStillNotBilled_AsWhenNoRowExisted()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: true);
        await AddFeeAsync(db, assignment.Id, paymentType: null);

        await BuildService(db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.Quotation(4_200m, Guid.NewGuid(), "Q-1"),
            repriceSystemBase: true, CancellationToken.None);

        Assert.Empty((await db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
    }
}
