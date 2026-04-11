using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

public class EmitAppraisalCreationRequestedStepTests
{
    private readonly IIntegrationEventOutbox _outbox;
    private readonly AppraisalCreationTriggerEvaluator _triggerEvaluator;
    private readonly ILogger<EmitAppraisalCreationRequestedStep> _logger;
    private readonly EmitAppraisalCreationRequestedStep _sut;

    public EmitAppraisalCreationRequestedStepTests()
    {
        _outbox = Substitute.For<IIntegrationEventOutbox>();
        var expressionEvaluator = new ExpressionEvaluator();
        var evaluatorLogger = Substitute.For<ILogger<AppraisalCreationTriggerEvaluator>>();
        _triggerEvaluator = new AppraisalCreationTriggerEvaluator(null!, expressionEvaluator, evaluatorLogger);
        _logger = Substitute.For<ILogger<EmitAppraisalCreationRequestedStep>>();
        _sut = new EmitAppraisalCreationRequestedStep(_outbox, _triggerEvaluator, _logger);
    }

    [Fact]
    public void Name_ShouldBeEmitAppraisalCreationRequested()
    {
        _sut.Name.Should().Be("EmitAppraisalCreationRequested");
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyRequested_ReturnsOkWithoutPublishing()
    {
        var context = BuildContext(
            variables: new Dictionary<string, object>
            {
                ["appraisalCreationRequested"] = true,
                ["channel"] = "MANUAL"
            },
            parameters: """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""");

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        _outbox.DidNotReceive().Publish(
            Arg.Any<AppraisalCreationRequestedIntegrationEvent>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingParameters_ReturnsFail()
    {
        var context = BuildContext(
            variables: new Dictionary<string, object> { ["channel"] = "MANUAL" },
            parameters: null);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Missing step parameters");
    }

    [Fact]
    public async Task ExecuteAsync_ConditionNotMet_ReturnsOkWithoutPublishing()
    {
        var context = BuildContext(
            variables: new Dictionary<string, object>
            {
                ["channel"] = "ONLINE",
                ["requestSubmissionPayload"] = BuildSamplePayload()
            },
            parameters: """{"condition": "channel == 'MANUAL'"}""");

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        _outbox.DidNotReceive().Publish(
            Arg.Any<AppraisalCreationRequestedIntegrationEvent>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_RequireDecisionNotMet_ReturnsOkWithoutPublishing()
    {
        var context = BuildContext(
            variables: new Dictionary<string, object>
            {
                ["channel"] = "MANUAL",
                ["requestSubmissionPayload"] = BuildSamplePayload()
            },
            parameters: """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            input: new Dictionary<string, object> { ["decisionTaken"] = "R" });

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        _outbox.DidNotReceive().Publish(
            Arg.Any<AppraisalCreationRequestedIntegrationEvent>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_ConditionMet_PublishesEventAndSetsFlag()
    {
        var variables = new Dictionary<string, object>
        {
            ["channel"] = "MANUAL",
            ["requestSubmissionPayload"] = BuildSamplePayload()
        };

        var context = BuildContext(
            variables: variables,
            parameters: """{"condition": "channel == 'MANUAL'", "requireDecision": "P"}""",
            input: new Dictionary<string, object> { ["decisionTaken"] = "P" });

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        _outbox.Received(1).Publish(
            Arg.Any<AppraisalCreationRequestedIntegrationEvent>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>());
        variables["appraisalCreationRequested"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPayload_ReturnsFail()
    {
        var context = BuildContext(
            variables: new Dictionary<string, object> { ["channel"] = "ONLINE" },
            parameters: """{"condition": "channel != 'MANUAL'"}""");

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Missing requestSubmissionPayload in workflow variables");
    }

    [Fact]
    public async Task ExecuteAsync_NonManualChannel_ConditionMet_Publishes()
    {
        var variables = new Dictionary<string, object>
        {
            ["channel"] = "ONLINE",
            ["requestSubmissionPayload"] = BuildSamplePayload()
        };

        var context = BuildContext(
            variables: variables,
            parameters: """{"condition": "channel != 'MANUAL'"}""");

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Success.Should().BeTrue();
        _outbox.Received(1).Publish(
            Arg.Any<AppraisalCreationRequestedIntegrationEvent>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>?>());
    }

    // ── Helpers ──

    private static ProcessStepContext BuildContext(
        Dictionary<string, object> variables,
        string? parameters,
        Dictionary<string, object>? input = null) =>
        new()
        {
            CorrelationId = Guid.NewGuid(),
            AppraisalId = null,
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "appraisal-initiation-check",
            CompletedBy = "test.user",
            Input = input ?? new Dictionary<string, object>(),
            Variables = variables,
            Parameters = parameters
        };

    private static string BuildSamplePayload()
    {
        var evt = new RequestSubmittedIntegrationEvent
        {
            RequestId = Guid.NewGuid(),
            RequestTitles = [],
            CreatedBy = "test.user",
            Priority = "Normal",
            IsPma = false,
            Channel = "MANUAL"
        };
        return JsonSerializer.Serialize(evt);
    }
}
