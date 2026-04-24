using FluentAssertions;
using Shared.Exceptions;
using Workflow.Domain.Committees;

namespace Workflow.Tests.Meetings;

public class CommitteeTests
{
    // =========================================================================
    // CommitteeMember.Create with explicit attendance
    // =========================================================================

    [Fact]
    public void CommitteeMember_Create_DefaultAttendanceIsAlways()
    {
        var committee = BuildCommittee();

        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Chairman);

        member.Attendance.Should().Be(CommitteeAttendance.Always);
    }

    [Fact]
    public void CommitteeMember_Create_WithExplicitAttendance_PersistsIt()
    {
        var committee = BuildCommittee();

        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Chairman, CommitteeAttendance.Odd);

        member.Attendance.Should().Be(CommitteeAttendance.Odd);
    }

    [Fact]
    public void CommitteeMember_Create_EvenAttendance_PersistsIt()
    {
        var committee = BuildCommittee();

        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Chairman, CommitteeAttendance.Even);

        member.Attendance.Should().Be(CommitteeAttendance.Even);
    }

    // =========================================================================
    // Committee.AddMember with attendance parameter
    // =========================================================================

    [Fact]
    public void AddMember_WithAttendance_PropagatesAttendanceToMember()
    {
        var committee = BuildCommittee();

        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Chairman, CommitteeAttendance.Even);

        committee.Members.Should().ContainSingle(m => m.UserId == "u1");
        committee.Members.Single(m => m.UserId == "u1").Attendance.Should().Be(CommitteeAttendance.Even);
    }

    [Fact]
    public void AddMember_WithoutAttendance_DefaultsToAlways()
    {
        var committee = BuildCommittee();

        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Chairman);

        member.Attendance.Should().Be(CommitteeAttendance.Always);
    }

    // =========================================================================
    // Committee.UpdateMember
    // =========================================================================

    [Fact]
    public void UpdateMember_ChangesPosition()
    {
        var committee = BuildCommittee();
        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Member);

        committee.UpdateMember(member.Id, CommitteeMemberPosition.Chairman, CommitteeAttendance.Always, isActive: true);

        committee.Members.Single(m => m.Id == member.Id).Position.Should().Be(CommitteeMemberPosition.Chairman);
    }

    [Fact]
    public void UpdateMember_ChangesAttendance()
    {
        var committee = BuildCommittee();
        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Member);

        committee.UpdateMember(member.Id, CommitteeMemberPosition.Member, CommitteeAttendance.Odd, isActive: true);

        committee.Members.Single(m => m.Id == member.Id).Attendance.Should().Be(CommitteeAttendance.Odd);
    }

    [Fact]
    public void UpdateMember_Deactivate_SetsIsActiveFalse()
    {
        var committee = BuildCommittee();
        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Member);

        committee.UpdateMember(member.Id, CommitteeMemberPosition.Member, CommitteeAttendance.Always, isActive: false);

        committee.Members.Single(m => m.Id == member.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateMember_Reactivate_SetsIsActiveTrue()
    {
        var committee = BuildCommittee();
        var member = committee.AddMember("u1", "Alice", CommitteeMemberPosition.Member);
        committee.RemoveMember(member.Id); // deactivates

        committee.UpdateMember(member.Id, CommitteeMemberPosition.Director, CommitteeAttendance.Even, isActive: true);

        committee.Members.Single(m => m.Id == member.Id).IsActive.Should().BeTrue();
        committee.Members.Single(m => m.Id == member.Id).Position.Should().Be(CommitteeMemberPosition.Director);
        committee.Members.Single(m => m.Id == member.Id).Attendance.Should().Be(CommitteeAttendance.Even);
    }

    [Fact]
    public void UpdateMember_UnknownMemberId_ThrowsNotFoundException()
    {
        var committee = BuildCommittee();

        var act = () => committee.UpdateMember(
            Guid.NewGuid(), CommitteeMemberPosition.Member, CommitteeAttendance.Always, isActive: true);

        act.Should().Throw<NotFoundException>();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Committee BuildCommittee()
        => Committee.Create("Test Committee", "TC", null, QuorumType.Fixed, 2, MajorityType.Simple);
}
