using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.AssigneeSelection.Services;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class InternalFollowupSelectionActivityTests
{
    private readonly IInternalStaffRoundRobinService _staffRoundRobinService;
    private readonly ITaskConfigurationService _configurationService;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly InternalFollowupSelectionActivity _sut;

    public InternalFollowupSelectionActivityTests()
    {
        _staffRoundRobinService = Substitute.For<IInternalStaffRoundRobinService>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        _configurationService = Substitute.For<ITaskConfigurationService>();
        _outbox = Substitute.For<IIntegrationEventOutbox>();
        var logger = Substitute.For<ILogger<InternalFollowupSelectionActivity>>();
        _sut = new InternalFollowupSelectionActivity(
            _staffRoundRobinService, _configurationService, dateTimeProvider, _outbox, logger);
    }

    private static ActivityContext CreateContext(
        Dictionary<string, object>? variables = null,
        Dictionary<string, object>? properties = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", null, "test-user");

        var vars = variables ?? new Dictionary<string, object>();
        vars.TryAdd("appraisalId", Guid.NewGuid());

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "internal-followup-selection",
            Properties = properties ?? new Dictionary<string, object>(),
            Variables = vars,
            WorkflowInstance = workflowInstance
        };
    }

    [Fact]
    public async Task ExecuteAsync_AdminPreSelectedStaff_PublishesFollowupEvent()
    {
        var appraisalId = Guid.NewGuid();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId,
            ["internalFollowupStaffId"] = "user-123",
            ["internalFollowupMethod"] = "Manual"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("staff_selected");
        result.OutputData["internalFollowupStaffId"].Should().Be("user-123");
        result.OutputData["internalFollowupMethod"].Should().Be("Manual");

        _outbox.Received(1).Publish(
            Arg.Is<InternalFollowupAssignedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId
                && e.InternalAppraiserId == "user-123"
                && e.InternalFollowupAssignmentMethod == "Manual"),
            appraisalId.ToString(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_AdminPreSelectedStaffNoMethod_DefaultsToManual()
    {
        var context = CreateContext(new Dictionary<string, object>
        {
            ["internalFollowupStaffId"] = "user-123"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["internalFollowupMethod"].Should().Be("Manual");
        _outbox.Received(1).Publish(
            Arg.Is<InternalFollowupAssignedIntegrationEvent>(e =>
                e.InternalFollowupAssignmentMethod == "Manual"),
            Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobinSuccess_PublishesFollowupEvent()
    {
        var appraisalId = Guid.NewGuid();
        _staffRoundRobinService.SelectStaffAsync(Arg.Any<CancellationToken>())
            .Returns(StaffSelectionResult.Success("rr-user-456"));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("staff_selected");
        result.OutputData["internalFollowupStaffId"].Should().Be("rr-user-456");
        result.OutputData["internalFollowupMethod"].Should().Be("RoundRobin");

        _outbox.Received(1).Publish(
            Arg.Is<InternalFollowupAssignedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId
                && e.InternalAppraiserId == "rr-user-456"
                && e.InternalFollowupAssignmentMethod == "RoundRobin"),
            appraisalId.ToString(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_DoesNotPublish()
    {
        _staffRoundRobinService.SelectStaffAsync(Arg.Any<CancellationToken>())
            .Returns(StaffSelectionResult.Failure("No eligible internal staff"));

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["selectionError"].Should().Be("No eligible internal staff");
        _outbox.DidNotReceiveWithAnyArgs().Publish<InternalFollowupAssignedIntegrationEvent>(default!);
    }

    // ── sameAssigneeAsActivity: the source activity's completer becomes the followup staff ──

    private static void AddCompleted(ActivityContext context, string activityId, string completedBy)
    {
        var execution = WorkflowActivityExecution.Create(
            context.WorkflowInstance.Id, activityId, activityId, "TaskActivity");
        execution.Complete(completedBy);
        context.WorkflowInstance.ActivityExecutions.Add(execution);
    }

    private static Dictionary<string, object> JsonSource(string activityId)
        => new() { ["sameAssigneeAsActivity"] = activityId };

    [Fact]
    public async Task ExecuteAsync_SourceCompleterPresent_WinsEvenOverAdminSelectedStaff()
    {
        var appraisalId = Guid.NewGuid();
        var context = CreateContext(
            new Dictionary<string, object>
            {
                ["appraisalId"] = appraisalId,
                ["internalFollowupStaffId"] = "admin-picked",
                ["internalFollowupMethod"] = "Manual"
            },
            JsonSource("int-pma-input"));
        AddCompleted(context, "int-pma-input", "pma.person");

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("staff_selected");
        result.OutputData["internalFollowupStaffId"].Should().Be("pma.person");
        result.OutputData["internalFollowupMethod"].Should().Be("Manual");
        _outbox.Received(1).Publish(
            Arg.Is<InternalFollowupAssignedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId
                && e.InternalAppraiserId == "pma.person"
                && e.InternalFollowupAssignmentMethod == "Manual"),
            appraisalId.ToString(),
            Arg.Any<Dictionary<string, string>?>());
        await _staffRoundRobinService.DidNotReceive().SelectStaffAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SourceSetButNeverCompleted_FallsBackToRoundRobin()
    {
        _staffRoundRobinService.SelectStaffAsync(Arg.Any<CancellationToken>())
            .Returns(StaffSelectionResult.Success("rr-user-456"));
        var context = CreateContext(properties: JsonSource("int-pma-input"));
        // Only a system completion exists, which never counts as a person.
        AddCompleted(context, "int-pma-input", "SYSTEM");

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["internalFollowupStaffId"].Should().Be("rr-user-456");
        result.OutputData["internalFollowupMethod"].Should().Be("RoundRobin");
    }

    [Fact]
    public async Task ExecuteAsync_DbOverrideSource_BeatsJsonSource()
    {
        _configurationService
            .GetConfigurationAsync("internal-followup-selection", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TaskAssignmentConfigurationDto
            {
                AdditionalConfiguration = new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "db-source" }
            });
        var context = CreateContext(properties: JsonSource("json-source"));
        AddCompleted(context, "json-source", "json.person");
        AddCompleted(context, "db-source", "db.person");

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["internalFollowupStaffId"].Should().Be("db.person");
    }

    [Fact]
    public async Task ExecuteAsync_UnsetDbSource_FallsBackToJsonSource()
    {
        _configurationService
            .GetConfigurationAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TaskAssignmentConfigurationDto
            {
                AdditionalConfiguration = new Dictionary<string, object> { ["sameAssigneeAsActivity"] = "  " }
            });
        var context = CreateContext(properties: JsonSource("json-source"));
        AddCompleted(context, "json-source", "json.person");

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["internalFollowupStaffId"].Should().Be("json.person");
    }

    [Fact]
    public async Task ExecuteAsync_NoSource_KeepsAdminSelectedStaff()
    {
        var context = CreateContext(new Dictionary<string, object> { ["internalFollowupStaffId"] = "admin-picked" });
        // A completer exists, but without a configured source it must not be consulted.
        AddCompleted(context, "int-pma-input", "pma.person");

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["internalFollowupStaffId"].Should().Be("admin-picked");
        result.OutputData["internalFollowupMethod"].Should().Be("Manual");
    }
}
