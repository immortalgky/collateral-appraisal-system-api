using Appraisal.Application.EventHandlers;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.EventHandlers;

/// <summary>
/// Regression coverage for the internal-assignment InProgress race: InternalAssignedIntegrationEvent
/// (which stamps Assigned) and the separately-endpointed WorkflowTransitioned event (which calls
/// StartWork, guarded on == Assigned) both fire on the int-appraisal-execution landing. Because the
/// Assign event lands AFTER the transition, the status handler's StartWork saw Pending and dropped the
/// InProgress transition. The fix advances to InProgress inside THIS handler, so it no longer depends
/// on cross-endpoint ordering.
/// </summary>
public class InternalAssignedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Consume_InitialInternalAssignment_AdvancesToInProgress_WithoutWorkflowTransitioned()
    {
        // Arrange: appraisal with a single Pending admin assignment (as created at appraisal creation).
        var appraisal = AppraisalAggregate.Create(
            requestId: Guid.NewGuid(),
            appraisalType: "Initial",
            priority: "Normal",
            now: DateTime.Now);
        appraisal.AssignAdmin();
        var assignment = appraisal.Assignments.Single();
        Assert.Equal(AssignmentStatus.Pending, assignment.AssignmentStatus);

        var handler = BuildHandler(appraisal);
        var ctx = BuildContext(new InternalAssignedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            AssigneeUserId = "P0001",
            InternalAppraiserId = "P0002",
            AssignmentMethod = "RoundRobin",
            InternalFollowupAssignmentMethod = "RoundRobin",
            CompletedBy = "system"
        });

        // Act
        await handler.Consume(ctx);

        // Assert: reached InProgress purely from this handler — no WorkflowTransitioned StartWork ran.
        Assert.Equal(AssignmentType.Internal, assignment.AssignmentType);
        Assert.Equal(AssignmentStatus.InProgress, assignment.AssignmentStatus);
        Assert.NotNull(assignment.StartedAt);
    }

    [Fact]
    public async Task Consume_RoutebackRefire_DoesNotForceInProgress()
    {
        // Arrange: assignment already on the bank-review side (UnderReview), i.e. a routeback re-fire
        // of InternalAssigned. wasPending is false, so StartWork must NOT run.
        var appraisal = AppraisalAggregate.Create(
            Guid.NewGuid(), "Initial", "Normal", DateTime.Now);
        appraisal.AssignAdmin();
        var assignment = appraisal.Assignments.Single();
        assignment.Assign(assignmentType: "Internal", assigneeUserId: "P0001",
            assignmentMethod: "RoundRobin", assignedBy: "system");
        assignment.StartWork();       // InProgress
        assignment.MarkUnderReview(); // UnderReview
        Assert.Equal(AssignmentStatus.UnderReview, assignment.AssignmentStatus);

        var handler = BuildHandler(appraisal);
        var ctx = BuildContext(new InternalAssignedIntegrationEvent
        {
            AppraisalId = appraisal.Id,
            AssigneeUserId = "P0001",
            AssignmentMethod = "RoundRobin",
            CompletedBy = "system"
        });

        // Act
        await handler.Consume(ctx);

        // Assert: the wasPending guard means the handler does not force InProgress on a re-fire.
        Assert.NotEqual(AssignmentStatus.InProgress, assignment.AssignmentStatus);
    }

    // ── Helpers ──

    private static InternalAssignedIntegrationEventHandler BuildHandler(AppraisalAggregate appraisal)
    {
        var repo = Substitute.For<IAppraisalRepository>();
        repo.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(appraisal);

        var uow = Substitute.For<IAppraisalUnitOfWork>();

        var feeService = Substitute.For<IAssignmentFeeService>();
        feeService.ResolveSourceForAppraisalAsync(
                Arg.Any<AppraisalAggregate>(),
                Arg.Any<AssignmentFeeSource>(),
                Arg.Any<CancellationToken>())
            .Returns(new AssignmentFeeSource.TierBased());

        // Real InboxGuard, but the provider-less context is never touched: BuildContext sets a null
        // MessageId, so TryClaimAsync/MarkAsProcessedAsync both short-circuit before any DB access.
        var db = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>().Options);
        var inboxGuard = new InboxGuard<AppraisalDbContext>(
            db,
            NullLogger<InboxGuard<AppraisalDbContext>>.Instance,
            Substitute.For<IDateTimeProvider>());

        return new InternalAssignedIntegrationEventHandler(
            repo, uow, feeService,
            NullLogger<InternalAssignedIntegrationEventHandler>.Instance,
            inboxGuard);
    }

    private static ConsumeContext<InternalAssignedIntegrationEvent> BuildContext(
        InternalAssignedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<InternalAssignedIntegrationEvent>>();
        ctx.Message.Returns(message);
        ctx.MessageId.Returns((Guid?)null); // null → InboxGuard is a complete no-op (no DB access)
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }
}
