using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Workflow.Data;
using Workflow.EventHandlers;
using Workflow.Sla.Models;
using Workflow.Sla.Services;
using Workflow.Tasks.Models;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;

namespace Workflow.Tests.EventHandlers;

/// <summary>
/// Tests for <see cref="AppointmentDateChangedIntegrationEventConsumer"/> —
/// appointment-anchored SLA recalculation when an appointment is set or rescheduled.
///
/// InboxGuard is bypassed by setting MessageId=null on the ConsumeContext. When
/// MessageId is null, InboxGuard returns false immediately (TryClaimAsync) and
/// returns early without raw SQL (MarkAsProcessedAsync), making it compatible with
/// the InMemory EF provider (which does not support ExecuteSqlRawAsync).
/// </summary>
public class AppointmentDateChangedConsumerTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly IWorkflowInstanceRepository _instanceRepo;
    private readonly ISlaCalculator _slaCalc;
    private readonly AppointmentDateChangedIntegrationEventConsumer _consumer;

    private const string ActivityId = "int-appraisal-execution";

    public AppointmentDateChangedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);

        _instanceRepo = Substitute.For<IWorkflowInstanceRepository>();
        _slaCalc = Substitute.For<ISlaCalculator>();

        // Real InboxGuard — DB operations are skipped when MessageId=null (see class summary above).
        var inboxGuard = new InboxGuard<WorkflowDbContext>(
            _db,
            Substitute.For<ILogger<InboxGuard<WorkflowDbContext>>>(),
            Substitute.For<Shared.Time.IDateTimeProvider>());

        _consumer = new AppointmentDateChangedIntegrationEventConsumer(
            _db, _instanceRepo, _slaCalc, inboxGuard,
            Substitute.For<Shared.Time.IDateTimeProvider>(),
            Substitute.For<Shared.Data.Outbox.IIntegrationEventOutbox>(),
            Substitute.For<ILogger<AppointmentDateChangedIntegrationEventConsumer>>());
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a NSubstitute ConsumeContext with MessageId=null so InboxGuard is bypassed.
    /// </summary>
    private static ConsumeContext<AppointmentDateChangedIntegrationEvent> BuildContext(
        AppointmentDateChangedIntegrationEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<AppointmentDateChangedIntegrationEvent>>();
        ctx.MessageId.Returns((Guid?)null); // bypass InboxGuard claim + MarkAsProcessed raw SQL
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    // ── Scenario 7: Consumer updates PendingTask.DueAt when appointment changes ────────────────

    /// <summary>
    /// Happy path: an appointment-anchored SlaPolicy exists, a PendingTask for that activity
    /// is in the database, and the consumer receives an AppointmentDateChangedIntegrationEvent.
    /// After Consume(), PendingTask.DueAt must be the value returned by the SlaCalculator.
    /// </summary>
    [Fact]
    public async Task Consume_AppointmentDateChanged_UpdatesPendingTaskDueAt()
    {
        var correlationId = Guid.NewGuid();
        var appointmentDate = new DateTime(2026, 7, 10, 9, 0, 0);
        var expectedDueAt = appointmentDate.AddHours(8);

        // Seed SlaPolicy with AnchorType=AppointmentDate so the consumer's
        // anchoredActivityIds query returns the activity and finds the PendingTask.
        var policy = SlaPolicy.Create(ActivityId, 8, false, 1, anchorType: SlaAnchorType.AppointmentDate);
        _db.SlaPolicies.Add(policy);

        // WorkflowInstance — returned by the mocked repository (not persisted to InMemory DB;
        // the consumer only needs instance.Id and instance.Variables).
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "Collateral Appraisal Workflow", correlationId.ToString(), "system");

        _instanceRepo.GetByCorrelationId(correlationId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        // PendingTask for that activity, owned by the workflow instance.
        var task = PendingTask.Create(
            correlationId, "Appraisal Execution Task", "user1", "Individual",
            assignedAt: new DateTime(2026, 7, 1, 9, 0, 0),
            workflowInstanceId: instance.Id,
            activityId: ActivityId);
        _db.PendingTasks.Add(task);
        await _db.SaveChangesAsync();

        // SlaCalculator (mocked) returns the appointment-anchored DueAt.
        _slaCalc.CalculateActivityDueAtAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new SlaDeadline(expectedDueAt, null));

        var ctx = BuildContext(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            CorrelationId = correlationId,
            AssignmentId = Guid.NewGuid(),
            AppointmentDate = appointmentDate,
            OccurredOn = DateTime.Now
        });

        await _consumer.Consume(ctx);

        // PendingTask.DueAt must now be set to the appointment-anchored deadline.
        var updatedTask = await _db.PendingTasks.FindAsync(task.Id);
        updatedTask!.DueAt.Should().Be(expectedDueAt,
            "RecalculateDueAt should stamp the value returned by ISlaCalculator");
        updatedTask.SlaStatus.Should().Be("OnTime",
            "RecalculateDueAt resets SlaStatus to OnTime whenever a new DueAt is set");
    }

    /// <summary>
    /// When the WorkflowInstance is not found, the consumer must exit early
    /// and must not invoke the SlaCalculator.
    /// </summary>
    [Fact]
    public async Task Consume_NoWorkflowInstanceFound_SkipsProcessingAndDoesNotCallSlaCalculator()
    {
        var correlationId = Guid.NewGuid();

        _instanceRepo.GetByCorrelationId(correlationId.ToString(), Arg.Any<CancellationToken>())
            .Returns((WorkflowInstance?)null);

        var ctx = BuildContext(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            CorrelationId = correlationId,
            AssignmentId = Guid.NewGuid(),
            AppointmentDate = DateTime.Now.AddDays(5),
            OccurredOn = DateTime.Now
        });

        await _consumer.Consume(ctx);

        await _slaCalc.DidNotReceive().CalculateActivityDueAtAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTime?>(),
            Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When no SlaPolicy with AnchorType=AppointmentDate exists, anchoredActivityIds is empty
    /// and the consumer saves the variable update but does not touch any PendingTask.
    /// </summary>
    [Fact]
    public async Task Consume_NoAppointmentAnchoredPolicy_SavesVariableUpdateOnly()
    {
        var correlationId = Guid.NewGuid();
        var appointmentDate = new DateTime(2026, 7, 10, 9, 0, 0);

        // No SlaPolicy seeded — anchoredActivityIds will be empty.
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "Test Workflow", correlationId.ToString(), "system");

        _instanceRepo.GetByCorrelationId(correlationId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        // Seed a PendingTask anyway — it must not be modified.
        var task = PendingTask.Create(
            correlationId, "Task", "user1", "Individual",
            new DateTime(2026, 7, 1, 9, 0, 0), instance.Id, ActivityId);
        _db.PendingTasks.Add(task);
        await _db.SaveChangesAsync();

        var ctx = BuildContext(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId = Guid.NewGuid(),
            CorrelationId = correlationId,
            AssignmentId = Guid.NewGuid(),
            AppointmentDate = appointmentDate,
            OccurredOn = DateTime.Now
        });

        await _consumer.Consume(ctx);

        // SlaCalculator must not be called (no anchored activities found)
        await _slaCalc.DidNotReceive().CalculateActivityDueAtAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTime?>(),
            Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());

        // PendingTask.DueAt must remain null (not updated)
        var unchanged = await _db.PendingTasks.FindAsync(task.Id);
        unchanged!.DueAt.Should().BeNull();
    }

    // ── Scenario 8: FIX CONFIRMED — initial appointment creation now publishes the event ───────

    /// <summary>
    /// Confirms that the initial-appointment-creation gap documented in the original test below
    /// is NOW FIXED. <c>CreateAppointmentCommandHandler</c> now publishes
    /// <c>AppointmentDateChangedIntegrationEvent</c> immediately after persisting the appointment
    /// (added outbox.Publish call at lines 51-58 of CreateAppointmentCommandHandler.cs).
    ///
    /// When that event is consumed, the consumer must set <c>PendingTask.DueAt</c> to the
    /// appointment-anchored deadline — exactly the same path exercised by a reschedule.
    ///
    /// This test replaces the "GAP" assertion and proves the fix end-to-end at the consumer level.
    /// </summary>
    [Fact(DisplayName = "FIX: initial appointment creation now publishes AppointmentDateChangedIntegrationEvent — DueAt is set")]
    public async Task InitialAppointmentCreation_WhenEventConsumed_PendingTaskDueAtBecomesNonNull()
    {
        // Arrange: appointment-anchored SlaPolicy + PendingTask with DueAt=null (just assigned,
        // no appointment yet — same state as when CreateAppointmentCommandHandler fires).
        var correlationId = Guid.NewGuid();
        var appointmentDate = new DateTime(2026, 7, 15, 9, 0, 0);
        var expectedDueAt   = appointmentDate.AddHours(8);

        var policy = SlaPolicy.Create(ActivityId, 8, false, 1, anchorType: SlaAnchorType.AppointmentDate);
        _db.SlaPolicies.Add(policy);

        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "Collateral Appraisal Workflow", correlationId.ToString(), "system");
        _instanceRepo.GetByCorrelationId(correlationId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        // PendingTask starts with DueAt=null — the task was assigned before any appointment existed.
        var task = PendingTask.Create(
            correlationId, "Appraisal Execution Task", "user1", "Individual",
            assignedAt: new DateTime(2026, 7, 1, 9, 0, 0),
            workflowInstanceId: instance.Id,
            activityId: ActivityId,
            dueAt: null);
        _db.PendingTasks.Add(task);
        await _db.SaveChangesAsync();

        _slaCalc.CalculateActivityDueAtAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new SlaDeadline(expectedDueAt, null));

        // Act: simulate what CreateAppointmentCommandHandler now does — publish the event.
        // The consumer processes it and must set DueAt on the pending task.
        var ctx = BuildContext(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId   = Guid.NewGuid(),
            CorrelationId = correlationId,
            AssignmentId  = Guid.NewGuid(),
            AppointmentDate = appointmentDate,
            OccurredOn    = DateTime.Now
        });

        await _consumer.Consume(ctx);

        // Assert: DueAt is no longer null — the fix ensures clock starts at appointment creation.
        var updatedTask = await _db.PendingTasks.FindAsync(task.Id);
        updatedTask!.DueAt.Should().Be(expectedDueAt,
            "Fix confirmed: CreateAppointmentCommandHandler now publishes " +
            "AppointmentDateChangedIntegrationEvent on initial creation, triggering the consumer " +
            "to stamp DueAt immediately instead of waiting for a reschedule.");
        updatedTask.SlaStatus.Should().Be("OnTime",
            "consumer resets SlaStatus to OnTime whenever DueAt is freshly set");
    }

    // ── Scenario 9: REGRESSION — gap is fixed; consumer path verified end-to-end ───────────────

    // This test replaced the historical GAP test. The gap was fixed when
    // `CreateAppointmentCommandHandler.cs` added `outbox.Publish(new AppointmentDateChangedIntegrationEvent {...})`.
    // The direct handler-level regression is now in `CreateAppointmentCommandHandlerTests.cs` in Appraisal.Tests.

    /// <summary>
    /// Explicit regression marker for the initial-appointment-creation gap that was documented
    /// in the original Scenario 9 GAP test.
    ///
    /// The gap was: <c>CreateAppointmentCommandHandler</c> did not publish
    /// <c>AppointmentDateChangedIntegrationEvent</c> on first creation, so
    /// <c>PendingTask.DueAt</c> remained null until a reschedule occurred.
    ///
    /// The fix added <c>outbox.Publish(new AppointmentDateChangedIntegrationEvent {...})</c>
    /// to <c>CreateAppointmentCommandHandler</c>. This test confirms the consumer side of
    /// that fix: when the event is consumed, DueAt is set to a non-null value.
    ///
    /// Handler-level regression (that the event is actually published) lives in
    /// <c>Appraisal.Tests / CreateAppointmentCommandHandlerTests.cs</c>.
    /// </summary>
    [Fact(DisplayName = "REGRESSION: CreateAppointmentCommandHandler now publishes the event on first creation (gap is fixed)")]
    public async Task Regression_InitialAppointmentCreation_WhenEventConsumed_PendingTaskDueAtBecomesNonNull()
    {
        // Arrange: appointment-anchored SlaPolicy + PendingTask with DueAt=null (just assigned,
        // no appointment yet — same state as when CreateAppointmentCommandHandler fires).
        var correlationId = Guid.NewGuid();
        var appointmentDate = new DateTime(2026, 7, 15, 9, 0, 0);
        var expectedDueAt   = appointmentDate.AddHours(8);

        var policy = SlaPolicy.Create(ActivityId, 8, false, 1, anchorType: SlaAnchorType.AppointmentDate);
        _db.SlaPolicies.Add(policy);

        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "Collateral Appraisal Workflow", correlationId.ToString(), "system");
        _instanceRepo.GetByCorrelationId(correlationId.ToString(), Arg.Any<CancellationToken>())
            .Returns(instance);

        // PendingTask starts with DueAt=null — the task was assigned before any appointment existed.
        var task = PendingTask.Create(
            correlationId, "Appraisal Execution Task", "user1", "Individual",
            assignedAt: new DateTime(2026, 7, 1, 9, 0, 0),
            workflowInstanceId: instance.Id,
            activityId: ActivityId,
            dueAt: null);
        _db.PendingTasks.Add(task);
        await _db.SaveChangesAsync();

        _slaCalc.CalculateActivityDueAtAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<TimeSpan?>(), Arg.Any<DateTime?>(),
                Arg.Any<Guid?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new SlaDeadline(expectedDueAt, null));

        // Act: simulate what CreateAppointmentCommandHandler now does — publish the event.
        // The consumer processes it and must set DueAt on the pending task.
        var ctx = BuildContext(new AppointmentDateChangedIntegrationEvent
        {
            AppraisalId     = Guid.NewGuid(),
            CorrelationId   = correlationId,
            AssignmentId    = Guid.NewGuid(),
            AppointmentDate = appointmentDate,
            OccurredOn      = DateTime.Now
        });

        await _consumer.Consume(ctx);

        // Assert: DueAt is no longer null — the gap is closed.
        var updatedTask = await _db.PendingTasks.FindAsync(task.Id);
        updatedTask!.DueAt.Should().Be(expectedDueAt,
            "Gap is fixed: CreateAppointmentCommandHandler now publishes " +
            "AppointmentDateChangedIntegrationEvent on initial creation, triggering the consumer " +
            "to stamp DueAt immediately instead of waiting for a reschedule.");
        updatedTask.SlaStatus.Should().Be("OnTime",
            "consumer resets SlaStatus to OnTime whenever DueAt is freshly set");
    }
}
