using Assignment.AssigneeSelection.Configuration;
using Assignment.AssigneeSelection.Core;
using Assignment.AssigneeSelection.Strategies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Assignment.Tests.AssigneeSelection;

public class SupervisorAssigneeSelectorTests
{
    private readonly ILogger<SupervisorAssigneeSelector> _logger;
    private readonly IOptions<MockSupervisorOptions> _options;
    private readonly MockSupervisorOptions _mockOptions;
    private readonly SupervisorAssigneeSelector _selector;

    public SupervisorAssigneeSelectorTests()
    {
        _logger = Substitute.For<ILogger<SupervisorAssigneeSelector>>();
        _options = Substitute.For<IOptions<MockSupervisorOptions>>();
        
        _mockOptions = new MockSupervisorOptions
        {
            SupervisorMappings = new Dictionary<string, string>
            {
                ["appraisers"] = "supervisor-001",
                ["reviewers"] = "supervisor-002",
                ["underwriters"] = "supervisor-003"
            },
            ValidSupervisors = new HashSet<string>
            {
                "supervisor-001",
                "supervisor-002", 
                "supervisor-003",
                "default-supervisor-001"
            },
            DefaultSupervisor = "default-supervisor-001"
        };

        _options.Value.Returns(_mockOptions);
        _selector = new SupervisorAssigneeSelector(_logger, _options);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithSupervisorIdInContext_ReturnsSpecifiedSupervisor()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            Properties = new Dictionary<string, object>
            {
                ["SupervisorId"] = "supervisor-002"
            }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("supervisor-002", result.AssigneeId);
        Assert.Equal("Supervisor", result.Metadata!["SelectionStrategy"]);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithValidUserGroup_ReturnsCorrectSupervisor()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "appraisers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("supervisor-001", result.AssigneeId);
        Assert.Equal("Supervisor", result.Metadata!["SelectionStrategy"]);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithUnknownUserGroup_ReturnsDefaultSupervisor()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "unknown-group" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("default-supervisor-001", result.AssigneeId);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithInvalidSupervisorId_ReturnsFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            Properties = new Dictionary<string, object>
            {
                ["SupervisorId"] = "invalid-supervisor"
            }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not eligible", result.ErrorMessage);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNoUserGroupsOrSupervisorId_ReturnsFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string>()
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("supervisor to be specified", result.ErrorMessage);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNullUserGroups_ReturnsFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = null!
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("supervisor to be specified", result.ErrorMessage);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithWhitespaceUserGroups_ReturnsFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "", "  ", null! }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("supervisor to be specified", result.ErrorMessage);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "appraisers" }
        };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _selector.SelectAssigneeAsync(context, cts.Token));
    }

    [Fact]
    public void MockSupervisorOptions_Validate_WithValidConfiguration_DoesNotThrow()
    {
        // Act & Assert
        _mockOptions.Validate(); // Should not throw
    }

    [Fact]
    public void MockSupervisorOptions_Validate_WithInvalidDefaultSupervisor_Throws()
    {
        // Arrange
        var invalidOptions = new MockSupervisorOptions
        {
            SupervisorMappings = new Dictionary<string, string> { ["test"] = "supervisor-001" },
            ValidSupervisors = new HashSet<string> { "supervisor-001" },
            DefaultSupervisor = "invalid-supervisor"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invalidOptions.Validate());
    }

    [Fact]
    public void MockSupervisorOptions_Validate_WithInvalidMapping_Throws()
    {
        // Arrange
        var invalidOptions = new MockSupervisorOptions
        {
            SupervisorMappings = new Dictionary<string, string> { ["test"] = "invalid-supervisor" },
            ValidSupervisors = new HashSet<string> { "supervisor-001" },
            DefaultSupervisor = "supervisor-001"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => invalidOptions.Validate());
    }
}