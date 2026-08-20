using FluentAssertions;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Time;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.Features.ReleaseMeetingItem;
using Workflow.Services.Users;
using Xunit;

namespace Workflow.Tests.Meetings;

/// <summary>
/// Releasing hands the roster to the approval activity as its voting members. The handler must
/// refuse when that roster cannot satisfy the committee's rules — a round that opens without a
/// reachable quorum or required role never resolves and never reports anything.
/// </summary>
public class ReleaseMeetingItemGateTests
{
    private readonly IMeetingRepository _meetingRepository = Substitute.For<IMeetingRepository>();
    private readonly ICommitteeRepository _committeeRepository = Substitute.For<ICommitteeRepository>();
    private readonly IUserDirectory _userDirectory = Substitute.For<IUserDirectory>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public ReleaseMeetingItemGateTests()
    {
        _currentUser.Username.Returns("secretary");
        _clock.ApplicationNow.Returns(DateTime.UtcNow);

        // Default: every username asked about resolves to a real user, so these tests exercise the
        // quorum/condition rules in isolation. Overridden by the unresolved-member test below.
        _userDirectory
            .GetExistingAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlySet<string>>(
                ci.Arg<IEnumerable<string>>().ToHashSet(StringComparer.OrdinalIgnoreCase)));
    }

    private ReleaseMeetingItemCommandHandler BuildHandler() =>
        new(_meetingRepository, _committeeRepository, _userDirectory, _currentUser, _clock);

    [Fact]
    public async Task Handle_RosterBelowFixedQuorum_ThrowsConflictAndDoesNotRelease()
    {
        var committee = BuildCommittee(quorumValue: 3);
        var meeting = BuildMeetingWithRoster(committee,
            ("alice", CommitteeMemberPosition.Chairman),
            ("bob", CommitteeMemberPosition.UW));
        var appraisalId = DecisionItemId(meeting);
        Arrange(meeting, committee);

        var act = () => BuildHandler().Handle(
            new ReleaseMeetingItemCommand(meeting.Id, appraisalId), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage("*quorum requires 3*");

        meeting.DomainEvents.Should().NotContain(e => e is MeetingItemReleasedDomainEvent,
            "no workflow resume must be triggered for a roster that cannot vote the round through");
    }

    [Fact]
    public async Task Handle_RosterMissingRequiredRole_ThrowsConflict()
    {
        var committee = BuildCommittee(quorumValue: 2);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");
        var meeting = BuildMeetingWithRoster(committee,
            ("alice", CommitteeMemberPosition.Chairman),
            ("bob", CommitteeMemberPosition.Member));
        var appraisalId = DecisionItemId(meeting);
        Arrange(meeting, committee);

        var act = () => BuildHandler().Handle(
            new ReleaseMeetingItemCommand(meeting.Id, appraisalId), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage("*required role UW*");
    }

    [Fact]
    public async Task Handle_CompliantRoster_ReleasesAndRaisesTheEventWithTheRoster()
    {
        var committee = BuildCommittee(quorumValue: 2);
        committee.AddCondition(ConditionType.RoleRequired, nameof(CommitteeMemberPosition.UW),
            null, 1, "UW must approve");
        var meeting = BuildMeetingWithRoster(committee,
            ("alice", CommitteeMemberPosition.Chairman),
            ("bob", CommitteeMemberPosition.UW));
        var appraisalId = DecisionItemId(meeting);
        Arrange(meeting, committee);

        await BuildHandler().Handle(
            new ReleaseMeetingItemCommand(meeting.Id, appraisalId), CancellationToken.None);

        var evt = (MeetingItemReleasedDomainEvent)meeting.DomainEvents
            .Single(e => e is MeetingItemReleasedDomainEvent);
        evt.Members.Should().BeEquivalentTo(
        [
            new MeetingApprover("alice", nameof(CommitteeMemberPosition.Chairman)),
            new MeetingApprover("bob", nameof(CommitteeMemberPosition.UW))
        ]);
    }

    [Fact]
    public async Task Handle_RosterMemberIsNotARealUser_ThrowsConflictAndDoesNotRelease()
    {
        // A member who cannot sign in still counts toward the round's member total, so it raises
        // the majority denominator while never being able to vote.
        var committee = BuildCommittee(quorumValue: 2);
        var meeting = BuildMeetingWithRoster(committee,
            ("alice", CommitteeMemberPosition.Chairman),
            ("ghost", CommitteeMemberPosition.UW));
        var appraisalId = DecisionItemId(meeting);
        Arrange(meeting, committee);

        _userDirectory
            .GetExistingAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alice" }));

        var act = () => BuildHandler().Handle(
            new ReleaseMeetingItemCommand(meeting.Id, appraisalId), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage("*no such user: ghost*");

        meeting.DomainEvents.Should().NotContain(e => e is MeetingItemReleasedDomainEvent);
    }

    [Fact]
    public async Task Handle_UnknownMeeting_ThrowsNotFound()
    {
        _meetingRepository.GetByIdForDecisionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Meeting?)null);

        var act = () => BuildHandler().Handle(
            new ReleaseMeetingItemCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // -- Helpers --

    private void Arrange(Meeting meeting, Committee committee)
    {
        _meetingRepository.GetByIdForDecisionAsync(meeting.Id, Arg.Any<CancellationToken>())
            .Returns(meeting);
        _committeeRepository.GetByCodeAsync(MeetingCommittee.WithMeetingCode, Arg.Any<CancellationToken>())
            .Returns(committee);
    }

    private static Committee BuildCommittee(int quorumValue)
        => Committee.Create("Committee With Meeting", MeetingCommittee.WithMeetingCode, null,
            QuorumType.Fixed, quorumValue, MajorityType.Unanimous, VotingMode.WaitForAll);

    /// <summary>InvitationSent meeting (no StartAt, so the roster is still editable) with one Decision item.</summary>
    private static Meeting BuildMeetingWithRoster(
        Committee committee,
        params (string UserId, CommitteeMemberPosition Position)[] roster)
    {
        foreach (var (userId, position) in roster)
            committee.AddMember(userId, userId, position);

        var meeting = Meeting.Create("Test Meeting", null, "1/2568", 1, 2568);
        meeting.SnapshotCommittee(committee, meetingSeq: 1);
        meeting.AddItem(Guid.NewGuid(), "APR-001", 50_000_000m, 50_000_000m,
            Guid.NewGuid(), "pending-meeting", DateTime.UtcNow);
        meeting.SendInvitation(DateTime.UtcNow.AddDays(-1));
        meeting.ClearDomainEvents();

        return meeting;
    }

    private static Guid DecisionItemId(Meeting meeting)
        => meeting.Items.Single(i => i.Kind == MeetingItemKind.Decision).AppraisalId;
}
