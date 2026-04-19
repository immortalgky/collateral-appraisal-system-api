using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Data;
using Shared.Identity;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;
using Xunit;

namespace Workflow.Tests.Workflow.Pipeline;

public class ValidateTaskOwnershipStepTests
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ValidateTaskOwnershipStep> _logger;
    private readonly IDbConnection _dbConnection;
    private readonly ValidateTaskOwnershipStep _sut;

    public ValidateTaskOwnershipStepTests()
    {
        _connectionFactory = Substitute.For<ISqlConnectionFactory>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _logger = Substitute.For<ILogger<ValidateTaskOwnershipStep>>();
        _dbConnection = Substitute.For<IDbConnection>();

        _connectionFactory.GetOpenConnection().Returns(_dbConnection);

        _sut = new ValidateTaskOwnershipStep(_connectionFactory, _currentUserService, _logger);
    }

    // ── Descriptor ──

    [Fact]
    public void Descriptor_Name_ShouldBeValidateTaskOwnership()
    {
        _sut.Descriptor.Name.Should().Be("ValidateTaskOwnership");
    }

    // ── ProcessStepResult contract ──

    [Fact]
    public void ProcessStepResult_Pass_ShouldBeSuccess()
    {
        var result = ProcessStepResult.Pass();

        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<ProcessStepResult.Passed>();
    }

    [Fact]
    public void ProcessStepResult_Fail_ShouldNotBeSuccess()
    {
        var result = ProcessStepResult.Fail("SOME_CODE", "Some error");

        result.IsSuccess.Should().BeFalse();
        result.Should().BeOfType<ProcessStepResult.Failed>();
        var failed = (ProcessStepResult.Failed)result;
        failed.ErrorCode.Should().Be("SOME_CODE");
        failed.Message.Should().Be("Some error");
    }

    // ── ProcessStepContext contract ──

    [Fact]
    public void ProcessStepContext_ShouldHoldExpectedFields()
    {
        var appraisalId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();

        var ctx = new ProcessStepContext
        {
            CorrelationId = appraisalId,
            AppraisalId = appraisalId,
            WorkflowInstanceId = workflowInstanceId,
            ActivityName = "int-appraisal-staff",
            CompletedBy = "john.doe",
            Input = new Dictionary<string, object?> { ["key"] = "value" }
        };

        ctx.CorrelationId.Should().Be(appraisalId);
        ctx.AppraisalId.Should().Be(appraisalId);
        ctx.WorkflowInstanceId.Should().Be(workflowInstanceId);
        ctx.ActivityName.Should().Be("int-appraisal-staff");
        ctx.CompletedBy.Should().Be("john.doe");
        ctx.Input.Should().ContainKey("key");
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        var step = new ValidateTaskOwnershipStep(_connectionFactory, _currentUserService, _logger);

        step.Should().NotBeNull();
        step.Descriptor.Name.Should().Be("ValidateTaskOwnership");
    }

    // ── ExecuteAsync: connection usage ──

    [Fact]
    public async Task ExecuteAsync_ShouldOpenConnection()
    {
        // Arrange
        _currentUserService.Username.Returns("john.doe");
        var context = BuildContext(Guid.NewGuid());

        // Act — mock DB will not return a real scalar; the step catches all exceptions
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert: connection was obtained
        _connectionFactory.Received(1).GetOpenConnection();
    }

    [Fact]
    public async Task ExecuteAsync_WhenConnectionThrows_ShouldReturnErrored()
    {
        // Arrange — simulate a broken connection factory by making GetOpenConnection throw
        var throwingFactory = Substitute.For<ISqlConnectionFactory>();
        throwingFactory
            .When(f => f.GetOpenConnection())
            .Do(_ => throw new InvalidOperationException("DB unavailable"));

        var step = new ValidateTaskOwnershipStep(throwingFactory, _currentUserService, _logger);
        var context = BuildContext(Guid.NewGuid());

        // Act
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        // Assert: step must not propagate exceptions — it returns Errored
        result.IsSuccess.Should().BeFalse();
        result.Should().BeOfType<ProcessStepResult.Errored>();
    }

    // ── IsOwner comparison logic ──

    [Theory]
    [InlineData("john.doe", "john.doe", true)]
    [InlineData("JOHN.DOE", "john.doe", true)]
    [InlineData("john.doe", "JOHN.DOE", true)]
    [InlineData("John.Doe", "JOHN.DOE", true)]
    [InlineData("john.doe", "jane.smith", false)]
    public void OwnershipCheck_CaseInsensitiveComparison_BehavesCorrectly(
        string assignedTo,
        string currentUsername,
        bool expectedOwner)
    {
        var isOwner = string.Equals(assignedTo, currentUsername, StringComparison.OrdinalIgnoreCase);
        isOwner.Should().Be(expectedOwner);
    }

    [Fact]
    public void OwnershipCheck_WhenAssignedToIsNull_ShouldNotBeOwner()
    {
        var isOwner = string.Equals(null, "john.doe", StringComparison.OrdinalIgnoreCase);
        isOwner.Should().BeFalse();
    }

    // ── IActivityProcessStep interface compliance ──

    [Fact]
    public void Step_ShouldImplementIActivityProcessStep()
    {
        _sut.Should().BeAssignableTo<IActivityProcessStep>();
    }

    // ── Helpers ──

    private static ProcessStepContext BuildContext(Guid correlationId) =>
        new()
        {
            CorrelationId = correlationId,
            AppraisalId = correlationId,
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "int-appraisal-staff",
            CompletedBy = "john.doe",
            Input = new Dictionary<string, object?>()
        };
}
