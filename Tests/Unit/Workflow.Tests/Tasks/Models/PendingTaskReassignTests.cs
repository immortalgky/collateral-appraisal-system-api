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
        task.StartWorking("alice");
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
        task.StartWorking("alice");

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
