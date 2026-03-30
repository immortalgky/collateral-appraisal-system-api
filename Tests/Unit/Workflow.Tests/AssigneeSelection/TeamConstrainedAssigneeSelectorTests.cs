using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using Workflow.AssigneeSelection.Teams;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class TeamConstrainedAssigneeSelectorTests
{
    private readonly TeamConstrainedAssigneeSelector _sut;

    public TeamConstrainedAssigneeSelectorTests()
    {
        var logger = Substitute.For<ILogger<TeamConstrainedAssigneeSelector>>();
        _sut = new TeamConstrainedAssigneeSelector(logger);
    }

    private static AssignmentContext CreateContext(List<TeamMemberInfo>? pool = null)
    {
        return new AssignmentContext
        {
            ActivityName = "test-activity",
            UserGroups = new List<string> { "Appraisers" },
            CandidatePool = pool
        };
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithCandidates_ReturnsOneFromPool()
    {
        // Arrange
        var pool = new List<TeamMemberInfo>
        {
            new("user-1", "Alice", "team-a", new List<string> { "Appraiser" }),
            new("user-2", "Bob", "team-a", new List<string> { "Appraiser" }),
            new("user-3", "Charlie", "team-a", new List<string> { "Appraiser" })
        };
        var context = CreateContext(pool);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        pool.Select(m => m.UserId).Should().Contain(result.AssigneeId);
    }

    [Fact]
    public async Task SelectAssigneeAsync_NullPool_ReturnsFailure()
    {
        // Arrange
        var context = CreateContext(null);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No candidates");
    }

    [Fact]
    public async Task SelectAssigneeAsync_EmptyPool_ReturnsFailure()
    {
        // Arrange
        var context = CreateContext(new List<TeamMemberInfo>());

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No candidates");
    }

    [Fact]
    public async Task SelectAssigneeAsync_SingleCandidate_ReturnsThatCandidate()
    {
        // Arrange
        var pool = new List<TeamMemberInfo>
        {
            new("only-user", "Solo", "team-x", new List<string> { "Reviewer" })
        };
        var context = CreateContext(pool);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().Be("only-user");
    }

    [Fact]
    public async Task SelectAssigneeAsync_MetadataIncludesPoolSizeAndTeamId()
    {
        // Arrange
        var pool = new List<TeamMemberInfo>
        {
            new("user-1", "Alice", "team-alpha", new List<string> { "Appraiser" }),
            new("user-2", "Bob", "team-alpha", new List<string> { "Appraiser" })
        };
        var context = CreateContext(pool);

        // Act
        var result = await _sut.SelectAssigneeAsync(context);

        // Assert
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("TeamConstrained");
        result.Metadata.Should().ContainKey("PoolSize");
        result.Metadata["PoolSize"].Should().Be(2);
        result.Metadata.Should().ContainKey("TeamId");
    }
}
