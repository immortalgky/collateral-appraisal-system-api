using FluentAssertions;
using Workflow.Domain;
using Workflow.Workflow.Features.GetApprovalList;
using Xunit;

namespace Workflow.Tests.Workflow.Features;

/// <summary>
/// Verifies the read-side status mirrors the engine decision so the UI never shows "Approved" before
/// the round would actually resolve (the WaitForAll premature-badge bug).
/// </summary>
public class ApprovalListStatusTests
{
    private static List<ApprovalVote> Votes(params string[] votes) =>
        votes.Select((v, i) => ApprovalVote.Create(
            Guid.NewGuid(), Guid.NewGuid(), "pending-approval", Guid.NewGuid(),
            $"voter{i}", "Member", v, null)).ToList();

    [Fact]
    public void WaitForAll_MajorityButNotEveryoneVoted_IsPending()
    {
        // 4 approve of 5 — majority met, but one member still pending.
        var status = ApprovalListProjection.DeriveStatus(
            "WaitForAll", totalVotes: 4, totalMembers: 5,
            quorumMet: true, majorityMet: true, conditionsMet: true,
            Votes("approve", "approve", "approve", "approve"));

        status.Should().Be("Pending");
    }

    [Fact]
    public void WaitForAll_AllVotedAndMajority_IsApproved()
    {
        var status = ApprovalListProjection.DeriveStatus(
            "WaitForAll", totalVotes: 5, totalMembers: 5,
            quorumMet: true, majorityMet: true, conditionsMet: true,
            Votes("approve", "approve", "approve", "approve", "approve"));

        status.Should().Be("Approved");
    }

    [Fact]
    public void Quorum_QuorumAndMajority_IsApprovedBeforeEveryoneVotes()
    {
        // Quorum mode resolves early — 3 of 5 with quorum+majority met.
        var status = ApprovalListProjection.DeriveStatus(
            "Quorum", totalVotes: 3, totalMembers: 5,
            quorumMet: true, majorityMet: true, conditionsMet: true,
            Votes("approve", "approve", "approve"));

        status.Should().Be("Approved");
    }

    [Fact]
    public void AnyRouteBackVote_IsReturned()
    {
        var status = ApprovalListProjection.DeriveStatus(
            "WaitForAll", totalVotes: 5, totalMembers: 5,
            quorumMet: true, majorityMet: true, conditionsMet: true,
            Votes("approve", "approve", "approve", "approve", "route_back"));

        status.Should().Be("Returned");
    }

    [Fact]
    public void ConditionsNotMet_IsPending()
    {
        // All voted, majority met, but a required condition (e.g. UW must approve) is unmet.
        var status = ApprovalListProjection.DeriveStatus(
            "WaitForAll", totalVotes: 5, totalMembers: 5,
            quorumMet: true, majorityMet: true, conditionsMet: false,
            Votes("approve", "approve", "approve", "approve", "approve"));

        status.Should().Be("Pending");
    }
}
