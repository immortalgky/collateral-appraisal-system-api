using Assignment.AssigneeSelection.Core;
using Assignment.AssigneeSelection.Strategies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Assignment.Tests.AssigneeSelection;

public class ManualAssigneeSelectorTests
{
    private readonly ManualAssigneeSelector _selector;
    private readonly ILogger<ManualAssigneeSelector> _logger;

    public ManualAssigneeSelectorTests()
    {
        _logger = Substitute.For<ILogger<ManualAssigneeSelector>>();
        _selector = new ManualAssigneeSelector(_logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithValidUserCode_ShouldAssignToSpecificUser()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "ManualUserAssignment",
            UserCode = "specific-user@test.com",
            UserGroups = new List<string> { "Appraisers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("specific-user@test.com");
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("Manual");
        result.Metadata.Should().ContainKey("AssignmentType");
        result.Metadata["AssignmentType"].Should().Be("User");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithValidUserGroups_ShouldAssignToGroup()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "ManualGroupAssignment",
            UserGroups = new List<string> { "Senior-Appraisers", "Managers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().BeNull(); // Group assignment returns null assignee
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("Manual");
        result.Metadata.Should().ContainKey("AssignmentType");
        result.Metadata["AssignmentType"].Should().Be("Group");
        result.Metadata.Should().ContainKey("AssignedGroup");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithBothUserAndGroups_ShouldPreferUser()
    {
        // Arrange - User assignment has higher priority than group
        var context = new AssignmentContext
        {
            ActivityName = "UserPriorityTest",
            UserCode = "priority-user@test.com",
            UserGroups = new List<string> { "Secondary-Group" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("priority-user@test.com");
        result.Metadata!["AssignmentType"].Should().Be("User");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithEmptyUserCode_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "EmptyUserTest",
            UserCode = "", // Invalid empty user
            UserGroups = new List<string>()
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not eligible");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithWhitespaceUserCode_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "WhitespaceUserTest",
            UserCode = "   ", // Invalid whitespace-only user
            UserGroups = new List<string>()
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not eligible");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithEmptyGroups_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "EmptyGroupsTest",
            UserGroups = new List<string>() // Empty groups
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not eligible");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNoUserOrGroups_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "NoAssignmentTest",
            UserGroups = new List<string>()
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Manual assignment requires");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithAssignerInProperties_ShouldIncludeInMetadata()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "AssignerPropertiesTest",
            UserCode = "assigned-user@test.com",
            Properties = new Dictionary<string, object>
            {
                ["AssignedBy"] = "manager@test.com"
            }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().ContainKey("AssignedBy");
        result.Metadata!["AssignedBy"].Should().Be("manager@test.com");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNoAssignerInProperties_ShouldDefaultToSystem()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "DefaultAssignerTest",
            UserCode = "assigned-user@test.com"
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().ContainKey("AssignedBy");
        result.Metadata!["AssignedBy"].Should().Be("system");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "CancellationTest",
            UserCode = "test-user@test.com"
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _selector.SelectAssigneeAsync(context, cts.Token));
    }
}