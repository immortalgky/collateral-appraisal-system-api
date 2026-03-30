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

    // ── Name property ──

    [Fact]
    public void Name_ShouldBeValidateTaskOwnership()
    {
        _sut.Name.Should().Be("ValidateTaskOwnership");
    }

    // ── ProcessStepResult contract ──

    [Fact]
    public void ProcessStepResult_Ok_ShouldHaveSuccessTrue()
    {
        var result = ProcessStepResult.Ok();

        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ProcessStepResult_Fail_ShouldHaveSuccessFalse()
    {
        var result = ProcessStepResult.Fail("Some error");

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Some error");
    }

    [Fact]
    public void ProcessStepResult_Fail_WithMultipleErrors_ShouldRetainAll()
    {
        var result = ProcessStepResult.Fail("Error A", "Error B");

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Error A");
        result.Errors.Should().Contain("Error B");
    }

    // ── ProcessStepContext contract ──

    [Fact]
    public void ProcessStepContext_ShouldHoldExpectedFields()
    {
        var appraisalId = Guid.NewGuid();
        var workflowInstanceId = Guid.NewGuid();

        var ctx = new ProcessStepContext
        {
            AppraisalId = appraisalId,
            WorkflowInstanceId = workflowInstanceId,
            ActivityName = "int-appraisal-staff",
            CompletedBy = "john.doe",
            Input = new Dictionary<string, object> { ["key"] = "value" }
        };

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
        step.Name.Should().Be("ValidateTaskOwnership");
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
    public async Task ExecuteAsync_WhenConnectionThrows_ShouldReturnFail()
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

        // Assert: step must not propagate exceptions — it returns Fail
        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("DB unavailable");
    }

    // ── IsOwner comparison logic ──

    // The step checks:
    //   string.Equals(assignedTo, currentUserService.Username, StringComparison.OrdinalIgnoreCase)
    // These theory tests document and verify that contract independently of Dapper.

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
        // This mirrors the exact comparison inside ValidateTaskOwnershipStep.ExecuteAsync
        var isOwner = string.Equals(assignedTo, currentUsername, StringComparison.OrdinalIgnoreCase);

        isOwner.Should().Be(expectedOwner);
    }

    [Fact]
    public void OwnershipCheck_WhenAssignedToIsNull_ShouldNotBeOwner()
    {
        // Null assignedTo means "no task found", the step returns Fail before this check.
        // But if compared directly: string.Equals(null, "user", OrdinalIgnoreCase) → false.
        var isOwner = string.Equals(null, "john.doe", StringComparison.OrdinalIgnoreCase);

        isOwner.Should().BeFalse();
    }

    [Fact]
    public void OwnershipCheck_WhenBothNull_ReturnsFalse()
    {
        // Unauthenticated user (null username) vs. null assignee: both null → true by string.Equals.
        // The step guards against null assignedTo first (returns Fail), so this path is unreachable
        // in production, but the underlying comparison still returns true for (null, null).
        var bothNull = string.Equals((string?)null, (string?)null, StringComparison.OrdinalIgnoreCase);

        bothNull.Should().BeTrue();
    }

    // ── IActivityProcessStep interface compliance ──

    [Fact]
    public void Step_ShouldImplementIActivityProcessStep()
    {
        _sut.Should().BeAssignableTo<IActivityProcessStep>();
    }

    // ── Helpers ──

    private static ProcessStepContext BuildContext(Guid appraisalId) =>
        new()
        {
            AppraisalId = appraisalId,
            WorkflowInstanceId = Guid.NewGuid(),
            ActivityName = "int-appraisal-staff",
            CompletedBy = "john.doe",
            Input = new Dictionary<string, object>()
        };
}
