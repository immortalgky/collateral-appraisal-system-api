using Auth.Domain.Companies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow;

public class CompanySelectionActivityTests
{
    private readonly ICompanyRoundRobinService _roundRobinService;
    private readonly ICompanyRepository _companyRepository;
    private readonly IIntegrationEventOutbox _outbox;
    private readonly CompanySelectionActivity _sut;

    public CompanySelectionActivityTests()
    {
        _roundRobinService = Substitute.For<ICompanyRoundRobinService>();
        _companyRepository = Substitute.For<ICompanyRepository>();
        // Default: any resolved company is assignable (active, open-ended window).
        _companyRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Company.Create("Stub Co"));
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        dateTimeProvider.Now.Returns(new DateTime(2026, 4, 19, 12, 0, 0));
        _outbox = Substitute.For<IIntegrationEventOutbox>();
        var logger = Substitute.For<ILogger<CompanySelectionActivity>>();
        _sut = new CompanySelectionActivity(_roundRobinService, _companyRepository, dateTimeProvider, _outbox, logger);
    }

    private static ActivityContext CreateContext(Dictionary<string, object>? variables = null)
    {
        var workflowInstance = WorkflowInstance.Create(
            Guid.NewGuid(),
            "test-workflow",
            null,
            "test-user");

        var vars = variables ?? new Dictionary<string, object>();
        vars.TryAdd("appraisalId", Guid.NewGuid());

        return new ActivityContext
        {
            WorkflowInstanceId = workflowInstance.Id,
            ActivityId = "company-selection",
            Properties = new Dictionary<string, object>(),
            Variables = vars,
            WorkflowInstance = workflowInstance
        };
    }

    [Fact]
    public async Task ExecuteAsync_ManualSelection_ReturnsSuccessWithCompany()
    {
        // Arrange
        var companyId = Guid.NewGuid().ToString();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["selectedCompanyId"] = companyId,
            ["selectedCompanyName"] = "Acme Corp"
        });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["assignedCompanyId"].Should().Be(companyId);
        result.OutputData["assignedCompanyName"].Should().Be("Acme Corp");
        result.OutputData["assignmentMethod"].Should().Be("Manual");
        result.OutputData["decision"].Should().Be("company_selected");
    }

    [Fact]
    public async Task ExecuteAsync_ManualSelectionNoCompanyId_ReturnsFailed()
    {
        // Arrange
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual"
        });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Failed);
        result.ErrorMessage.Should().Contain("No company selected");
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobinSuccess_ReturnsCompany()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Success(companyId, "RR Corp"));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "roundrobin"
        });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["assignedCompanyId"].Should().Be(companyId.ToString());
        result.OutputData["assignedCompanyName"].Should().Be("RR Corp");
        result.OutputData["assignmentMethod"].Should().Be("RoundRobin");
        result.OutputData["decision"].Should().Be("company_selected");
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobinWithBankingSegment_ScopesPoolToSegment()
    {
        // The round-robin pool scope comes from the bankingSegment variable (set at workflow
        // start), not the legacy never-assigned loanType variable. An IBG appraisal must select
        // from the IBG pool.
        var companyId = Guid.NewGuid();
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), "IBG", Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Success(companyId, "Loan Corp"));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["bankingSegment"] = "IBG"
        });

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["assignedCompanyId"].Should().Be(companyId.ToString());

        // Verify the banking segment was passed through to the selection service as the scope.
        await _roundRobinService.Received(1)
            .SelectCompanyAsync(Arg.Any<Guid?>(), "IBG", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobinNoMatch_ReturnsNoMatchDecision()
    {
        // Arrange
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Failure("No active companies"));

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert — still success (escalation), but decision is no_match
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["selectionError"].Should().Be("No active companies");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultSelectionMethodIsRoundRobin()
    {
        // Arrange — no assignmentMethod in variables
        var companyId = Guid.NewGuid();
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Success(companyId, "Default Corp"));

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context);

        // Assert — should use roundrobin path (not manual)
        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["assignmentMethod"].Should().Be("RoundRobin");
        await _roundRobinService.Received(1)
            .SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RoundRobin_PublishesCompanyAssignedEvent()
    {
        var appraisalId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Success(companyId, "RR Corp"));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["appraisalId"] = appraisalId
        });

        await _sut.ExecuteAsync(context);

        _outbox.Received(1).Publish(
            Arg.Is<CompanyAssignedIntegrationEvent>(e =>
                e.AppraisalId == appraisalId
                && e.CompanyId == companyId
                && e.AssignmentMethod == "RoundRobin"),
            appraisalId.ToString(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_ReplayBranch_DoesNotPublish()
    {
        // Replay: same segment as previously assigned company — the activity
        // reuses the prior selection and must NOT republish (would double-count).
        var context = CreateContext(new Dictionary<string, object>
        {
            ["bankingSegment"] = "IBG",
            ["assignedCompanyId"] = Guid.NewGuid().ToString(),
            ["assignedCompanyName"] = "Prior Corp",
            ["assignedCompanyLoanType"] = "IBG"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_DoesNotPublish()
    {
        _roundRobinService.SelectCompanyAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CompanySelectionResult.Failure("No active companies"));

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_ManualSelectionFailure_DoesNotPublish()
    {
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual"
            // no selectedCompanyId
        });

        await _sut.ExecuteAsync(context);

        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_ManualSelectionExpiredCompany_EscalatesAndDoesNotPublish()
    {
        // Company resolved but outside its MOU window (expired yesterday) → not assignable.
        _companyRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Company.Create("Expired Co", expireDate: new DateTime(2026, 4, 18, 0, 0, 0)));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["selectedCompanyId"] = Guid.NewGuid().ToString(),
            ["selectedCompanyName"] = "Expired Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("no_match");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_ForcedExpiredCompany_EscalatesAndDoesNotPublish()
    {
        _companyRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Company.Create("Forced Co", expireDate: new DateTime(2026, 4, 18, 0, 0, 0)));

        var context = CreateContext(new Dictionary<string, object>
        {
            ["forceCompanyId"] = Guid.NewGuid().ToString(),
            ["forceCompanyName"] = "Forced Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("no_match");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }
}
