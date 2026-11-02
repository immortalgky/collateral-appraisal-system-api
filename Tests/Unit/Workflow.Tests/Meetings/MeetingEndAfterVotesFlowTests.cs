using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Data;
using Shared.Data.Interceptors;
using Shared.Data.Outbox;
using Shared.DDD;
using Shared.Exceptions;
using Shared.Messaging.Events;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain.Committees;
using Workflow.Infrastructure;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.EventHandlers;
using Workflow.Meetings.Features.EndMeeting;
using Workflow.Meetings.Infrastructure;
using Workflow.Meetings.ReadModels;
using Workflow.Sla.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;

namespace Workflow.Tests.Meetings;

/// <summary>
/// CA-564: a meeting ends when every released item's committee approval round has concluded — not
/// when the secretary releases the last item.
///
/// Drives the REAL WorkflowEngine over the REAL appraisal-workflow.json with the real Meeting
/// aggregate, ApprovalActivity, MeetingItemReleased/MeetingEnded handlers AND the real
/// DispatchDomainEventInterceptor (its mediator forwards to the real handlers), so the
/// MeetingEndedDomainEvent raised from inside ApprovalActivity is proven to dispatch during the
/// engine's checkpoint SaveChanges, including the handler's nested SaveChanges. Same substitutions
/// as <see cref="MeetingRouteBackReturnFlowTests"/>.
/// </summary>
public class MeetingEndAfterVotesFlowTests
{
    private static readonly string[] Voters = ["chair-cwm", "uw-cwm", "member-cwm", "A"];

    [Fact]
    public async Task ReleasingAllItems_WaitsForVotes_AndEndsWhenTheLastRoundConcludes()
    {
        using var flow = new Flow();
        await flow.SeedAsync();
        var first = await flow.QueueAppraisalAsync();
        var second = await flow.QueueAppraisalAsync();
        await flow.CutOffAndInviteAsync();

        // First release: more to release, nothing changes.
        await flow.ReleaseAsync(first);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.InvitationSent);

        // Last release: no longer ends the meeting. It waits for the votes.
        await flow.ReleaseAsync(second);
        var awaiting = await flow.LoadMeetingAsync();
        awaiting.Status.Should().Be(MeetingStatus.AwaitingVotes);
        awaiting.EndedAt.Should().BeNull();
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Included, "the meeting is not over");
        (await flow.LoadInstanceAsync(first)).CurrentActivityId.Should().Be("pending-approval");
        (await flow.LoadInstanceAsync(second)).CurrentActivityId.Should().Be("pending-approval");

        // The EndMeeting handler refuses while rounds are open.
        var open = async () => await flow.EndMeetingAsync();
        (await open.Should().ThrowAsync<ConflictException>()).WithMessage("*2 decision item(s)*awaiting committee votes*");

        // One round concludes (approve): the other is still open, so the meeting stays AwaitingVotes.
        await flow.VoteApproveUntilConcludedAsync(first);
        (await flow.LoadInstanceAsync(first)).CurrentActivityId.Should().Be("workflow-completed");
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.AwaitingVotes);
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Included);
        var stillOpen = async () => await flow.EndMeetingAsync();
        (await stillOpen.Should().ThrowAsync<ConflictException>()).WithMessage("*1 decision item(s)*");

        // The last round concludes: the whole end-of-meeting step runs.
        await flow.VoteApproveUntilConcludedAsync(second);

        var ended = await flow.LoadMeetingAsync();
        ended.Status.Should().Be(MeetingStatus.Ended);
        ended.EndedAt.Should().NotBeNull();
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Acknowledged,
            "MeetingEndedDomainEventHandler ran from the vote's SaveChanges");
        flow.AcknowledgedEvents().Should().ContainSingle();
        (await flow.LoadQueueItemsAsync()).Should().OnlyContain(q => q.Status == MeetingQueueItemStatus.Released);

        // Both rounds read as concluded through the shared predicate.
        var decisions = await flow.ConcludedDecisionsAsync();
        decisions.Values.Should().OnlyContain(d => d == "approve").And.HaveCount(2);
    }

    [Fact]
    public async Task ApproverRouteBack_WhileAwaitingVotes_DoesNotEnd()
    {
        using var flow = new Flow();
        await flow.SeedAsync();
        var only = await flow.QueueAppraisalAsync();
        await flow.CutOffAndInviteAsync();
        await flow.ReleaseAsync(only);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.AwaitingVotes);

        await flow.VoteAsync(only, "chair-cwm", "route_back");

        // A route-back is not a conclusion.
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.RoutedBack);
        (await flow.ConcludedDecisionsAsync()).Should().BeEmpty();
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Included);
    }

    [Fact]
    public async Task AllMembersVotedWithoutApproving_CountsAsConcluded_AndEndsTheMeeting()
    {
        using var flow = new Flow();
        await flow.SeedAsync();
        var approved = await flow.QueueAppraisalAsync();
        var rejected = await flow.QueueAppraisalAsync();
        await flow.CutOffAndInviteAsync();
        await flow.ReleaseAsync(approved);
        await flow.ReleaseAsync(rejected);

        await flow.VoteApproveUntilConcludedAsync(approved);

        // Three of four reject: the round is still open, so is the meeting.
        foreach (var voter in Voters.Take(3))
            await flow.VoteAsync(rejected, voter, "reject");
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.AwaitingVotes);
        (await flow.ConcludedDecisionsAsync()).Should().ContainKey(approved).And.HaveCount(1);

        // The last member votes reject: nobody can approve any more. The engine still leaves the
        // appraisal's own round wedged, but the meeting side sees it as concluded and ends.
        await flow.VoteAsync(rejected, Voters[3], "reject");

        (await flow.LoadInstanceAsync(rejected)).CurrentActivityId.Should().Be("pending-approval", "the workflow is not rerouted");
        var ended = await flow.LoadMeetingAsync();
        ended.Status.Should().Be(MeetingStatus.Ended);
        ended.EndedAt.Should().NotBeNull();
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Acknowledged);
        var outcomes = await flow.ConcludedDecisionsAsync();
        outcomes.Should().BeEquivalentTo(new Dictionary<string, string> { [approved] = "approve", [rejected] = "reject" });
    }

    [Fact]
    public async Task LatestRoundWins_AnOlderConcludedRoundDoesNotCountForAReopenedItem()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase($"latest-round-{Guid.NewGuid()}")
            .AddInterceptors(new MeetingRouteBackReturnFlowTests.StampRowVersions()).Options;
        await using var db = new WorkflowDbContext(options);
        var instanceId = Guid.NewGuid();

        var meeting = Meeting.Create("M", null, "1/2569", 1, 2569);
        meeting.AddItem(Guid.NewGuid(), "APR-1", 1m, 1m, instanceId, "pending-meeting", DateTime.Now);
        meeting.SendInvitation(DateTime.Now);
        meeting.ReleaseItem(meeting.Items.Single().AppraisalId, "secretary", DateTime.Now);
        db.Meetings.Add(meeting);

        var old = WorkflowActivityExecution.Create(instanceId, "pending-approval", "Approval", "ApprovalActivity");
        old.Complete("chair", new Dictionary<string, object> { ["decision"] = "approve" });
        await Task.Delay(20);
        var reopened = WorkflowActivityExecution.Create(instanceId, "pending-approval", "Approval", "ApprovalActivity");
        reopened.Start();
        db.WorkflowActivityExecutions.AddRange(old, reopened);
        await db.SaveChangesAsync();

        (await db.ConcludedByItemAsync(meeting.Id, default)).Should().BeEmpty("the newest round is still open");

        reopened.Complete("chair", new Dictionary<string, object> { ["decision"] = "reject" });
        await db.SaveChangesAsync();

        (await db.ConcludedByItemAsync(meeting.Id, default)).Should()
            .ContainSingle().Which.Value.Should().Be("reject");
    }

    [Fact]
    public async Task CancelledRound_CountsAsConcluded_SoEndNowWorks_ButNothingAutoEnds()
    {
        using var flow = new Flow();
        await flow.SeedAsync();
        var approved = await flow.QueueAppraisalAsync();
        var cancelled = await flow.QueueAppraisalAsync();
        await flow.CutOffAndInviteAsync();
        await flow.ReleaseAsync(approved);
        await flow.ReleaseAsync(cancelled);

        // The second item's workflow is cancelled while its round is open (cascade / recovery).
        await flow.CancelApprovalRoundAsync(cancelled);

        // Cancelling alone does not end the meeting, and an open round elsewhere still blocks End.
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.AwaitingVotes);
        var blocked = async () => await flow.EndMeetingAsync();
        (await blocked.Should().ThrowAsync<ConflictException>()).WithMessage("*1 decision item(s)*");

        // The remaining round concludes: the cancelled one counts, so the meeting ends on that vote.
        await flow.VoteApproveUntilConcludedAsync(approved);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.Ended);
        (await flow.ConcludedDecisionsAsync()).Should()
            .BeEquivalentTo(new Dictionary<string, string> { [approved] = "approve", [cancelled] = "cancelled" });
    }

    [Fact]
    public async Task CancelledRound_LetsTheSecretaryEndTheMeetingManually()
    {
        using var flow = new Flow();
        await flow.SeedAsync();
        var only = await flow.QueueAppraisalAsync();
        await flow.CutOffAndInviteAsync();
        await flow.ReleaseAsync(only);
        var blocked = async () => await flow.EndMeetingAsync();
        await blocked.Should().ThrowAsync<ConflictException>();

        await flow.CancelApprovalRoundAsync(only);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.AwaitingVotes, "cancelling does not auto-end");

        await flow.EndMeetingAsync();   // no 409 any more

        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.Ended);
        (await flow.LoadAckItemAsync()).Status.Should().Be(AcknowledgementStatus.Acknowledged);
    }

    [Theory]
    // (a) a completed round: any decision concludes except a recall, which is the old round of an item about to be re-released.
    [InlineData(ActivityExecutionStatus.Completed, "approve", 0, 0, null, "approve")]
    [InlineData(ActivityExecutionStatus.Completed, "reject", 0, 0, null, "reject")]
    [InlineData(ActivityExecutionStatus.Completed, "recall_to_meeting", 0, 0, 4, null)]   // old recall round, re-release pending
    // Completed route_back / empty on a still-Released item: the meeting-side mirror was skipped or failed.
    [InlineData(ActivityExecutionStatus.Completed, "route_back", 4, 1, 4, "route_back")]
    [InlineData(ActivityExecutionStatus.Completed, null, 4, 0, 4, "route_back")]
    [InlineData(ActivityExecutionStatus.Completed, "  ", 0, 0, null, "route_back")]
    // (b) an open round that everyone has voted on, with no route_back, counts as "reject".
    [InlineData(ActivityExecutionStatus.InProgress, null, 4, 0, 4, "reject")]
    [InlineData(ActivityExecutionStatus.Pending, null, 4, 0, 4, "reject")]
    [InlineData(ActivityExecutionStatus.InProgress, null, 3, 0, 4, null)]       // one member still owes a vote
    [InlineData(ActivityExecutionStatus.InProgress, null, 4, 1, 4, null)]       // a route_back vote
    [InlineData(ActivityExecutionStatus.InProgress, null, 0, 0, null, null)]    // member count unknown
    [InlineData(ActivityExecutionStatus.InProgress, null, 0, 0, 0, null)]       // empty roster is not "everyone voted"
    // (c) a terminal round that never completed can no longer be voted on: it must not block End.
    [InlineData(ActivityExecutionStatus.Cancelled, null, 0, 0, 4, "cancelled")]
    [InlineData(ActivityExecutionStatus.Failed, null, 1, 0, 4, "cancelled")]
    [InlineData(ActivityExecutionStatus.Skipped, null, 0, 0, null, "cancelled")]
    public void ConcludedDecision_AppliesAllRules(
        ActivityExecutionStatus status, string? decision, int votes, int routeBackVotes, int? totalMembers, string? expected) =>
        ApprovalRoundQuery.ConcludedDecision(status, decision, votes, routeBackVotes, totalMembers).Should().Be(expected);

    [Theory]
    [InlineData("approve", true)]
    [InlineData("reject", true)]
    [InlineData("route_back", false)]   // its own mirror reopens the meeting; the end hook must not fire
    [InlineData("recall_to_meeting", false)]
    [InlineData(null, false)]
    public void IsConcludingDecision_OnlyForApproveStyleDecisions(string? decision, bool expected) =>
        ApprovalRoundQuery.IsConcludingDecision(decision).Should().Be(expected);

    [Theory]
    [InlineData("Bogus")]
    [InlineData("99")]   // Enum.TryParse accepts undefined numbers
    public void Round_WithAnUnknownStatus_IsTreatedAsOpen(string status) =>
        new ApprovalRoundQuery.Round(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status, "approve", 4, 0, 4)
            .Outcome.Should().BeNull();

    // ------------------------------------------------------------------------------------------

    private sealed class Flow : IDisposable
    {
        private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();
        private readonly ServiceProvider _root;
        private readonly Dictionary<string, (Guid AppraisalId, Guid InstanceId)> _appraisals = new();
        private Guid _definitionId, _versionId, _meetingId, _ackAppraisalId;
        private int _sequence;

        public Flow()
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.ApplicationNow.Returns(_ => DateTime.Now);
            clock.Now.Returns(_ => DateTime.Now);

            var dbName = $"meeting-end-after-votes-{Guid.NewGuid()}";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<WorkflowDbContext>((sp, o) => o.UseInMemoryDatabase(dbName).AddInterceptors(
                new MeetingRouteBackReturnFlowTests.StampRowVersions(),
                sp.GetRequiredService<DispatchDomainEventInterceptor>()));

            // Substituted.
            services.AddSingleton(Substitute.For<IPublisher>());
            services.AddSingleton(clock);
            services.AddSingleton(Substitute.For<ISlaCalculator>());
            services.AddSingleton(_outbox);
            services.AddSingleton(Substitute.For<IOutboxScope>());
            services.AddSingleton(Substitute.For<ISqlConnectionFactory>());
            var sodGuard = Substitute.For<global::Workflow.Tasks.Authorization.ISegregationOfDutiesGuard>();
            sodGuard.GetConflictedUsersAsync(default!, default!, default!, default)
                .ReturnsForAnyArgs(new Dictionary<string, string>());
            services.AddSingleton(sodGuard);
            services.AddScoped<IWorkflowService>(sp => ForwardToEngine(sp.GetRequiredService<WorkflowEngine>()));
            services.AddScoped<IWorkflowActivityFactory>(sp =>
                new MeetingRouteBackReturnFlowTests.RealOrParkedFactory(new WorkflowActivityFactory(sp)));

            // The real interceptor, wired to a mediator that forwards to the real handlers.
            services.AddScoped<IMediator>(sp => ForwardDomainEvents(sp));
            services.AddScoped<DispatchDomainEventInterceptor>();

            // Real.
            services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
            services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
            services.AddScoped<IWorkflowActivityExecutionRepository, WorkflowActivityExecutionRepository>();
            services.AddScoped<IWorkflowDefinitionVersionRepository, WorkflowDefinitionVersionRepository>();
            services.AddScoped<IWorkflowPersistenceService, WorkflowPersistenceService>();
            services.AddScoped<IWorkflowStateManager, WorkflowStateManager>();
            services.AddScoped<IWorkflowLifecycleManager, WorkflowLifecycleManager>();
            services.AddScoped<IFlowControlManager, FlowControlManager>();
            services.AddScoped<WorkflowEngine>();
            services.AddScoped<ICommitteeRepository, CommitteeRepository>();
            services.AddScoped<IApprovalMemberResolver, ApprovalMemberResolver>();
            services.AddScoped<IApprovalVoteRepository, ApprovalVoteRepository>();
            services.AddScoped<IMeetingRepository, MeetingRepository>();
            services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();
            services.AddScoped<MeetingItemReleasedDomainEventHandler>();
            services.AddScoped<MeetingEndedDomainEventHandler>();

            _root = services.BuildServiceProvider();
        }

        public void Dispose() => _root.Dispose();

        public async Task SeedAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

            var withMeeting = Committee.Create("Committee With Meeting", "COMMITTEE_WITH_MEETING", null,
                QuorumType.Fixed, 2, MajorityType.Simple, VotingMode.Quorum);
            withMeeting.AddMember("chair-cwm", "Chair", CommitteeMemberPosition.Chairman);
            withMeeting.AddMember("uw-cwm", "UW", CommitteeMemberPosition.UW);
            withMeeting.AddMember("member-cwm", "Member", CommitteeMemberPosition.Member);
            var committee = Committee.Create("Committee", "COMMITTEE", null,
                QuorumType.Fixed, 1, MajorityType.Simple, VotingMode.WaitForAll);
            committee.AddMember("c-1", "C1", CommitteeMemberPosition.Chairman);
            var sub = Committee.Create("Sub Committee", "SUB_COMMITTEE", null,
                QuorumType.Fixed, 1, MajorityType.Simple, VotingMode.WaitForAll);
            sub.AddMember("sub-1", "S1", CommitteeMemberPosition.Chairman);
            db.Committees.AddRange(withMeeting, committee, sub);

            var schemaJson = LoadAppraisalSchemaJson();
            var definition = WorkflowDefinition.Create("Collateral Appraisal Workflow", "flow", schemaJson,
                "Appraisal", "tester");
            var version = WorkflowDefinitionVersion.Create(definition.Id, 1, definition.Name, "flow", schemaJson,
                "Appraisal", "tester");
            version.Publish("tester");
            db.AddRange(definition, version);
            await db.SaveChangesAsync();
            _definitionId = definition.Id;
            _versionId = version.Id;

            var persisted = await db.Committees.Include(c => c.Members)
                .SingleAsync(c => c.Code == "COMMITTEE_WITH_MEETING");
            var meeting = Meeting.Create("Credit meeting", null, "1/2569", 1, 2569);
            meeting.SnapshotCommittee(persisted, meetingSeq: 1);
            meeting.AddMember(
                MeetingMember.CreateManual(meeting.Id, "A", "Member A", CommitteeMemberPosition.Member),
                DateTime.Now);
            db.Meetings.Add(meeting);
            _meetingId = meeting.Id;

            // An already-approved appraisal waiting to be acknowledged at this meeting.
            _ackAppraisalId = Guid.NewGuid();
            db.AppraisalAcknowledgementQueueItems.Add(AppraisalAcknowledgementQueueItem.Create(
                _ackAppraisalId, "APR-ACK", null, sub.Id, "SUB_COMMITTEE", "Group1", DateTime.Now));
            await db.SaveChangesAsync();
        }

        /// <summary>Starts a 35M appraisal workflow and runs the real tier switch: it parks on pending-meeting, queued.</summary>
        public async Task<string> QueueAppraisalAsync()
        {
            var key = $"APR-{++_sequence}";
            var appraisalId = Guid.NewGuid();
            Guid instanceId;
            using (var scope = _root.CreateScope())
            {
                var persistence = scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>();
                var instance = WorkflowInstance.Create(_definitionId, _versionId, key, appraisalId.ToString(),
                    "tester", new Dictionary<string, object>
                    {
                        ["appraisalId"] = appraisalId.ToString(),
                        ["appraisalNumber"] = key,
                        ["appraisalValue"] = 35_000_000m,
                        ["facilityLimit"] = 50_000_000m,
                        ["assignmentType"] = "Internal",
                        ["isOfflineExternal"] = false
                    });
                await persistence.SaveWorkflowInstanceAsync(instance);
                instanceId = instance.Id;
            }

            using (var scope = _root.CreateScope())
            {
                var persistence = scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>();
                var instance = (await persistence.GetWorkflowInstanceAsync(instanceId))!;
                var schema = (await persistence.GetSchemaByVersionIdAsync(_versionId))!;
                var result = await scope.ServiceProvider.GetRequiredService<WorkflowEngine>().ExecuteWorkflowAsync(
                    schema, instance, schema.Activities.Single(a => a.Id == "approval-tier-switch"));
                result.Status.Should().NotBe(WorkflowExecutionStatus.Failed, result.ErrorMessage);
            }

            (await LoadInstanceAsync(instanceId)).CurrentActivityId.Should().Be("pending-meeting");
            _appraisals[key] = (appraisalId, instanceId);
            return key;
        }

        public async Task CutOffAndInviteAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var meeting = await LoadAggregateAsync(db);
            var queued = await db.MeetingQueueItems.ToListAsync();
            var acks = await db.AppraisalAcknowledgementQueueItems.ToListAsync();

            meeting.CutOff(queued, acks, DateTime.Now);
            meeting.SendInvitation(DateTime.Now);
            await db.SaveChangesAsync();
        }

        /// <summary>Secretary release, exactly as the command handler does: the aggregate, then SaveChanges dispatches the events.</summary>
        public async Task ReleaseAsync(string key)
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var meeting = await LoadAggregateAsync(db);

            meeting.ReleaseItem(_appraisals[key].AppraisalId, "secretary", DateTime.Now);
            await db.SaveChangesAsync();
        }

        public Task VoteAsync(string key, string voter, string vote) => ResumeAsync(
            _appraisals[key].InstanceId, voter, new Dictionary<string, object> { ["decisionTaken"] = vote });

        public async Task VoteApproveUntilConcludedAsync(string key)
        {
            foreach (var voter in Voters)
            {
                if ((await LoadInstanceAsync(_appraisals[key].InstanceId)).CurrentActivityId != "pending-approval")
                    return;
                await VoteAsync(key, voter, "approve");
            }
        }

        public async Task EndMeetingAsync()
        {
            using var scope = _root.CreateScope();
            var handler = new EndMeetingCommandHandler(
                scope.ServiceProvider.GetRequiredService<IMeetingRepository>(),
                scope.ServiceProvider.GetRequiredService<IDateTimeProvider>());
            await handler.Handle(new EndMeetingCommand(_meetingId), default);
            // The transactional pipeline behavior saves after the handler.
            await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().SaveChangesAsync();
        }

        /// <summary>Concluded decision per appraisal key, as the shared predicate sees the meeting now.</summary>
        public async Task<Dictionary<string, string>> ConcludedDecisionsAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var byItem = await db.ConcludedByItemAsync(_meetingId, default);
            var items = await db.MeetingItems.AsNoTracking().Where(i => i.MeetingId == _meetingId).ToListAsync();
            return items.Where(i => byItem.ContainsKey(i.Id))
                .ToDictionary(i => _appraisals.Single(a => a.Value.AppraisalId == i.AppraisalId).Key, i => byItem[i.Id]);
        }

        /// <summary>Marks the item's open approval execution Cancelled, as a cancelled workflow does.</summary>
        public async Task CancelApprovalRoundAsync(string key)
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var instanceId = _appraisals[key].InstanceId;
            var execution = await db.WorkflowActivityExecutions.SingleAsync(e =>
                e.WorkflowInstanceId == instanceId && e.ActivityId == "pending-approval");
            execution.Cancel("workflow cancelled");
            await db.SaveChangesAsync();
        }

        public async Task<WorkflowInstance> LoadInstanceAsync(string key) => await LoadInstanceAsync(_appraisals[key].InstanceId);

        private async Task<WorkflowInstance> LoadInstanceAsync(Guid instanceId)
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>()
                .GetWorkflowInstanceAsync(instanceId) ?? throw new InvalidOperationException("instance missing");
        }

        public async Task<Meeting> LoadMeetingAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Meetings
                .AsNoTracking().SingleAsync(m => m.Id == _meetingId);
        }

        public async Task<AppraisalAcknowledgementQueueItem> LoadAckItemAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().AppraisalAcknowledgementQueueItems
                .AsNoTracking().SingleAsync(a => a.AppraisalId == _ackAppraisalId);
        }

        public async Task<List<MeetingQueueItem>> LoadQueueItemsAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().MeetingQueueItems
                .AsNoTracking().ToListAsync();
        }

        public IEnumerable<AppraisalAcknowledgedIntegrationEvent> AcknowledgedEvents() =>
            _outbox.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<AppraisalAcknowledgedIntegrationEvent>();

        private async Task ResumeAsync(Guid instanceId, string voter, Dictionary<string, object> input)
        {
            using var scope = _root.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<WorkflowEngine>()
                .ResumeWorkflowAsync(instanceId, "pending-approval", voter, input);
            result.Status.Should().NotBe(WorkflowExecutionStatus.Failed, result.ErrorMessage);
        }

        private Task<Meeting> LoadAggregateAsync(WorkflowDbContext db) =>
            db.Meetings.Include(m => m.Items).Include(m => m.Members).SingleAsync(m => m.Id == _meetingId);

        /// <summary>Stands in for DispatchDomainEventInterceptor's MediatR: forwards to the real handlers.</summary>
        private static IMediator ForwardDomainEvents(IServiceProvider sp)
        {
            var mediator = Substitute.For<IMediator>();
            mediator.Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                switch (call.Arg<IDomainEvent>())
                {
                    case MeetingItemReleasedDomainEvent released:
                        await sp.GetRequiredService<MeetingItemReleasedDomainEventHandler>()
                            .Handle(released, CancellationToken.None);
                        break;
                    case MeetingEndedDomainEvent ended:
                        await sp.GetRequiredService<MeetingEndedDomainEventHandler>()
                            .Handle(ended, CancellationToken.None);
                        break;
                }
            });
            return mediator;
        }

        private static IWorkflowService ForwardToEngine(WorkflowEngine engine)
        {
            var service = Substitute.For<IWorkflowService>();
            service.ResumeWorkflowAsync(
                    Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                    Arg.Any<Dictionary<string, object>?>(), Arg.Any<Dictionary<string, RuntimeOverride>?>(),
                    Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    var result = await engine.ResumeWorkflowAsync(
                        call.ArgAt<Guid>(0), call.ArgAt<string>(1), call.ArgAt<string>(2),
                        call.ArgAt<Dictionary<string, object>?>(3),
                        call.ArgAt<Dictionary<string, RuntimeOverride>?>(4), call.ArgAt<CancellationToken>(5));
                    if (result.Status == WorkflowExecutionStatus.Failed)
                        throw new InvalidOperationException(result.ErrorMessage ?? "Workflow resume failed");
                    return result.WorkflowInstance!;
                });
            return service;
        }

        private static string LoadAppraisalSchemaJson()
        {
            var assembly = typeof(WorkflowEngine).Assembly;
            var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("appraisal-workflow.json"));
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            using var document = JsonDocument.Parse(reader.ReadToEnd());
            return document.RootElement.GetProperty("workflowSchema").GetRawText();
        }
    }
}
