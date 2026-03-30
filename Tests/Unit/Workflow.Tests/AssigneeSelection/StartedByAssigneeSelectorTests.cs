using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class StartedByAssigneeSelectorTests
{
    private readonly StartedByAssigneeSelector _sut;

    public StartedByAssigneeSelectorTests()
    {
        var logger = Substitute.For<ILogger<StartedByAssigneeSelector>>();
        _sut = new StartedByAssigneeSelector(logger);
    }

    private static AssignmentContext CreateContext(string? startedBy)
    {
        return new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "Staff" },
            StartedBy = startedBy
        };
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithStartedBy_ReturnsWorkflowInitiator()
    {
        // Arrange
        var context = CreateContext("user-123");

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("user-123");
    }

    [Fact]
    public async Task SelectAssigneeAsync_NullStartedBy_ReturnsFailure()
    {
        // Arrange
        var context = CreateContext(null);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("StartedBy");
    }

    [Fact]
    public async Task SelectAssigneeAsync_EmptyStartedBy_ReturnsFailure()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("StartedBy");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WhitespaceStartedBy_ReturnsFailure()
    {
        // Arrange
        var context = CreateContext("   ");

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("StartedBy");
    }

    [Fact]
    public async Task SelectAssigneeAsync_MetadataIncludesStrategy()
    {
        // Arrange
        var context = CreateContext("admin-user");

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("StartedBy");
        result.Metadata.Should().ContainKey("WorkflowInitiator");
        result.Metadata["WorkflowInitiator"].Should().Be("admin-user");
    }
}
