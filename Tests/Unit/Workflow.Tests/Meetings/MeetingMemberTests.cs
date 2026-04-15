using FluentAssertions;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Tests.Meetings;

public class MeetingMemberTests
{
    // -------------------------------------------------------------------------
    // CreateSnapshot
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateSnapshot_CopiesUserId_MemberName_And_Position()
    {
        var meetingId = Guid.NewGuid();
        var committeeMember = BuildCommitteeMember("user-42", "Jane Doe", CommitteeMemberPosition.Secretary);

        var member = MeetingMember.CreateSnapshot(meetingId, committeeMember);

        member.MeetingId.Should().Be(meetingId);
        member.UserId.Should().Be("user-42");
        member.MemberName.Should().Be("Jane Doe");
        member.Position.Should().Be(CommitteeMemberPosition.Secretary);
    }

    [Fact]
    public void CreateSnapshot_StoresSourceCommitteeMemberId()
    {
        var meetingId = Guid.NewGuid();
        var committeeMember = BuildCommitteeMember("user-42", "Jane Doe", CommitteeMemberPosition.Chairman);

        var member = MeetingMember.CreateSnapshot(meetingId, committeeMember);

        member.SourceCommitteeMemberId.Should().Be(committeeMember.Id);
    }

    [Fact]
    public void CreateSnapshot_NullCommitteeMember_Throws()
    {
        var act = () => MeetingMember.CreateSnapshot(Guid.NewGuid(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // CreateManual
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateManual_Leaves_SourceCommitteeMemberId_Null()
    {
        var member = MeetingMember.CreateManual(
            Guid.NewGuid(), "user-99", "Bob Smith", CommitteeMemberPosition.Member);

        member.SourceCommitteeMemberId.Should().BeNull();
    }

    [Fact]
    public void CreateManual_StoresUserIdAndName()
    {
        var meetingId = Guid.NewGuid();
        var member = MeetingMember.CreateManual(meetingId, "user-99", "Bob Smith", CommitteeMemberPosition.Risk);

        member.MeetingId.Should().Be(meetingId);
        member.UserId.Should().Be("user-99");
        member.MemberName.Should().Be("Bob Smith");
        member.Position.Should().Be(CommitteeMemberPosition.Risk);
    }

    [Fact]
    public void CreateManual_EmptyUserId_Throws()
    {
        var act = () => MeetingMember.CreateManual(Guid.NewGuid(), "", "Bob", CommitteeMemberPosition.Member);

        act.Should().Throw<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // UpdatePosition
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdatePosition_PersistsNewPosition()
    {
        var member = MeetingMember.CreateManual(
            Guid.NewGuid(), "user-1", "Alice", CommitteeMemberPosition.Member);

        member.UpdatePosition(CommitteeMemberPosition.Director);

        member.Position.Should().Be(CommitteeMemberPosition.Director);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static CommitteeMember BuildCommitteeMember(
        string userId, string memberName, CommitteeMemberPosition position)
    {
        // CommitteeMember.Create is internal; use Committee.AddMember to get one.
        var committee = Committee.Create(
            "Test Committee", "TC", null,
            QuorumType.Fixed, 3, MajorityType.Simple);

        return committee.AddMember(userId, memberName, position);
    }
}
