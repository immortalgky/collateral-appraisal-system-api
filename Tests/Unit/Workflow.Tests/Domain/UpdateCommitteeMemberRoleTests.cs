using FluentAssertions;
using NSubstitute;
using Shared.Exceptions;
using Workflow.Domain.Committees;
using Workflow.Workflow.Features.Committees.UpdateCommitteeMember;
using Xunit;

namespace Workflow.Tests.Domain;

/// <summary>
/// Updating a committee member is a WHOLE-RECORD write (Role + Attendance + IsActive), so an admin
/// deactivating or re-scheduling a member added before Risk/Appraisal/Credit/Member were retired
/// sends that retired role straight back. Rejecting it outright would strand those members — the
/// rule is that only a CHANGE has to land on a currently-assignable position.
/// </summary>
public class UpdateCommitteeMemberRoleTests
{
    [Fact]
    public async Task Deactivating_AMemberHoldingARetiredRole_IsAllowed()
    {
        var (handler, committee, memberId) = Build(CommitteeMemberPosition.Risk);

        await handler.Handle(
            Command(committee.Id, memberId, nameof(CommitteeMemberPosition.Risk), isActive: false),
            CancellationToken.None);

        var member = committee.Members.Single(m => m.Id == memberId);
        member.IsActive.Should().BeFalse();
        member.Position.Should().Be(CommitteeMemberPosition.Risk, "an untouched role must survive");
    }

    [Fact]
    public async Task MovingARetiredRoleOntoASelectableOne_IsAllowed()
    {
        // The cleanup path: Committee Admin must always be able to migrate a legacy member.
        var (handler, committee, memberId) = Build(CommitteeMemberPosition.Risk);

        await handler.Handle(
            Command(committee.Id, memberId, nameof(CommitteeMemberPosition.Director)),
            CancellationToken.None);

        committee.Members.Single(m => m.Id == memberId)
            .Position.Should().Be(CommitteeMemberPosition.Director);
    }

    [Fact]
    public async Task AssigningARetiredRoleToAMemberWhoDidNotHoldIt_IsRejected()
    {
        var (handler, committee, memberId) = Build(CommitteeMemberPosition.Chairman);

        var act = () => handler.Handle(
            Command(committee.Id, memberId, nameof(CommitteeMemberPosition.Risk)),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*retired*");
    }

    [Fact]
    public async Task AnUnknownRoleName_IsRejected()
    {
        var (handler, committee, memberId) = Build(CommitteeMemberPosition.Chairman);

        var act = () => handler.Handle(
            Command(committee.Id, memberId, "Underwriter"),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Invalid Role*");
    }

    [Fact]
    public async Task TheNumericFormOfAPosition_IsRejected()
    {
        // Enum.TryParse would map "3" to UW; roles are compared as raw strings downstream.
        var (handler, committee, memberId) = Build(CommitteeMemberPosition.Chairman);

        var act = () => handler.Handle(
            Command(committee.Id, memberId, "3"), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Invalid Role*");
    }

    // -- Helpers --

    private static UpdateCommitteeMemberCommand Command(
        Guid committeeId, Guid memberId, string role, bool isActive = true) =>
        new(committeeId, memberId,
            new UpdateCommitteeMemberRequest(role, nameof(CommitteeAttendance.Always), isActive));

    private static (UpdateCommitteeMemberCommandHandler Handler, Committee Committee, Guid MemberId)
        Build(CommitteeMemberPosition position)
    {
        var committee = Committee.Create("C", "C", null,
            QuorumType.Fixed, 1, MajorityType.Simple, VotingMode.WaitForAll);

        var member = committee.AddMember("legacy", "Legacy Member", position);

        var repository = Substitute.For<ICommitteeRepository>();
        repository.GetByIdWithMembersAsync(committee.Id, Arg.Any<CancellationToken>())
            .Returns(committee);

        return (new UpdateCommitteeMemberCommandHandler(repository), committee, member.Id);
    }
}
