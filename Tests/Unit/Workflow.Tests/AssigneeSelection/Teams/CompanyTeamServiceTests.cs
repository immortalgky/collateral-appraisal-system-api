using FluentAssertions;
using Workflow.AssigneeSelection.Teams;
using Xunit;

namespace Workflow.Tests.AssigneeSelection.Teams;

/// <summary>
/// Role resolution is now schema-driven (assigneeRole property on each activity).
/// The hardcoded ActivityToRoleMap and ResolveRoleName have been removed.
/// These tests verify the ITeamService interface contract remains correct.
/// </summary>
public class CompanyTeamServiceTests
{
    [Fact]
    public void ITeamService_GetTeamMembersForActivityAsync_AcceptsRoleName()
    {
        // Verify the interface method signature accepts a roleName parameter
        var method = typeof(ITeamService).GetMethod("GetTeamMembersForActivityAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].Name.Should().Be("teamId");
        parameters[1].Name.Should().Be("roleName");
    }

    [Fact]
    public void ITeamService_GetAllMembersForActivityAsync_AcceptsRoleName()
    {
        var method = typeof(ITeamService).GetMethod("GetAllMembersForActivityAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].Name.Should().Be("roleName");
    }
}
