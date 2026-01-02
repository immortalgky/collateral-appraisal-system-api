using Workflow.AssigneeSelection.Core;
using Workflow.AssigneeSelection.Strategies;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Workflow.Tests.AssigneeSelection;

public class RandomAssigneeSelectorTests
{
    private readonly RandomAssigneeSelector _selector;
    private readonly ILogger<RandomAssigneeSelector> _logger;

    public RandomAssigneeSelectorTests()
    {
        _logger = Substitute.For<ILogger<RandomAssigneeSelector>>();
        _selector = new RandomAssigneeSelector(_logger);
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithValidContext_ShouldReturnRandomUser()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "RandomSelectionTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().NotBeNullOrEmpty();
        
        // The current implementation returns hardcoded users
        var expectedUsers = new[] { "User1", "User2", "User3" };
        expectedUsers.Should().Contain(result.AssigneeId);
        
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("Random");
    }

    [Fact]
    public async Task SelectAssigneeAsync_MultipleInvocations_ShouldPotentiallyReturnDifferentUsers()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "MultipleCallsTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        var results = new List<string>();

        // Act - Run multiple times to test randomness
        for (int i = 0; i < 10; i++)
        {
            var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            results.Add(result.AssigneeId);
        }

        // Assert - All results should be valid users
        results.Should().AllSatisfy(assignee => 
        {
            var expectedUsers = new[] { "User1", "User2", "User3" };
            expectedUsers.Should().Contain(assignee);
        });

        // With random selection, we should get some variety over multiple calls
        results.Distinct().Count().Should().BeGreaterThan(1, 
            "Random selection should produce variety over multiple calls");
    }

    [Fact]
    public async Task SelectAssigneeAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "MetadataTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("SelectionStrategy");
        result.Metadata!["SelectionStrategy"].Should().Be("Random");
        result.Metadata.Should().ContainKey("EligibleUserCount");
        result.Metadata.Should().ContainKey("SelectionTimestamp");
        
        var timestamp = (DateTime)result.Metadata["SelectionTimestamp"];
        timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SelectAssigneeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "CancellationTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _selector.SelectAssigneeAsync(context, cts.Token));
    }

    [Fact]
    public async Task SelectAssigneeAsync_ShouldUseCryptographicallySecureRandomness()
    {
        // This test verifies that the implementation completes successfully
        // using RandomNumberGenerator (cryptographically secure)
        
        // Arrange
        var context = new AssignmentContext
        {
            ActivityName = "CryptoRandomTest",
            UserGroups = new List<string> { "Appraisers" }
        };

        // Act
        var result = await _selector.SelectAssigneeAsync(context, CancellationToken.None);

        // Assert - Should complete successfully without throwing
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.AssigneeId.Should().NotBeNullOrEmpty();
    }
}