using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Identity;
using Shared.Time;
using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Teams;
using Workflow.Data;
using Workflow.Services.Groups;
using Workflow.Services.TaskMonitor;
using Workflow.Tasks.Features.ReassignTask;
using Workflow.Tasks.Models;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Xunit;
using TaskStatus = Workflow.Tasks.ValueObjects.TaskStatus;

namespace Workflow.Tests.Tasks.Features.ReassignTask;

public class ReassignTaskCommandHandlerTests : IDisposable
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly WorkflowDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGroupMonitoringService _groupMonitoringService;
    private readonly ITaskMonitorScope _taskMonitorScope;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IAssignmentPipeline _assignmentPipeline;
    private readonly ReassignTaskCommandHandler _handler;

    public ReassignTaskCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new WorkflowDbContext(options);

        _currentUserService = Substitute.For<ICurrentUserService>();
        _groupMonitoringService = Substitute.For<IGroupMonitoringService>();
        _taskMonitorScope = Substitute.For<ITaskMonitorScope>();
        _instanceRepository = Substitute.For<IWorkflowInstanceRepository>();
        _assignmentPipeline = Substitute.For<IAssignmentPipeline>();

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(DateTime.Now);

        var logger = Substitute.For<ILogger<ReassignTaskCommandHandler>>();

        _handler = new ReassignTaskCommandHandler(
            _dbContext,
            _currentUserService,
            _groupMonitoringService,
            _taskMonitorScope,
            _instanceRepository,
            _assignmentPipeline,
            dateTimeProvider,
            logger);

        // Default happy-path stubs
        _currentUserService.Username.Returns("supervisor");
        _groupMonitoringService.IsUserSupervisedByAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _taskMonitorScope.IsTargetInScopeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helper factories ──────────────────────────────────────────────────────

    private async Task<PendingTask> SeedTaskAsync(
        string assignedTo = "alice",
        string assignedType = "1",
        TaskStatus? status = null)
    {
        var task = PendingTask.Create(
            correlationId: Guid.NewGuid(),
            taskName: "Test Task",
            assignedTo: assignedTo,
            assignedType: assignedType,
            assignedAt: DateTime.Now.AddHours(-1),
            workflowInstanceId: Guid.NewGuid(),
            activityId: "appraisal-checker",
            dueAt: DateTime.Now.AddDays(1));

        if (status == TaskStatus.InProgress)
            task.StartWorking(assignedTo);

        _dbContext.PendingTasks.Add(task);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // detach so handler does its own FindAsync
        return task;
    }

    private void SetupEligibleAssignee(string username)
    {
        var pipelineCtx = new AssignmentPipelineContext
        {
            CandidatePool = [new TeamMemberInfo(username, username, "team-1", [])]
        };
        _assignmentPipeline
            .GetEligibleAssigneesAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(pipelineCtx);
        _instanceRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowInstance.Create(Guid.NewGuid(), "test-workflow", null, "system"));
    }

    // ── Validation: unauthenticated user ──────────────────────────────────────

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsFail()
    {
        _currentUserService.Username.Returns((string?)null);

        var result = await _handler.Handle(
            new ReassignTaskCommand(Guid.NewGuid(), "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("User not authenticated");
    }

    // ── Validation: empty target user ─────────────────────────────────────────

    [Fact]
    public async Task Handle_EmptyNewAssignedTo_ReturnsFail()
    {
        var result = await _handler.Handle(
            new ReassignTaskCommand(Guid.NewGuid(), ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Target user is required");
    }

    // ── Validation: task not found ────────────────────────────────────────────

    [Fact]
    public async Task Handle_TaskNotFound_ReturnsFail()
    {
        var result = await _handler.Handle(
            new ReassignTaskCommand(Guid.NewGuid(), "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Task not found");
    }

    // ── Validation: pool task (AssignedType != "1") ───────────────────────────

    [Fact]
    public async Task Handle_PoolTask_ReturnsFail()
    {
        var task = await SeedTaskAsync(assignedType: "2");

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ClaimTask");
    }

    // ── Validation: task not in reassignable state ────────────────────────────

    [Fact]
    public async Task Handle_CompletedTask_ReturnsFail()
    {
        // Seed Assigned, then mark it Completed by archiving to CompletedTasks and removing from Pending
        var task = await SeedTaskAsync("alice");
        var completed = CompletedTask.CreateFromPendingTask(task, "Completed", DateTime.Now);
        _dbContext.CompletedTasks.Add(completed);
        _dbContext.PendingTasks.Remove(await _dbContext.PendingTasks.FindAsync(task.Id) ?? task);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // The task no longer exists in PendingTasks
        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Task not found");
    }

    // ── Idempotency: same assignee returns success with Changed=false ──────────

    [Fact]
    public async Task Handle_SameAssignee_ReturnsSuccessWithChangedFalse()
    {
        var task = await SeedTaskAsync("alice");

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "alice"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Changed.Should().BeFalse();
        result.AssignedTo.Should().Be("alice");
    }

    // ── Validation: supervisor does not monitor the user ──────────────────────

    [Fact]
    public async Task Handle_SupervisorNotMonitoringUser_ReturnsFail()
    {
        var task = await SeedTaskAsync("alice");
        _groupMonitoringService.IsUserSupervisedByAsync("alice", "supervisor", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("You do not supervise this user");
    }

    // ── Validation: task outside the team-scoped supervisor's team/company ─────

    [Fact]
    public async Task Handle_TaskOutsideTeamScope_ReturnsFail()
    {
        var task = await SeedTaskAsync("alice");
        // Supervisor monitors alice's group, but alice is outside the supervisor's team/company scope.
        _taskMonitorScope.IsTargetInScopeAsync("TASK_MONITOR_REASSIGN", "alice", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("This task is outside your team/company scope");
    }

    // ── Validation: new assignee not eligible ─────────────────────────────────

    [Fact]
    public async Task Handle_NewAssigneeNotEligible_ReturnsFail()
    {
        var task = await SeedTaskAsync("alice");
        // Pipeline returns empty candidate pool → bob is not eligible
        var emptyCtx = new AssignmentPipelineContext { CandidatePool = [] };
        _assignmentPipeline
            .GetEligibleAssigneesAsync(Arg.Any<ActivityContext>(), Arg.Any<CancellationToken>())
            .Returns(emptyCtx);
        _instanceRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(WorkflowInstance.Create(Guid.NewGuid(), "test-workflow", null, "system"));

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Target user is not eligible for this activity");
    }

    // ── Happy path: successful reassignment ───────────────────────────────────

    [Fact]
    public async Task Handle_ValidReassignment_ReturnsSuccessWithChangedTrue()
    {
        var task = await SeedTaskAsync("alice");
        SetupEligibleAssignee("bob");

        var result = await _handler.Handle(
            new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Changed.Should().BeTrue();
        result.AssignedTo.Should().Be("bob");
    }

    [Fact]
    public async Task Handle_ValidReassignment_PendingTaskAssignedToBob()
    {
        var task = await SeedTaskAsync("alice");
        SetupEligibleAssignee("bob");

        await _handler.Handle(new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        var updated = await _dbContext.PendingTasks.FindAsync(task.Id);
        updated.Should().NotBeNull();
        updated!.AssignedTo.Should().Be("bob");
    }

    [Fact]
    public async Task Handle_ValidReassignment_AuditRowCreatedWithReassignedAction()
    {
        var task = await SeedTaskAsync("alice");
        SetupEligibleAssignee("bob");

        await _handler.Handle(new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        // Audit row has a fresh Id (CreateAuditFromPendingTask), so query by ActionTaken instead
        var audit = await _dbContext.CompletedTasks
            .FirstOrDefaultAsync(ct => ct.ActionTaken == "Reassigned");
        audit.Should().NotBeNull();
        audit!.AssignedTo.Should().Be("alice");
        audit.Id.Should().NotBe(task.Id); // fresh Id, no PK collision with future completion row
    }

    [Fact]
    public async Task Handle_ValidReassignment_DueAtPreserved()
    {
        var task = await SeedTaskAsync("alice");
        var originalDueAt = task.DueAt;
        SetupEligibleAssignee("bob");

        await _handler.Handle(new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        var updated = await _dbContext.PendingTasks.FindAsync(task.Id);
        updated!.DueAt.Should().Be(originalDueAt);
    }

    [Fact]
    public async Task Handle_ValidReassignment_WorkingByClearedAndStatusAssigned()
    {
        var task = await SeedTaskAsync("alice", status: TaskStatus.InProgress);
        _dbContext.ChangeTracker.Clear();
        SetupEligibleAssignee("bob");

        await _handler.Handle(new ReassignTaskCommand(task.Id, "bob"), CancellationToken.None);

        var updated = await _dbContext.PendingTasks.FindAsync(task.Id);
        updated!.WorkingBy.Should().BeNull();
        updated.TaskStatus.Should().Be(TaskStatus.Assigned);
    }
}
