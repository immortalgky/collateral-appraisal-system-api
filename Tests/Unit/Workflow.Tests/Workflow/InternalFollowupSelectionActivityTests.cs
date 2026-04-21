using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class InternalFollowupSelectionActivityTests
{
    private readonly IInternalStaffRoundRobinService _staffRoundRobinService;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly InternalFollowupSelectionActivity _sut;

    public InternalFollowupSelectionActivityTests()
    {
        _staffRoundRobinService = Substitute.For<IInternalStaffRoundRobinService>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        _outbox = Substitute.For<IIntegrationEventOutbox>();
        var logger = Substitute.For<ILogger<InternalFollowupSelectionActivity>>();
        _sut = new InternalFollowupSelectionActivity(
            _staffRoundRobinService, dateTimeProvider, _outbox, logger);
    }

    private static ActivityContext CreateContext(Dictionary<string, object>? variables = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", null, "test-user");

        var vars = variables ?? new Dictionary<string, object>();
        vars.TryAdd("appraisalId", Guid.NewGuid());

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "internal-followup-selection",
            Properties = new Dictionary<string, object>(),
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
}
