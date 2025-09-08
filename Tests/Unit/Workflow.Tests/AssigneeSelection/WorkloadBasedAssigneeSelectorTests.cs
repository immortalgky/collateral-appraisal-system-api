using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Workflow.Data.Repository;
using Workflow.Services.Groups;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class WorkloadBasedAssigneeSelectorTests
{
    private readonly WorkloadBasedAssigneeSelector _selector;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUserGroupService _userGroupService;
    private readonly ILogger<WorkloadBasedAssigneeSelector> _logger;

    public WorkloadBasedAssigneeSelectorTests()
    {
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _userGroupService = Substitute.For<IUserGroupService>();
        _logger = Substitute.For<ILogger<WorkloadBasedAssigneeSelector>>();
        
        _selector = new WorkloadBasedAssigneeSelector(
            _assignmentRepository,
            _userGroupService,
            _logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithMultipleUsers_ShouldSelectUserWithLowestWorkload()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "WorkloadTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        var eligibleUsers = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" };
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);

        // Set up workloads: user2 has the lowest workload
        _assignmentRepository.GetActiveTaskCountForUserAsync("user1@test.com", Arg.Any<CancellationToken>())
            .Returns(5);
        _assignmentRepository.GetActiveTaskCountForUserAsync("user2@test.com", Arg.Any<CancellationToken>())
            .Returns(2); // Lowest workload
        _assignmentRepository.GetActiveTaskCountForUserAsync("user3@test.com", Arg.Any<CancellationToken>())
            .Returns(7);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("user2@test.com");
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("WorkloadBased");
        result.Metadata.Should().ContainKey("SelectedUserWorkload");
        result.Metadata["SelectedUserWorkload"].Should().Be(2);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithEqualWorkloads_ShouldSelectFirstAlphabetically()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "EqualWorkloadTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        // Order matters: the selector will sort by UserId as secondary sort
        var eligibleUsers = new List<string> { "user-charlie@test.com", "user-alice@test.com", "user-bravo@test.com" };
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);

        // All users have equal workload
        _assignmentRepository.GetActiveTaskCountForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(3);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        // Should select first user alphabetically (secondary sort by UserId)
        result.AssigneeId.Should().Be("user-alice@test.com");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithSpecifiedUserGroups_ShouldUseThoseGroups()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "SpecificGroupsTest",
            UserGroups = new List<string> { "Specialists", "Seniors" }
        };

        var eligibleUsers = new List<string> { "specialist@test.com" };
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);
        _assignmentRepository.GetActiveTaskCountForUserAsync("specialist@test.com", Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("specialist@test.com");

        // Verify the correct groups were used
        await _userGroupService.Received(1).GetUsersInGroupsAsync(
            Arg.Is<List<string>>(groups => groups.Contains("Specialists") && groups.Contains("Seniors")), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNoUserGroups_ShouldUseDefaultGroups()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "DefaultGroupsTest",
            UserGroups = new List<string>()
        };

        var defaultUsers = new List<string> { "default-user@test.com" };
        _userGroupService.GetUsersInGroupsAsync(
                Arg.Is<List<string>>(groups => groups.Contains("Seniors") && groups.Contains("Juniors")), 
                Arg.Any<CancellationToken>())
            .Returns(defaultUsers);
        _assignmentRepository.GetActiveTaskCountForUserAsync("default-user@test.com", Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("default-user@test.com");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNoEligibleUsers_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "NoUsersTest",
            UserGroups = new List<string> { "EmptyGroup" }
        };

        _userGroupService.GetUsersInGroupsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No eligible users found");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithException_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "ExceptionTest",
            UserGroups = new List<string> { "TestGroup" }
        };

        _userGroupService.GetUsersInGroupsAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<List<string>>(new Exception("Database connection failed")));

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Selection failed");
        result.ErrorMessage.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task SelectAssigneeAsync_ShouldIncludeAllWorkloadsInMetadata()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "MetadataTest",
            UserGroups = new List<string> { "TestGroup" }
        };

        var eligibleUsers = new List<string> { "user1@test.com", "user2@test.com" };
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);

        _assignmentRepository.GetActiveTaskCountForUserAsync("user1@test.com", Arg.Any<CancellationToken>())
            .Returns(3);
        _assignmentRepository.GetActiveTaskCountForUserAsync("user2@test.com", Arg.Any<CancellationToken>())
            .Returns(1);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().ContainKey("AllWorkloads");
        result.Metadata.Should().ContainKey("EligibleUserCount");
        result.Metadata["EligibleUserCount"].Should().Be(2);
        
        var allWorkloads = result.Metadata!["AllWorkloads"] as Dictionary<string, int>;
        allWorkloads.Should().NotBeNull();
        allWorkloads!["user1@test.com"].Should().Be(3);
        allWorkloads["user2@test.com"].Should().Be(1);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "CancellationTest",
            UserGroups = new List<string> { "TestGroup" }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _selector.SelectAssigneeAsync(context, cts.Token));
    }
}