using Auth.Domain.Companies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Configuration;
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
    private readonly ISystemConfigurationReader _configReader;
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

        // Default: auto-assignment ENABLED, so the existing tests exercise the unchanged behaviour.
        // The reader is asked with defaultValue:true, and NSubstitute returns the first matching
        // configured value, so this mirrors an environment where the key is absent or "true".
        _configReader = Substitute.For<ISystemConfigurationReader>();
        _configReader
            .GetBoolAsync(CompanySelectionActivity.CompanyAssignmentEnabledKey, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var logger = Substitute.For<ILogger<CompanySelectionActivity>>();
        _sut = new CompanySelectionActivity(
            _roundRobinService, _companyRepository, _configReader, dateTimeProvider, _outbox, logger);
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

    [Theory]
    [InlineData("manual", "Manual")]
    [InlineData("Quotation", "Quotation")]
    public async Task ExecuteAsync_StaleAssignedCompany_DoesNotOverrideTheFreshSelection(string method, string normalized)
    {
        // Re-entry after a route back / earlier round robin: assignedCompanyId still holds company X.
        var stale = Guid.NewGuid().ToString();
        var fresh = Guid.NewGuid();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = method,
            ["assignedCompanyId"] = stale,
            ["assignedCompanyName"] = "Stale Co",
            ["selectedCompanyId"] = fresh.ToString(),
            ["selectedCompanyName"] = "Fresh Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["assignedCompanyId"].Should().Be(fresh.ToString());
        result.OutputData["assignedCompanyName"].Should().Be("Fresh Co");
        result.OutputData["assignmentMethod"].Should().Be(normalized);
        _outbox.Received(1).Publish(
            Arg.Is<CompanyAssignedIntegrationEvent>(e => e.CompanyId == fresh && e.CompanyName == "Fresh Co"),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("Quotation")]
    public async Task ExecuteAsync_EmptySelectedCompany_FallsBackToAssignedCompany(string method)
    {
        var assigned = Guid.NewGuid().ToString();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = method,
            ["assignedCompanyId"] = assigned,
            ["assignedCompanyName"] = "Assigned Co",
            ["selectedCompanyId"] = ""
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["assignedCompanyId"].Should().Be(assigned);
        result.OutputData["assignedCompanyName"].Should().Be("Assigned Co");
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

    // ── Global "no auto-assign" switch (ExternalCompanyAssignmentEnabled) ────────────────────

    private void GivenAutoAssignDisabled() =>
        _configReader
            .GetBoolAsync(CompanySelectionActivity.CompanyAssignmentEnabledKey, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);

    [Fact]
    public async Task ExecuteAsync_Disabled_EscalatesToAdminInsteadOfRoundRobin()
    {
        // Phase-1 go-live: the bank engages companies outside the system, so nothing may
        // round-robin one. no_match is what the existing company-no-match transition routes to
        // appraisal-assignment on.
        GivenAutoAssignDisabled();

        var result = await _sut.ExecuteAsync(CreateContext());

        result.Status.Should().Be(ActivityResultStatus.Completed);
        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["assignedCompanyId"].Should().Be("");
        await _roundRobinService.DidNotReceiveWithAnyArgs()
            .SelectCompanyAsync(default, default, default);
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Theory]
    [InlineData("manual", "selectedCompanyId")]
    [InlineData("Quotation", "selectedCompanyId")]
    public async Task ExecuteAsync_Disabled_BlocksAdminChosenCompanyToo(
        string method, string companyVariable)
    {
        // The switch is NOT "no automatic assignment" — it is "no in-system company assignment at
        // all". An admin picking the company by hand, or a quotation winner, is just as wrong while
        // the bank engages companies outside CAS, so these are blocked too.
        GivenAutoAssignDisabled();

        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = method,
            [companyVariable] = Guid.NewGuid().ToString(),
            ["selectedCompanyName"] = "Chosen Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["assignedCompanyId"].Should().Be("");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_BlocksForcedConstructionInspectionCarryOver()
    {
        // A CI follow-up carries the prior engagement's company over. That is still an in-system
        // assignment to a company, so it escalates to admin like everything else; the admin routes
        // it internally or records the off-system engagement.
        GivenAutoAssignDisabled();

        var context = CreateContext(new Dictionary<string, object>
        {
            ["forceCompanyId"] = Guid.NewGuid().ToString(),
            ["forceCompanyName"] = "Prior Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_KeepsCompanyAlreadySelectedBeforeTheSwitch()
    {
        // The guard exempts a case that already holds a company: that engagement predates the
        // switch and must not be torn up when the activity replays.
        GivenAutoAssignDisabled();

        var companyId = Guid.NewGuid();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignedCompanyId"] = companyId.ToString(),
            ["assignedCompanyName"] = "Already Co",
            ["assignedCompanyLoanType"] = ""
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        result.OutputData["assignedCompanyId"].Should().Be(companyId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_RoundRobinWithChangedSegment_IsNoMatchNotANewCompany()
    {
        // The replay guard does not fire (the segment changed), so round robin would pick a NEW company.
        // That is a new in-system engagement, so the switch applies even though the case holds a company.
        GivenAutoAssignDisabled();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["bankingSegment"] = "IBG",
            ["assignedCompanyId"] = Guid.NewGuid().ToString(),
            ["assignedCompanyName"] = "Prior Corp",
            ["assignedCompanyLoanType"] = "Retail"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["assignedCompanyId"].Should().Be("");
        await _roundRobinService.DidNotReceiveWithAnyArgs().SelectCompanyAsync(default, default, default);
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_ReplayGuardReuse_StaysExempt()
    {
        GivenAutoAssignDisabled();
        var companyId = Guid.NewGuid().ToString();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["bankingSegment"] = "IBG",
            ["assignedCompanyId"] = companyId,
            ["assignedCompanyName"] = "Prior Corp",
            ["assignedCompanyLoanType"] = "IBG"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        result.OutputData["assignedCompanyId"].Should().Be(companyId);
        await _roundRobinService.DidNotReceiveWithAnyArgs().SelectCompanyAsync(default, default, default);
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("Quotation")]
    public async Task ExecuteAsync_Disabled_StaleAssignedCompanyDoesNotExemptADifferentFreshPick(string method)
    {
        // assignedCompanyId=X is stale; the admin now picks Y. Y is a NEW engagement, so the switch applies.
        GivenAutoAssignDisabled();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = method,
            ["assignedCompanyId"] = Guid.NewGuid().ToString(),
            ["selectedCompanyId"] = Guid.NewGuid().ToString(),
            ["selectedCompanyName"] = "Fresh Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        result.OutputData["assignedCompanyId"].Should().Be("");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_SameCompanyAssignedAndSelected_IsStillExempt()
    {
        GivenAutoAssignDisabled();
        var companyId = Guid.NewGuid();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["assignedCompanyId"] = companyId.ToString().ToUpperInvariant(),
            ["selectedCompanyId"] = companyId.ToString(),
            ["selectedCompanyName"] = "Same Co"
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        result.OutputData["assignedCompanyId"].Should().Be(companyId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_AssignedCompanyWithEmptySelection_StaysExempt()
    {
        GivenAutoAssignDisabled();
        var companyId = Guid.NewGuid().ToString();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["assignedCompanyId"] = companyId,
            ["assignedCompanyName"] = "Already Co",
            ["selectedCompanyId"] = ""
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        result.OutputData["assignedCompanyId"].Should().Be(companyId);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_ForcedCompanyEqualToAssigned_StaysExemptEvenWithADifferentFreshPick()
    {
        // Construction inspection: the forced company A is what will be used, whatever was selected.
        GivenAutoAssignDisabled();
        var forced = Guid.NewGuid();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["forceCompanyId"] = forced.ToString(),
            ["forceCompanyName"] = "Forced Co",
            ["assignedCompanyId"] = forced.ToString(),
            ["selectedCompanyId"] = Guid.NewGuid().ToString()
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("company_selected");
        result.OutputData["assignedCompanyId"].Should().Be(forced.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_ForcedCompanyDifferentFromAssigned_IsNoMatch()
    {
        GivenAutoAssignDisabled();
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["forceCompanyId"] = Guid.NewGuid().ToString(),
            ["forceCompanyName"] = "Forced Co",
            ["assignedCompanyId"] = Guid.NewGuid().ToString()
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["decision"].Should().Be("no_match");
        _outbox.DidNotReceiveWithAnyArgs().Publish<CompanyAssignedIntegrationEvent>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_FreshSelectionWithoutName_TakesTheNameFromThatCompanyNotTheStaleOne()
    {
        var fresh = Guid.NewGuid();
        var company = Company.Create("Fresh Co From Lookup");
        _companyRepository.GetByIdAsync(fresh, Arg.Any<CancellationToken>()).Returns(company);
        var context = CreateContext(new Dictionary<string, object>
        {
            ["assignmentMethod"] = "manual",
            ["assignedCompanyId"] = Guid.NewGuid().ToString(),
            ["assignedCompanyName"] = "Stale Co",
            ["selectedCompanyId"] = fresh.ToString(),
            ["selectedCompanyName"] = ""
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["assignedCompanyId"].Should().Be(fresh.ToString());
        result.OutputData["assignedCompanyName"].Should().Be("Fresh Co From Lookup");
        _outbox.Received(1).Publish(
            Arg.Is<CompanyAssignedIntegrationEvent>(e => e.CompanyName == "Fresh Co From Lookup"),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_ForcedWithoutName_TakesTheNameFromTheForcedCompany()
    {
        var forced = Guid.NewGuid();
        _companyRepository.GetByIdAsync(forced, Arg.Any<CancellationToken>())
            .Returns(Company.Create("Forced Co From Lookup"));
        var context = CreateContext(new Dictionary<string, object>
        {
            ["forceCompanyId"] = forced.ToString(),
            ["forceCompanyName"] = ""
        });

        var result = await _sut.ExecuteAsync(context);

        result.OutputData["assignedCompanyId"].Should().Be(forced.ToString());
        result.OutputData["assignedCompanyName"].Should().Be("Forced Co From Lookup");
        _outbox.Received(1).Publish(
            Arg.Is<CompanyAssignedIntegrationEvent>(e => e.CompanyName == "Forced Co From Lookup"),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingConfigKey_DefaultsToAutoAssignEnabled()
    {
        // An environment that never seeded the key must behave exactly as before the switch existed.
        var reader = Substitute.For<ISystemConfigurationReader>();
        reader.GetBoolAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<bool>(1));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(new DateTime(2026, 4, 19, 12, 0, 0));

        var sut = new CompanySelectionActivity(
            _roundRobinService, _companyRepository, reader, dateTimeProvider, _outbox,
            Substitute.For<ILogger<CompanySelectionActivity>>());

        await sut.ExecuteAsync(CreateContext());

        await _roundRobinService.ReceivedWithAnyArgs(1).SelectCompanyAsync(default, default, default);
    }
}
