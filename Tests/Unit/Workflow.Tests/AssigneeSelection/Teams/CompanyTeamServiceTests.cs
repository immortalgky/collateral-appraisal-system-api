using FluentAssertions;
using Workflow.AssigneeSelection.Teams;
using Xunit;

namespace Workflow.Tests.AssigneeSelection.Teams;

/// <summary>
/// Group resolution is now schema-driven (assigneeGroup property on each activity).
/// These tests verify the ITeamService interface contract remains correct.
/// </summary>
public class CompanyTeamServiceTests
{
    [Fact]
    public void ITeamService_GetTeamMembersForActivityAsync_AcceptsGroupName()
    {
        var method = typeof(ITeamService).GetMethod("GetTeamMembersForActivityAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].Name.Should().Be("teamId");
        parameters[1].Name.Should().Be("groupName");
    }

    [Fact]
    public void ITeamService_GetAllMembersForActivityAsync_AcceptsGroupName()
    {
        var method = typeof(ITeamService).GetMethod("GetAllMembersForActivityAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].Name.Should().Be("groupName");
    }
}
