using Appraisal.Application.Features.Fees.AddFeeItem;
using Appraisal.Application.Features.Fees.RemoveFeeItem;
using Appraisal.Application.Features.Fees.UpdateFee;
using Appraisal.Application.Features.Fees.UpdateFeeItem;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Workflow.Contracts.FeeAppointmentApprovals;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Admin fee editing before assignment (admin stage = the fee's assignment is still Pending) and its lock
/// afterwards. Before handover an item is the admin's, billable at once, with no approval evaluation.
/// </summary>
public class AdminFeeEditingTests
{
    private readonly AppraisalDbContext _db = NewDb();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();

    public AdminFeeEditingTests() =>
        _sender.Send(Arg.Any<EvaluateFeeAppointmentApprovalQuery>(), Arg.Any<CancellationToken>())
            .Returns(new EvaluateFeeAppointmentApprovalResult(false, FeesRequireApproval: true));

    private AddFeeItemCommandHandler Add => new(_db, _sender, _user);
    private UpdateFeeItemCommandHandler Update => new(_db, _sender, _user);
    private RemoveFeeItemCommandHandler Remove => new(_db, _sender, _user);

    private async Task<(Guid AppraisalId, AppraisalFee Fee)> SeedAsync(bool assigned)
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id, assigned);
        return (appraisal.Id, await AddFeeAsync(_db, assignment.Id));
    }

    private static AddFeeItemCommand AddCmd(Guid appraisalId, Guid? feeId, string code = "01", decimal amount = 3_500m) =>
        new(appraisalId, feeId, code, "desc", amount);

    // ── Add ──

    [Fact]
    public async Task AdminStage_Add_CreatesABillableAdminItem_WithoutAskingTheApprovalPolicy()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);

        var result = await Add.Handle(AddCmd(appraisalId, fee.Id), default);
        await _db.SaveChangesAsync();

        var item = Assert.Single((await _db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
        Assert.Equal(fee.Id, result.FeeId);
        Assert.Equal(FeeItemSource.Admin, item.Source);
        Assert.False(item.RequiresApproval);
        Assert.Null(item.ApprovalStatus);
        Assert.Equal(3_500m, fee.TotalFeeBeforeVAT);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(EvaluateFeeAppointmentApprovalQuery)!, default);
    }

    [Fact]
    public async Task AdminStage_AddingABase_OverridesTheOnlyStoredSystemBase_InsteadOfRefusing()
    {
        // After a route-back the preview hides the stale system 01, so the admin cannot "update" it.
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        var stale = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        var result = await Add.Handle(AddCmd(appraisalId, fee.Id, "01", 4_000m), default);
        await _db.SaveChangesAsync();

        var item = Assert.Single((await _db.AppraisalFees.Include(f => f.Items).SingleAsync()).Items);
        Assert.Equal(stale.Id, result.ItemId);
        Assert.Equal((FeeItemSource.Admin, 4_000m, "desc"), (item.Source, item.FeeAmount, item.FeeDescription));
        Assert.Equal(4_000m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task AdminStage_AddingABase_StillRefuses_WhenTheStoredBaseIsAnAdminOne()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        fee.AddItem("01", "admin base", 2_500m).MarkAsAdminSet();
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => Add.Handle(AddCmd(appraisalId, fee.Id, "01", 4_000m), default));
    }

    // The previous assignee's item awaiting approval (left over after a route-back) must not lock the admin out.
    private static AppraisalFeeItem AddPendingUserItem(AppraisalFee fee)
    {
        var item = fee.AddItem("02", "assignee travel", 400m);
        item.MarkAsUserAdded();
        item.MakePendingApproval();
        item.MarkApprovalSubmitted();
        return item;
    }

    [Fact]
    public async Task AdminStage_ThePreviousAssigneesPendingApproval_DoesNotLockTheAdmin()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        AddPendingUserItem(fee);
        var adminItem = fee.AddItem("03", "admin extra", 100m);
        adminItem.MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var added = await Add.Handle(AddCmd(appraisalId, fee.Id, "01", 4_000m), default);
        await Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, adminItem.Id, "03", "admin extra", 150m), default);
        await Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, added.ItemId), default);
    }

    [Fact]
    public async Task AfterAssignment_APendingApproval_LocksAddUpdateAndRemove_AsAConflictNotA500()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var pending = AddPendingUserItem(fee);
        await _db.SaveChangesAsync();

        var add = await Assert.ThrowsAsync<ConflictException>(() => Add.Handle(AddCmd(appraisalId, fee.Id, "03", 100m), default));
        var update = await Assert.ThrowsAsync<ConflictException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, pending.Id, "02", "x", 500m), default));
        var remove = await Assert.ThrowsAsync<ConflictException>(() =>
            Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, pending.Id), default));

        Assert.All([add, update, remove], e => Assert.Equal("FeeApprovalAwaitingReview", e.Code));
    }

    [Fact]
    public async Task AdminStage_AddWithoutFeeId_CreatesTheFeeRowOnTheAwaitingAssignmentRow()
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id, assigned: false);
        // The request carried no FeeDto, so there is no fee row at all.

        var result = await Add.Handle(AddCmd(appraisal.Id, null), default);
        await _db.SaveChangesAsync();

        var fee = await _db.AppraisalFees.Include(f => f.Items).SingleAsync();
        Assert.Equal(result.FeeId, fee.Id);
        Assert.Equal(assignment.Id, fee.AssignmentId);
        Assert.Equal(FeeItemSource.Admin, Assert.Single(fee.Items).Source);
    }

    [Fact]
    public async Task AdminStage_AddWithoutFeeId_ReusesTheExistingFeeRow()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);

        var result = await Add.Handle(AddCmd(appraisalId, null, "02", 500m), default);

        Assert.Equal(fee.Id, result.FeeId);
        Assert.Equal(1, await _db.AppraisalFees.CountAsync());
    }

    [Fact]
    public async Task AfterAssignment_AddWithoutFeeId_AndNoFeeRow_IsRefused()
    {
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        await AddAssignmentAsync(_db, appraisal.Id, assigned: true);

        await Assert.ThrowsAsync<BadRequestException>(() => Add.Handle(AddCmd(appraisal.Id, null), default));
    }

    [Fact]
    public async Task AfterAssignment_AddWithoutFeeId_BehavesLikeTheFeeIdRoute_WhenTheRowExists()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);

        var result = await Add.Handle(AddCmd(appraisalId, null, "02", 400m), default);

        Assert.Equal(fee.Id, result.FeeId);
        Assert.Equal(FeeItemSource.User, Assert.Single(fee.Items).Source);
    }

    [Fact]
    public async Task AdminStage_SecondBaseItem_IsRefused()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        await Add.Handle(AddCmd(appraisalId, fee.Id), default);

        await Assert.ThrowsAsync<BadRequestException>(() => Add.Handle(AddCmd(appraisalId, fee.Id), default));
    }

    [Fact]
    public async Task AfterAssignment_AddByTheAssignee_StillGoesThroughApproval_AsAUserItem()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);

        await Add.Handle(AddCmd(appraisalId, fee.Id, "02", 400m), default);

        var item = Assert.Single(fee.Items);
        Assert.Equal(FeeItemSource.User, item.Source);
        Assert.True(item.RequiresApproval); // the stubbed verdict says approval is needed
        await _sender.Received(1).Send(Arg.Any<EvaluateFeeAppointmentApprovalQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterAssignment_AddingA01_WhenTheFeeHasNone_Is409_LikeTheUpdatePath()
    {
        // No base exists (e.g. a chained inspection whose prior keyed no fee): the assignee still cannot add one.
        var (appraisalId, fee) = await SeedAsync(assigned: true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Add.Handle(AddCmd(appraisalId, fee.Id), default));

        Assert.Equal("FeeBaseSetBySystem", ex.Code);
        Assert.Empty(fee.Items);
    }

    // ── Update ──

    [Fact]
    public async Task AdminStage_UpdatingTheSystemBase_MakesItTheAdminsOwn()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        var system = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        await Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, system.Id, "01", "admin base", 4_000m), default);

        Assert.Equal(FeeItemSource.Admin, system.Source);
        Assert.Equal(4_000m, fee.TotalFeeBeforeVAT);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(EvaluateFeeAppointmentApprovalQuery)!, default);
    }

    [Fact]
    public async Task AfterAssignment_AdminItem_CannotBeUpdated_409()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var admin = fee.AddItem("01", "admin base", 3_500m);
        admin.MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, admin.Id, "01", "x", 1m), default));

        Assert.Equal("FeeItemSetByAdmin", ex.Code);
        Assert.Equal(3_500m, admin.FeeAmount);
    }

    [Fact]
    public async Task AfterAssignment_UserItem_CanStillBeUpdated()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var user = fee.AddItem("02", "travel", 400m);
        user.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        await Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, user.Id, "02", "travel", 450m), default);

        Assert.Equal(450m, user.FeeAmount);
        Assert.Equal(FeeItemSource.User, user.Source);
    }

    // ── Remove ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemBaseItem_CannotBeRemoved_AtAnyStage(bool assigned)
    {
        var (appraisalId, fee) = await SeedAsync(assigned);
        var system = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, system.Id), default));

        Assert.Single(fee.Items);
    }

    [Fact]
    public async Task AdminStage_RemovingTheAdminBase_HandsTheBaseBackToTheSystem()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        await Add.Handle(AddCmd(appraisalId, fee.Id), default);
        await _db.SaveChangesAsync();
        var admin = fee.Items.Single();

        await Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, admin.Id), default);

        Assert.Empty(fee.Items);
        Assert.Equal(0m, fee.TotalFeeBeforeVAT);
    }

    [Fact]
    public async Task AfterAssignment_AdminItem_CannotBeRemoved_409()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var admin = fee.AddItem("02", "admin travel", 500m);
        admin.MarkAsAdminSet();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, admin.Id), default));

        Assert.Equal("FeeItemSetByAdmin", ex.Code);
        Assert.Single(fee.Items);
    }

    [Fact]
    public async Task AfterAssignment_UserItem_CanStillBeRemoved()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var user = fee.AddItem("02", "travel", 400m);
        user.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        await Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, user.Id), default);

        Assert.Empty(fee.Items);
    }

    // ── The requested absorb survives admin-stage edits ──

    [Theory]
    [InlineData(3_000, 3_000)]   // fits the final total (3,210): applied as asked
    [InlineData(5_000, 3_210)]   // above it: capped at the final total, and only then
    public async Task AdminStage_RequestedAbsorb_IsNotClampedByPartialTotals_AndAppliedAtAssignment(
        double requested, double expectedAbsorb)
    {
        SeedGenericLadder(_db);
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(_db, appraisal.Id, assigned: false);
        var fee = await AddFeeAsync(_db, assignment.Id, sellingPrice: 1_000_000m);
        fee.SetBankAbsorb((decimal)requested); // carried on the request; no items yet, so no bound to check
        await _db.SaveChangesAsync();

        // Admin adds a 500 line: the partial total is 535, which used to clamp the absorb to 535 for good.
        await Add.Handle(AddCmd(appraisal.Id, fee.Id, "02", 500m), default);
        await _db.SaveChangesAsync();
        Assert.Equal((decimal)requested, fee.BankAbsorbAmount);
        Assert.Equal(0m, fee.CustomerPayableAmount); // floored, never negative

        // Assignment at tier 2,500: final total (2,500 + 500) * 1.07 = 3,210.
        assignment.Assign("Internal", assigneeUserId: "U1", assignedBy: "test");
        await BuildService(_db).EnsureAssignmentFeeItemsAsync(
            appraisal.Id, assignment.Id, new AssignmentFeeSource.TierBased(AppraisalTypes.New),
            repriceSystemBase: true, default);
        await _db.SaveChangesAsync();

        var reloaded = await _db.AppraisalFees.AsNoTracking().SingleAsync();
        Assert.Equal(3_210m, reloaded.TotalFeeAfterVAT);
        Assert.Equal((decimal)expectedAbsorb, reloaded.BankAbsorbAmount);
        Assert.Equal(3_210m - (decimal)expectedAbsorb, reloaded.CustomerPayableAmount);
    }

    [Fact]
    public async Task AdminStage_UpdateAndRemove_KeepTheRequestedAbsorbToo()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        fee.SetBankAbsorb(3_000m);
        var extra = fee.AddItem("02", "admin travel", 500m);
        extra.MarkAsAdminSet();
        fee.RestoreRequestedBankAbsorb(3_000m);
        await _db.SaveChangesAsync();

        await Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, extra.Id, "02", "admin travel", 600m), default);
        Assert.Equal(3_000m, fee.BankAbsorbAmount);

        await Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, extra.Id), default);
        Assert.Equal(3_000m, fee.BankAbsorbAmount);
    }

    // ── The base line (01) cannot be re-coded ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SystemBaseItem_CannotBeRecoded_AtAnyStage(bool assigned)
    {
        var (appraisalId, fee) = await SeedAsync(assigned);
        var system = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, system.Id, "02", "now travel", 2_500m), default));

        Assert.Equal("01", system.FeeCode);
        Assert.Equal(FeeItemSource.System, system.Source);
    }

    [Fact]
    public async Task AnotherItem_CannotBeRecodedInto01_WhenNoBaseExists()
    {
        // After handover a user item must not be able to pose as the base line (which the system re-prices / guards).
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var user = fee.AddItem("02", "travel", 400m);
        user.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, user.Id, "01", "travel", 400m), default));

        Assert.Equal("02", user.FeeCode);
    }

    // ── Admin stage touches only the admin's own items and the system base ──

    [Fact]
    public async Task AdminStage_UserItem_CannotBeUpdated_409_AndStaysUnderItsOwnApproval()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        var user = fee.AddItem("02", "assignee travel", 400m, requiresApproval: true);
        user.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, user.Id, "02", "admin edit", 900m), default));

        Assert.Equal("FeeItemNotAdminOwned", ex.Code);
        Assert.Equal(FeeItemSource.User, user.Source);
        Assert.Equal(400m, user.FeeAmount);
        Assert.Equal("Pending", user.ApprovalStatus);
    }

    [Fact]
    public async Task AdminStage_UserItem_CannotBeRemoved_409()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        var user = fee.AddItem("02", "assignee travel", 400m);
        user.MarkAsUserAdded();
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Remove.Handle(new RemoveFeeItemCommand(appraisalId, fee.Id, user.Id), default));

        Assert.Equal("FeeItemNotAdminOwned", ex.Code);
        Assert.Single(fee.Items);
    }

    // ── Current assignment election ──

    [Fact]
    public async Task AdminStage_AddWithoutFeeId_UsesTheNewestRow_EvenWhenAnOlderOneHasAnAssignedAt()
    {
        // A Pending row has no AssignedAt; ordering by it alone would pick the older assigned row (which has a fee,
        // so the add would silently land there and be refused as "after assignment").
        var appraisal = await AddAppraisalAsync(_db, AppraisalTypes.New);
        var older = await AddAssignmentAsync(_db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        await AddFeeAsync(_db, older.Id);
        var newer = await AddAssignmentAsync(_db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 2, 1));

        var result = await Add.Handle(AddCmd(appraisal.Id, null, "02", 500m), default);
        await _db.SaveChangesAsync();

        var fee = await _db.AppraisalFees.Include(f => f.Items).SingleAsync(f => f.Id == result.FeeId);
        Assert.Equal(newer.Id, fee.AssignmentId);
        Assert.Equal(FeeItemSource.Admin, Assert.Single(fee.Items).Source);
    }

    // ── The system base is not the assignee's to edit ──

    [Fact]
    public async Task AfterAssignment_SystemBase_CannotBeUpdatedByTheAssignee_409()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: true);
        var system = fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Update.Handle(new UpdateFeeItemCommand(appraisalId, fee.Id, system.Id, "01", "my base", 1_000m), default));

        Assert.Equal("FeeBaseSetBySystem", ex.Code);
        Assert.Equal(2_500m, system.FeeAmount);
        Assert.Equal(FeeItemSource.System, system.Source);
    }

    // ── An all-zero fee id is an id ──

    [Fact]
    public async Task AllZeroFeeId_IsA404_NotTheAwaitingAssignmentFee()
    {
        var (appraisalId, _) = await SeedAsync(assigned: false);

        await Assert.ThrowsAsync<NotFoundException>(() => Add.Handle(AddCmd(appraisalId, Guid.Empty, "02", 500m), default));

        Assert.Empty(_db.ChangeTracker.Entries<AppraisalFeeItem>());
    }

    // ── The header (absorb) save at the admin stage ──

    private UpdateFeeCommandHandler Header => new(_db);

    [Fact]
    public async Task AdminStage_HeaderSave_KeepsAnAbsorbAboveThePartialTotal_InsteadOfFailing()
    {
        var (appraisalId, fee) = await SeedAsync(assigned: false);
        fee.SetBankAbsorb(3_000m);
        await Add.Handle(AddCmd(appraisalId, fee.Id, "02", 500m), default); // partial total 535
        await _db.SaveChangesAsync();

        // The page re-sends the stored absorb on every save.
        await Header.Handle(new UpdateFeeCommand(fee.Id, "02", 3_000m), default);

        Assert.Equal(3_000m, fee.BankAbsorbAmount);
        Assert.Equal(0m, fee.CustomerPayableAmount);
        Assert.Equal("02", fee.FeePaymentType);
    }

    [Fact]
    public async Task AfterAssignment_HeaderSave_AboveTheTotal_Is400WithACode_Not500()
    {
        var (_, fee) = await SeedAsync(assigned: true);
        fee.AddItem("01", "system base", 2_500m); // total 2,675
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Header.Handle(new UpdateFeeCommand(fee.Id, "02", 5_000m), default));

        Assert.Equal("BankAbsorbExceedsTotal", ex.Code);
        Assert.Equal(0m, fee.BankAbsorbAmount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeaderSave_NegativeAbsorb_Is400WithACode_AtEveryStage(bool assigned)
    {
        var (_, fee) = await SeedAsync(assigned);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            Header.Handle(new UpdateFeeCommand(fee.Id, "02", -1m), default));

        Assert.Equal("BankAbsorbNegative", ex.Code);
    }

    [Fact]
    public async Task AfterAssignment_HeaderSave_WithinTheTotal_IsApplied()
    {
        var (_, fee) = await SeedAsync(assigned: true);
        fee.AddItem("01", "system base", 2_500m);
        await _db.SaveChangesAsync();

        await Header.Handle(new UpdateFeeCommand(fee.Id, "02", 1_000m), default);

        Assert.Equal(1_000m, fee.BankAbsorbAmount);
        Assert.Equal(1_675m, fee.CustomerPayableAmount);
    }

    // ── Races ──

    private sealed class RacingDb(DbContextOptions<AppraisalDbContext> options) : AppraisalDbContext(options)
    {
        /// <summary>Runs once, just before the next SaveChanges: the other request's write landing first.</summary>
        public Func<Task>? RivalWrite { get; set; }

        /// <summary>When set, that save then fails the way a unique-index violation does.</summary>
        public bool FailAfterRival { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (RivalWrite is { } rival)
            {
                RivalWrite = null;
                await rival();
                if (FailAfterRival)
                    throw new DbUpdateException("unique index violated");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private static (DbContextOptions<AppraisalDbContext> Options, RacingDb Db) NewRacingDb()
    {
        var options = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return (options, new RacingDb(options));
    }

    [Fact]
    public async Task ConcurrentFirstAdd_LosingTheFeeRowRace_ReloadsTheWinnersRow_Once()
    {
        var (options, db) = NewRacingDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: false);
        Guid winnerFeeId = default;
        db.FailAfterRival = true;
        db.RivalWrite = async () =>
        {
            await using var other = new AppraisalDbContext(options);
            var winner = AppraisalFee.Create(assignment.Id);
            winnerFeeId = winner.Id;
            other.AppraisalFees.Add(winner);
            await other.SaveChangesAsync();
        };

        var result = await new AddFeeItemCommandHandler(db, _sender, _user).Handle(AddCmd(appraisal.Id, null, "02", 500m), default);
        await db.SaveChangesAsync();

        Assert.Equal(winnerFeeId, result.FeeId);
        Assert.Equal(1, await db.AppraisalFees.CountAsync());
        var fee = await db.AppraisalFees.Include(f => f.Items).SingleAsync();
        Assert.Equal(FeeItemSource.Admin, Assert.Single(fee.Items).Source);
    }

    [Fact]
    public async Task FirstAddFailure_ThatIsNotARace_IsNotSwallowed()
    {
        var (_, db) = NewRacingDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        await AddAssignmentAsync(db, appraisal.Id, assigned: false);
        db.FailAfterRival = true;
        db.RivalWrite = () => Task.CompletedTask; // nobody else wrote a row

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new AddFeeItemCommandHandler(db, _sender, _user).Handle(AddCmd(appraisal.Id, null, "02", 500m), default));
    }

    [Fact]
    public async Task ConcurrentAddOfTheBaseItem_IsCaughtByTheRecheck_409()
    {
        var (options, db) = NewRacingDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var assignment = await AddAssignmentAsync(db, appraisal.Id, assigned: false);
        var fee = await AddFeeAsync(db, assignment.Id);
        db.RivalWrite = async () =>
        {
            await using var other = new AppraisalDbContext(options);
            var rivalFee = await other.AppraisalFees.Include(f => f.Items).SingleAsync();
            rivalFee.AddItem("01", "rival base", 3_000m).MarkAsAdminSet();
            await other.SaveChangesAsync();
        };

        // Our snapshot has no 01, so the up-front check passes; the post-write recheck sees both.
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new AddFeeItemCommandHandler(db, _sender, _user).Handle(AddCmd(appraisal.Id, fee.Id), default));

        Assert.Equal("FeeBaseItemDuplicated", ex.Code);
    }
}
