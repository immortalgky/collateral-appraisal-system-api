using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Workflow.Data.Repository;
using Workflow.Services.Groups;
using Workflow.Services.Hashing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class RoundRobinAssigneeSelectorTests
{
    private readonly RoundRobinAssigneeSelector _selector;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUserGroupService _userGroupService;
    private readonly IGroupHashService _groupHashService;
    private readonly ILogger<RoundRobinAssigneeSelector> _logger;

    public RoundRobinAssigneeSelectorTests()
    {
        _assignmentRepository = Substitute.For<IAssignmentRepository>();
        _userGroupService = Substitute.For<IUserGroupService>();
        _groupHashService = Substitute.For<IGroupHashService>();
        _logger = Substitute.For<ILogger<RoundRobinAssigneeSelector>>();
        
        _selector = new RoundRobinAssigneeSelector(
            _assignmentRepository,
            _userGroupService,
            _groupHashService,
            _logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithValidUserGroups_ShouldReturnAssignee()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "RoundRobinTest",
            UserGroups = new List<string> { "Appraisers", "Seniors" }
        };

        var eligibleUsers = new List<string> { "user1@test.com", "user2@test.com", "user3@test.com" };
        var groupsHash = "hash123";
        var groupsList = "Appraisers,Seniors";

        _groupHashService.GenerateGroupsHash(context.UserGroups).Returns(groupsHash);
        _groupHashService.GenerateGroupsList(context.UserGroups).Returns(groupsList);
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(context.ActivityName, groupsHash, Arg.Any<CancellationToken>())
            .Returns("user2@test.com");

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("user2@test.com");

        // Verify all dependencies were called correctly
        await _userGroupService.Received(1).GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>());
        await _assignmentRepository.Received(1).SyncUsersForGroupCombinationAsync(
            context.ActivityName, groupsHash, groupsList, eligibleUsers, Arg.Any<CancellationToken>());
        await _assignmentRepository.Received(1).SelectNextUserWithRoundResetAsync(
            context.ActivityName, groupsHash, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithEmptyUserGroups_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "EmptyGroupsTest",
            UserGroups = new List<string>()
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No user groups specified");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithNullUserGroups_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "NullGroupsTest",
            UserGroups = null!
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No user groups specified");
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

        _groupHashService.GenerateGroupsHash(context.UserGroups).Returns("hash456");
        _groupHashService.GenerateGroupsList(context.UserGroups).Returns("EmptyGroup");
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No eligible users found");
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithRepositoryReturningNull_ShouldReturnFailure()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "NullReturnTest",
            UserGroups = new List<string> { "TestGroup" }
        };

        var eligibleUsers = new List<string> { "user1@test.com" };
        
        _groupHashService.GenerateGroupsHash(context.UserGroups).Returns("hash789");
        _groupHashService.GenerateGroupsList(context.UserGroups).Returns("TestGroup");
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to select next user");
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

        _groupHashService.When(x => x.GenerateGroupsHash(context.UserGroups))
            .Do(x => throw new Exception("Hash generation failed"));

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Hash generation failed");
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

    [Fact]
    public async Task SelectAssigneeAsync_ShouldSyncUsersBeforeSelection()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "SyncTest",
            UserGroups = new List<string> { "TestGroup" }
        };

        var eligibleUsers = new List<string> { "user1@test.com", "user2@test.com" };
        var groupsHash = "syncHash";
        var groupsList = "TestGroup";

        _groupHashService.GenerateGroupsHash(context.UserGroups).Returns(groupsHash);
        _groupHashService.GenerateGroupsList(context.UserGroups).Returns(groupsList);
        _userGroupService.GetUsersInGroupsAsync(context.UserGroups, Arg.Any<CancellationToken>())
            .Returns(eligibleUsers);
        _assignmentRepository.SelectNextUserWithRoundResetAsync(context.ActivityName, groupsHash, Arg.Any<CancellationToken>())
            .Returns("user1@test.com");

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        // Verify sync was called before selection
        Received.InOrder(() =>
        {
            _assignmentRepository.SyncUsersForGroupCombinationAsync(
                context.ActivityName, groupsHash, groupsList, eligibleUsers, Arg.Any<CancellationToken>());
            _assignmentRepository.SelectNextUserWithRoundResetAsync(
                context.ActivityName, groupsHash, Arg.Any<CancellationToken>());
        });
    }
}