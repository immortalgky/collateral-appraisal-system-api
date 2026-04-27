using FluentAssertions;
using Workflow.Tasks.Authorization;

namespace Workflow.Tests.Tasks.Authorization;

public class PoolTaskAccessTests
{
    // ── BuildSqlClause ──

    [Fact]
    public void BuildSqlClause_EmptyGroups_ReturnsNull()
    {
        var result = PoolTaskAccess.BuildSqlClause([], null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildSqlClause_SingleGroupNoTeam_ContainsGroupOnly()
    {
        var result = PoolTaskAccess.BuildSqlClause(["ExtAdmin"], null, null);

        result.Should().NotBeNull();
        result!.Parameters.Should().ContainKey("PoolAssignee0").WhoseValue.Should().Be("ExtAdmin");
        result.Parameters.Should().NotContainKey("PoolAssignee1");
    }

    [Fact]
    public void BuildSqlClause_SingleGroupWithTeam_ContainsGroupAndTeamVariant()
    {
        var teamId = "019d1b89-100e-7ab6-9a8c-c5b52d12e364";
        var result = PoolTaskAccess.BuildSqlClause(["ExtAdmin"], teamId, null);

        result.Should().NotBeNull();
        var values = result!.Parameters
            .Where(p => p.Key.StartsWith("PoolAssignee"))
            .Select(p => (string?)p.Value)
            .ToHashSet();

        values.Should().Contain("ExtAdmin");
        values.Should().Contain($"ExtAdmin:Team_{teamId}");
    }

    [Fact]
    public void BuildSqlClause_MultiGroupWithTeam_ContainsFourCandidates()
    {
        var teamId = "abc-123";
        var result = PoolTaskAccess.BuildSqlClause(["Group1", "Group2"], teamId, null);

        result.Should().NotBeNull();
        var values = result!.Parameters
            .Where(p => p.Key.StartsWith("PoolAssignee"))
            .Select(p => (string?)p.Value)
            .ToHashSet();

        values.Should().Contain("Group1");
        values.Should().Contain($"Group1:Team_{teamId}");
        values.Should().Contain("Group2");
        values.Should().Contain($"Group2:Team_{teamId}");
        values.Should().HaveCount(4);
    }

    [Fact]
    public void BuildSqlClause_SqlContainsInClauseAndCompanyCondition()
    {
        var result = PoolTaskAccess.BuildSqlClause(["ExtAdmin"], null, null);

        result.Should().NotBeNull();
        result!.Sql.Should().Contain("AssigneeUserId IN (");
        result.Sql.Should().Contain("AssigneeCompanyId IS NULL OR AssigneeCompanyId = @PoolCallerCompanyId");
    }

    [Fact]
    public void BuildSqlClause_CompanyIdInParameters()
    {
        var companyId = Guid.NewGuid();
        var result = PoolTaskAccess.BuildSqlClause(["ExtAdmin"], null, companyId);

        result.Should().NotBeNull();
        result!.Parameters["PoolCallerCompanyId"].Should().Be(companyId);
    }

    [Fact]
    public void BuildSqlClause_NullCompanyId_ParameterIsNull()
    {
        var result = PoolTaskAccess.BuildSqlClause(["ExtAdmin"], null, null);

        result.Should().NotBeNull();
        result!.Parameters["PoolCallerCompanyId"].Should().BeNull();
    }

    // ── IsOwner ──

    [Fact]
    public void IsOwner_GroupOnlyMatch_ReturnsTrue()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: null,
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: null);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_TeamScopedMatch_ReturnsTrue()
    {
        var teamId = "019d1b89-100e-7ab6-9a8c-c5b52d12e364";
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: $"ExtAdmin:Team_{teamId}",
            assigneeCompanyId: null,
            userGroups: ["ExtAdmin"],
            userTeamId: teamId,
            callerCompanyId: null);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_TeamMismatch_ReturnsFalse()
    {
        var teamX = "019d1b89-100e-7ab6-9a8c-c5b52d12e364";
        var teamY = "deadbeef-dead-beef-dead-beefdeadbeef";
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: $"ExtAdmin:Team_{teamX}",
            assigneeCompanyId: null,
            userGroups: ["ExtAdmin"],
            userTeamId: teamY,
            callerCompanyId: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_GroupNotHeld_ReturnsFalse()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: null,
            userGroups: ["OtherGroup"],
            userTeamId: null,
            callerCompanyId: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_AssigneeCompanyNull_MatchesRegardlessOfCallerCompany()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: null,
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_AssigneeCompanySet_CallerCompanyMatches_ReturnsTrue()
    {
        var companyId = Guid.NewGuid();

        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: companyId,
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: companyId);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOwner_AssigneeCompanySet_CallerCompanyDiffers_ReturnsFalse()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: Guid.NewGuid(),
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_AssigneeCompanySet_CallerCompanyNull_ReturnsFalse()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: Guid.NewGuid(),
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_EmptyGroups_ReturnsFalse()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: "ExtAdmin",
            assigneeCompanyId: null,
            userGroups: [],
            userTeamId: null,
            callerCompanyId: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_NullAssigneeUserId_ReturnsFalse()
    {
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: null,
            assigneeCompanyId: null,
            userGroups: ["ExtAdmin"],
            userTeamId: null,
            callerCompanyId: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwner_MultiGroupWithTeam_MatchesAnyGroupVariant()
    {
        // Caller is in Group1 and Group2, team X — row is assigned to Group2:Team_X
        var teamId = "019d1b89-100e-7ab6-9a8c-c5b52d12e364";
        var result = PoolTaskAccess.IsOwner(
            assigneeUserId: $"Group2:Team_{teamId}",
            assigneeCompanyId: null,
            userGroups: ["Group1", "Group2"],
            userTeamId: teamId,
            callerCompanyId: null);

        result.Should().BeTrue();
    }
}
