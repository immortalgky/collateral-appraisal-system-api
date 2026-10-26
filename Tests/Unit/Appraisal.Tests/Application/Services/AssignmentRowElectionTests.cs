using Appraisal.Application.EventHandlers;
using Appraisal.Application.Features.Fees.AddFeeItem;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using Workflow.Contracts.FeeAppointmentApprovals;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// The admin stage (fee edits) and the consumers that assign and bill must pick the SAME assignment row, so what
/// the admin keyed is what assignment bills on. The case that used to split them: an older row that was assigned
/// (it has an AssignedAt) and a newer row awaiting assignment (it has none).
/// </summary>
public class AssignmentRowElectionTests
{
    [Fact]
    public async Task PendingNewerRowAndAssignedOlderRow_AdminEditsAndInternalAssignmentTargetTheSameRow()
    {
        var db = NewDb();
        SeedGenericLadder(db);
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var older = await AddAssignmentAsync(db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        var olderFee = await AddFeeAsync(db, older.Id, sellingPrice: 8_000_000m);
        olderFee.AddItem("01", "old base", 3_000m);
        var newer = await AddAssignmentAsync(db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 2, 1));
        await db.SaveChangesAsync();

        // 1. The admin keys an extra fee before assigning: it lands on the newer (Pending) row.
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<EvaluateFeeAppointmentApprovalQuery>(), Arg.Any<CancellationToken>())
            .Returns(new EvaluateFeeAppointmentApprovalResult(false, FeesRequireApproval: false));
        var added = await new AddFeeItemCommandHandler(db, sender, Substitute.For<ICurrentUserService>())
            .Handle(new AddFeeItemCommand(appraisal.Id, null, "02", "admin travel", 500m), default);
        await db.SaveChangesAsync();

        // 2. The internal assignment event arrives and must elect that same row.
        var aggregate = await db.Appraisals.Include(a => a.Assignments).SingleAsync(a => a.Id == appraisal.Id);
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(aggregate);
        var inbox = new InboxGuard<AppraisalDbContext>(
            new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options),
            NullLogger<InboxGuard<AppraisalDbContext>>.Instance, Substitute.For<IDateTimeProvider>());
        var handler = new InternalAssignedIntegrationEventHandler(
            repo, Substitute.For<IAppraisalUnitOfWork>(), BuildService(db),
            NullLogger<InternalAssignedIntegrationEventHandler>.Instance, inbox);
        var ctx = Substitute.For<ConsumeContext<InternalAssignedIntegrationEvent>>();
        ctx.Message.Returns(new InternalAssignedIntegrationEvent
        {
            AppraisalId = appraisal.Id, AssigneeUserId = "P0001", AssignmentMethod = "Manual", CompletedBy = "P5229"
        });
        ctx.MessageId.Returns((Guid?)null);
        ctx.CancellationToken.Returns(CancellationToken.None);

        await handler.Consume(ctx);
        await db.SaveChangesAsync();

        // The newer row was the one assigned and billed; the older row's fee was not touched.
        var newerFee = await db.AppraisalFees.Include(f => f.Items).SingleAsync(f => f.AssignmentId == newer.Id);
        Assert.Equal(newerFee.Id, added.FeeId);
        Assert.Equal(["01", "02"], newerFee.Items.Select(i => i.FeeCode).Order());
        Assert.Equal(FeeItemSource.System, newerFee.Items.Single(i => i.FeeCode == "01").Source);
        Assert.Equal(AssignmentStatus.InProgress, (await db.AppraisalAssignments.SingleAsync(a => a.Id == newer.Id)).AssignmentStatus);
        var untouched = await db.AppraisalFees.Include(f => f.Items).SingleAsync(f => f.Id == olderFee.Id);
        Assert.Equal("01", Assert.Single(untouched.Items).FeeCode);
    }

    [Fact]
    public async Task Election_PrefersTheNewestLiveRow_AndIgnoresCancelledOnes()
    {
        var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var first = await AddAssignmentAsync(db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 1, 1));
        var second = await AddAssignmentAsync(db, appraisal.Id, assigned: true, createdAt: new DateTime(2026, 2, 1));
        var pending = await AddAssignmentAsync(db, appraisal.Id, assigned: false, createdAt: new DateTime(2026, 3, 1));
        typeof(AppraisalAssignment).GetProperty(nameof(AppraisalAssignment.AssignmentStatus))!
            .SetValue(pending, AssignmentStatus.Cancelled);
        await db.SaveChangesAsync();
        var aggregate = await db.Appraisals.Include(a => a.Assignments).SingleAsync();

        Assert.Equal(second.Id, aggregate.Assignments.Current()!.Id);
        Assert.NotEqual(first.Id, aggregate.Assignments.Current()!.Id);
        // The query form (the global filter drops the cancelled row) agrees.
        Assert.Equal(second.Id, (await db.AppraisalAssignments.NewestFirst().FirstAsync()).Id);
    }

    [Fact]
    public void Election_BreaksCreatedAtTiesInSqlServerGuidOrder_NotDotNetOrder()
    {
        // SQL Server compares a uniqueidentifier by its LAST bytes first, .NET by its first ones. These two ids order
        // oppositely in the two worlds, so a .NET tie-break would pick the row the NewestFirst query does not.
        var dotNetHigher = Guid.Parse("01000000-0000-0000-0000-000000000000");
        var sqlHigher = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Assert.True(dotNetHigher.CompareTo(sqlHigher) > 0);
        Assert.True(new System.Data.SqlTypes.SqlGuid(sqlHigher).CompareTo(new System.Data.SqlTypes.SqlGuid(dotNetHigher)) > 0);

        var tie = new DateTime(2026, 2, 1);
        var a = AppraisalAssignment.Create(Guid.NewGuid(), "Internal", assignedBy: "test");
        var b = AppraisalAssignment.Create(a.AppraisalId, "Internal", assignedBy: "test");
        (a.Id, a.CreatedAt) = (dotNetHigher, tie);
        (b.Id, b.CreatedAt) = (sqlHigher, tie);

        Assert.Equal(sqlHigher, new[] { a, b }.Current()!.Id);
        Assert.Equal(sqlHigher, new[] { b, a }.Current()!.Id);
    }
}
