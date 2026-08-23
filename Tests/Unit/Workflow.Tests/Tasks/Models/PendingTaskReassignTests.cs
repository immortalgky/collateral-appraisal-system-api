using FluentAssertions;
using Workflow.Tasks.Models;
using Workflow.Workflow.Events;
using TaskStatus = Workflow.Tasks.ValueObjects.TaskStatus;

namespace Workflow.Tests.Tasks.Models;

public class PendingTaskReassignTests
{
    private static PendingTask CreateAssignedTask(string assignedTo = "alice", DateTime? dueAt = null, string? slaStatus = "OnTime")
    {
        var task = PendingTask.Create(
            correlationId: Guid.NewGuid(),
            taskName: "Test Task",
            assignedTo: assignedTo,
            assignedType: "1",
            assignedAt: DateTime.Now.AddHours(-2),
            workflowInstanceId: Guid.NewGuid(),
            activityId: "appraisal-checker",
            dueAt: dueAt ?? DateTime.Now.AddDays(1));
        return task;
    }

    // ── Event-raising invariants ──────────────────────────────────────────────

    [Fact]
    public void Reassign_WithSupervisorFlag_RaisesPendingTaskReassignedDomainEvent()
    {
        var task = CreateAssignedTask("alice");

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.DomainEvents.Should().ContainSingle(e => e is PendingTaskReassignedDomainEvent);
    }

    [Fact]
    public void Reassign_WithSupervisorFlag_EventCarriesCorrectData()
    {
        var dueAt = DateTime.Now.AddDays(2);
        var task = CreateAssignedTask("alice", dueAt: dueAt);
        var workflowInstanceId = task.WorkflowInstanceId;
        var taskId = task.Id;

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        var evt = (PendingTaskReassignedDomainEvent)task.DomainEvents.Single(e => e is PendingTaskReassignedDomainEvent);
        evt.TaskId.Should().Be(taskId);
        evt.PreviousAssignedTo.Should().Be("alice");
        evt.NewAssignedTo.Should().Be("bob");
        evt.WorkflowInstanceId.Should().Be(workflowInstanceId);
        evt.ActivityId.Should().Be("appraisal-checker");
        evt.DueAt.Should().BeCloseTo(dueAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Reassign_WithoutSupervisorFlag_RaisesNoDomainEvent()
    {
        // This is the existing ClaimTask path — must be event-silent
        var task = CreateAssignedTask("alice");

        task.Reassign("pool-group", "2");

        task.DomainEvents.Should().NotContain(e => e is PendingTaskReassignedDomainEvent);
    }

    [Fact]
    public void Reassign_WithNullRaiseEventFor_RaisesNoDomainEvent()
    {
        var task = CreateAssignedTask("alice");

        task.Reassign("bob", "1", raiseEventFor: null);

        task.DomainEvents.Should().BeEmpty();
    }

    // ── Field preservation/clearing invariants ────────────────────────────────

    [Fact]
    public void Reassign_PreservesDueAt()
    {
        var dueAt = DateTime.Now.AddDays(3);
        var task = CreateAssignedTask("alice", dueAt: dueAt);

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.DueAt.Should().BeCloseTo(dueAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Reassign_PreservesSlaStatus()
    {
        var task = CreateAssignedTask("alice");
        // SlaStatus is "OnTime" after Create with a dueAt

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.SlaStatus.Should().Be("OnTime");
    }

    [Fact]
    public void Reassign_PreservesSlaBreachedAt_WhenBreached()
    {
        var task = CreateAssignedTask("alice");
        var breachTime = DateTime.Now.AddMinutes(-5);
        task.MarkBreached(breachTime);

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.SlaBreachedAt.Should().BeCloseTo(breachTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Reassign_ClearsWorkingBy()
    {
        var task = CreateAssignedTask("alice");
        task.StartWorking("alice", DateTime.Now);
        task.ClearDomainEvents(); // clear the StartWorking event for isolation

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.WorkingBy.Should().BeNull();
    }

    [Fact]
    public void Reassign_ClearsLockedAt()
    {
        var task = CreateAssignedTask("alice");
        task.Lock("alice");

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.LockedAt.Should().BeNull();
    }

    [Fact]
    public void Reassign_SetsTaskStatusToAssigned()
    {
        var task = CreateAssignedTask("alice");
        task.StartWorking("alice", DateTime.Now);

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.TaskStatus.Should().Be(TaskStatus.Assigned);
    }

    [Fact]
    public void Reassign_UpdatesAssignedTo()
    {
        var task = CreateAssignedTask("alice");

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.AssignedTo.Should().Be("bob");
    }

    [Fact]
    public void Reassign_DoesNotChangeAssignedAt()
    {
        var task = CreateAssignedTask("alice");
        var originalAssignedAt = task.AssignedAt;

        task.Reassign("bob", "1", raiseEventFor: "supervisor");

        task.AssignedAt.Should().Be(originalAssignedAt);
    }

    // ── Holder clock vs SLA clock ─────────────────────────────────────────────

    [Fact]
    public void Create_StampsAssigneeAssignedAtToAssignedAt()
    {
        var task = CreateAssignedTask("alice");

        task.AssigneeAssignedAt.Should().Be(task.AssignedAt);
    }

    [Fact]
    public void Reassign_WithHolderChangedAt_RestampsAssigneeAssignedAtButNotAssignedAt()
    {
        var task = CreateAssignedTask("alice");
        var originalAssignedAt = task.AssignedAt;
        var handedOverAt = DateTime.Now;

        task.Reassign("bob", "1", raiseEventFor: "supervisor", holderChangedAt: handedOverAt);

        task.AssigneeAssignedAt.Should().Be(handedOverAt,
            because: "the incoming holder's clock starts at the hand-off, which is what history timelines order on");
        task.AssignedAt.Should().Be(originalAssignedAt,
            because: "the SLA clock must not restart on reassignment");
    }

    [Fact]
    public void Reassign_WithoutHolderChangedAt_LeavesAssigneeAssignedAtAlone()
    {
        // Claim / implicit-assign / fan-out advance take this overload. They write no audit row,
        // so their single history row keeps reporting the original start.
        var task = CreateAssignedTask("alice");
        var original = task.AssigneeAssignedAt;

        task.Reassign("bob", "1");

        task.AssigneeAssignedAt.Should().Be(original);
    }

    [Fact]
    public void CreateAuditFromPendingTask_CarriesOutgoingHoldersStamp_WhileTaskMovesOn()
    {
        // The reassign handler snapshots the audit row BEFORE mutating, so the two rows end up with
        // a strictly increasing AssigneeAssignedAt chain even though AssignedAt is identical.
        var task = CreateAssignedTask("alice");
        var outgoingStart = task.AssigneeAssignedAt;
        var handedOverAt = DateTime.Now;

        var auditRow = CompletedTask.CreateAuditFromPendingTask(task, "Reassigned", handedOverAt);
        task.Reassign("bob", "1", raiseEventFor: "supervisor", holderChangedAt: handedOverAt);

        auditRow.AssigneeAssignedAt.Should().Be(outgoingStart);
        auditRow.AssignedAt.Should().Be(task.AssignedAt);
        auditRow.AssigneeAssignedAt.Should().BeBefore(task.AssigneeAssignedAt);
    }

    [Fact]
    public void Reassign_WithHolderChangedAt_ClearsOpenedAt_AndAuditRowKeepsTheOutgoingHoldersOpenTime()
    {
        // OpenedAt uses ??= (stamped once, never overwritten). Without the reset, the incoming holder
        // inherits an open time they were never present for, and StartWorking can never re-stamp it.
        // Explicit, well-separated instants: two DateTime.Now reads microseconds apart can land on
        // the same tick on a coarse clock, which would make the final assertion flaky.
        var aliceOpenedAt = DateTime.Now.AddHours(-5);
        var handedOverAt = DateTime.Now.AddHours(-2);
        var bobOpenedAt = DateTime.Now.AddHours(-1);

        var task = CreateAssignedTask("alice");
        task.StartWorking("alice", aliceOpenedAt);
        task.OpenedAt.Should().Be(aliceOpenedAt);

        var auditRow = CompletedTask.CreateAuditFromPendingTask(task, "Reassigned", handedOverAt);
        task.Reassign("bob", "1", raiseEventFor: "supervisor", holderChangedAt: handedOverAt);

        auditRow.OpenedAt.Should().Be(aliceOpenedAt,
            because: "the audit row is snapshotted before the mutation, so it keeps alice's open time");
        task.OpenedAt.Should().BeNull(
            because: "bob has not opened the task yet — StartWorking must be free to stamp it");

        task.StartWorking("bob", bobOpenedAt);
        task.OpenedAt.Should().Be(bobOpenedAt).And.NotBe(aliceOpenedAt);
    }

    [Fact]
    public void Reassign_WithoutHolderChangedAt_KeepsOpenedAt()
    {
        // Claim / implicit-assign / fan-out advance: no audit row, no hand-off semantics.
        var task = CreateAssignedTask("alice");
        var openedAt = DateTime.Now.AddHours(-1);
        task.StartWorking("alice", openedAt);

        task.Reassign("bob", "1");

        task.OpenedAt.Should().Be(openedAt);
    }

    [Fact]
    public void StartWorking_ReplacesAnOpenedAtThatPredatesTheHandOff()
    {
        // Reproduces a row handed off before Reassign started clearing OpenedAt: the previous
        // holder's stamp is still there, and ??= alone would keep it forever. Reflection is the only
        // way to build this state — the aggregate no longer allows it, which is the fix; the rows
        // already sitting in the database from before it are what this guard exists for.
        var task = CreateAssignedTask("alice");
        var staleOpenedAt = DateTime.Now.AddDays(-30);   // alice opened it last month
        var handedOverAt = DateTime.Now.AddDays(-3);     // bob received it three days ago
        var bobOpenedAt = DateTime.Now.AddDays(-1);      // bob opens it today
        SetPrivate(task, nameof(PendingTask.OpenedAt), staleOpenedAt);
        SetPrivate(task, nameof(PendingTask.AssigneeAssignedAt), handedOverAt);

        task.StartWorking("bob", bobOpenedAt);

        task.OpenedAt.Should().Be(bobOpenedAt, because: "the stale stamp belongs to alice");
        task.OpenedAt.Should().BeAfter(handedOverAt,
            because: "an open time earlier than the hand-off cannot belong to the current holder");
    }

    private static void SetPrivate(PendingTask task, string propertyName, object value) =>
        typeof(PendingTask).GetProperty(propertyName)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(task, [value]);

    // ── PK collision guard: audit row vs completion row ───────────────────────

    [Fact]
    public void CreateAuditFromPendingTask_ThenCreateFromPendingTask_ProduceDifferentIds()
    {
        // Simulates "reassign task, later complete it":
        // 1. Supervisor reassigns → audit snapshot with fresh Id
        // 2. Reassigned user completes → normal CompletedTask reusing PendingTask.Id
        // Both rows must have distinct Ids or SaveChanges would throw a PK violation.
        var task = CreateAssignedTask("alice");

        var auditRow = CompletedTask.CreateAuditFromPendingTask(task, "Reassigned", DateTime.Now);
        var completionRow = CompletedTask.CreateFromPendingTask(task, "Completed", DateTime.Now.AddHours(1));

        auditRow.Id.Should().NotBe(completionRow.Id,
            because: "audit row must mint a fresh Id to avoid PK collision when the task is later completed normally");
        completionRow.Id.Should().Be(task.Id,
            because: "the normal completion path continues to use PendingTask.Id as the completed-task Id");
    }
}
