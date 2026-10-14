using FluentAssertions;
using Workflow.Workflow.Features.GetApprovalList;
using Xunit;

namespace Workflow.Tests.Workflow.Features;

public class CommitteeRoleOrderTests
{
    [Theory]
    [InlineData("Chairman", 1)]
    [InlineData("  CHAIRMAN ", 1)]
    [InlineData("director", 2)]
    [InlineData("Secretary", 9)]
    [InlineData("UW", 5)]
    [InlineData("", 5)]
    [InlineData(null, 5)]
    public void CommitteeRoleRank_MapsRole(string? role, int expected) =>
        ApprovalListProjection.CommitteeRoleRank(role).Should().Be(expected);

    [Fact]
    public void OrderByCommitteeRole_ChairmanFirst_ThenByDisplayName_FallingBackToUsername()
    {
        static ApprovalMemberStatus M(string user, string? role, string? name) =>
            new(user, role, "Pending", null, null, null, false, name);

        var ordered = ApprovalListProjection.OrderByCommitteeRole(new[]
        {
            M("u1", "UW", "Zed"), M("sec", "Secretary", "Aaa"), M("u2", "Risk", "alice"),
            M("chair", " chairman ", "Zzz"), M("dir", "Director", "Yyy"), M("u3", null, null), M("bob", "Credit", null)
        });

        // rank 5 by display name (case-insensitive): alice, bob (no name -> username), u3 (no name -> username), Zed
        ordered.Select(m => m.Username).Should().Equal("chair", "dir", "u2", "bob", "u3", "u1", "sec");
    }

    [Fact]
    public void OrderByCommitteeRole_ThaiNames_AreSortedByThaiCollation()
    {
        static ApprovalMemberStatus M(string user, string name) =>
            new(user, "UW", "Pending", null, null, null, false, name);

        var ordered = ApprovalListProjection.OrderByCommitteeRole(new[] { M("a", "สมชาย"), M("b", "กมล"), M("c", "ขวัญ") });

        ordered.Select(m => m.Username).Should().Equal("b", "c", "a");
    }
}
