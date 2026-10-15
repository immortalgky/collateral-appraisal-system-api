using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Time;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.Domain.Events;
using Workflow.Meetings.EventHandlers;
using Workflow.Sla.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Activities.Core;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Core;
using Workflow.Workflow.Events;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Schema;
using Workflow.Workflow.Services;

namespace Workflow.Tests.Meetings;

/// <summary>
/// End-to-end flow for the "routed back from a meeting always returns to that meeting" rule.
/// Drives the REAL WorkflowEngine over the REAL appraisal-workflow.json (embedded resource), with the
/// real FlowControlManager / SwitchActivity / MeetingActivity / ApprovalActivity, the real Meeting
/// aggregate and handlers, the real committee repository + member resolver, all on one EF InMemory
/// WorkflowDbContext. Every step runs in a fresh DI scope (fresh DbContext) so state must survive a
/// persistence round-trip, like separate HTTP requests do.
///
/// Substituted (cannot run in-process): the SLA calculator, the integration-event outbox, MediatR's
/// IPublisher (its in-process handlers fan out to PendingTask / notification / user-directory code),
/// ISqlConnectionFactory (Dapper-only, unused on these paths), IWorkflowService (its real body takes a
/// SQL Server transaction + sp_getapplock — replaced by a thin forwarder to the real engine), and the
/// human TaskActivity steps of the rework leg (a stub that just parks the workflow).
/// </summary>
public class MeetingRouteBackReturnFlowTests
{
    private const decimal AboveThreshold = 35_000_000m;
    private const decimal ReworkedBelowThreshold = 25_000_000m;
    private const string Secretary = "secretary";

    // Distinct per committee so the one the round runs under is observable.
    private static readonly string[] MeetingCommitteeVoters = ["chair-cwm", "uw-cwm", "member-cwm"];
    private static readonly string[] MeetingRoster = [.. MeetingCommitteeVoters, "A"];

    [Fact]
    public async Task SecretaryRouteBack_ReworkedBelowThreshold_ReturnsToTheSameMeeting_AndIsApprovedUnderItsCommittee()
    {
        using var flow = new Flow();
        await flow.SeedAsync(AboveThreshold);

        // 35M: switch -> pending-meeting, queued; secretary cuts off, invites, then routes it back.
        (await flow.RunFromSwitchAsync()).CurrentActivityId.Should().Be("pending-meeting");
        await flow.CutOffAndInviteAsync();
        await flow.SecretaryRoutesBackAsync();
        (await flow.LoadInstanceAsync()).CurrentActivityId.Should().Be("int-appraisal-execution");
        (await flow.LoadItemAsync()).ItemDecision.Should().Be(ItemDecision.RoutedBack);

        // Rework drops the value to 25M and re-enters at the tier switch.
        var afterRework = await flow.RunFromSwitchAsync(ReworkedBelowThreshold);

        afterRework.CurrentActivityId.Should().Be("pending-meeting", "a routed-back item must return to its meeting");
        afterRework.ActivityExecutions.Should().NotContain(e => e.ActivityId == "pending-approval");
        (await flow.LoadItemAsync()).ItemDecision.Should().Be(ItemDecision.Pending);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.InvitationSent);

        // Secretary releases it: approval runs under the meeting's committee and roster.
        await flow.ReleaseAsync();

        flow.AssertApprovalRoundRunsUnderTheMeeting(await flow.LoadInstanceAsync());
    }

    [Fact]
    public async Task CommitteeMemberRouteBack_ReworkedBelowThreshold_ReturnsToTheSameMeeting_AndIsApprovedUnderItsCommittee()
    {
        using var flow = new Flow();
        await flow.SeedAsync(AboveThreshold);

        (await flow.RunFromSwitchAsync()).CurrentActivityId.Should().Be("pending-meeting");
        await flow.CutOffAndInviteAsync();

        // First release -> approval round under the meeting; a member votes route_back.
        await flow.ReleaseAsync();
        flow.AssertApprovalRoundRunsUnderTheMeeting(await flow.LoadInstanceAsync());
        await flow.MemberVotesRouteBackAsync("chair-cwm");

        (await flow.LoadInstanceAsync()).CurrentActivityId.Should().Be("int-appraisal-execution");
        (await flow.LoadItemAsync()).ItemDecision.Should().Be(ItemDecision.RoutedBack);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.RoutedBack);

        // Rework to 25M, back through the tier switch.
        var afterRework = await flow.RunFromSwitchAsync(ReworkedBelowThreshold);

        afterRework.CurrentActivityId.Should().Be("pending-meeting", "a routed-back item must return to its meeting");
        (await flow.LoadItemAsync()).ItemDecision.Should().Be(ItemDecision.Pending);
        (await flow.LoadMeetingAsync()).Status.Should().Be(MeetingStatus.InvitationSent);

        // Second release: a NEW round, again under the meeting's committee and roster.
        flow.Publisher.ClearReceivedCalls();
        await flow.ReleaseAsync();

        flow.AssertApprovalRoundRunsUnderTheMeeting(await flow.LoadInstanceAsync());
    }

    [Fact]
    public async Task NoMeetingItem_At25M_GoesStraightToApproval_UnderTheValueTierCommittee()
    {
        using var flow = new Flow();
        await flow.SeedAsync(ReworkedBelowThreshold);

        var instance = await flow.RunFromSwitchAsync();

        // The redirect must not fire: nothing was ever routed back from a meeting.
        instance.CurrentActivityId.Should().Be("pending-approval");
        instance.Variables["pending_approval_committeeCode"].ToString().Should().Be("COMMITTEE");
        var assigned = flow.LastApprovalAssignment();
        assigned.CommitteeCode.Should().Be("COMMITTEE");
        assigned.Members.Select(m => m.Username).Should().BeEquivalentTo("c-1", "c-2");
    }

    // ------------------------------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------------------------------

    private sealed class Flow : IDisposable
    {
        public IPublisher Publisher { get; } = Substitute.For<IPublisher>();
        private readonly ServiceProvider _root;
        private readonly Guid _appraisalId = Guid.NewGuid();
        private Guid _definitionId, _versionId, _instanceId, _meetingId;

        public Flow()
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.ApplicationNow.Returns(_ => DateTime.Now);
            clock.Now.Returns(_ => DateTime.Now);

            var dbName = $"meeting-route-back-flow-{Guid.NewGuid()}";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<WorkflowDbContext>(
                o => o.UseInMemoryDatabase(dbName).AddInterceptors(new StampRowVersions()));

            // Substituted.
            services.AddSingleton(Publisher);
            services.AddSingleton(clock);
            services.AddSingleton(Substitute.For<ISlaCalculator>());
            services.AddSingleton(Substitute.For<IIntegrationEventOutbox>());
            services.AddSingleton(Substitute.For<ISqlConnectionFactory>());
            services.AddScoped<IWorkflowService>(sp => ForwardToEngine(sp.GetRequiredService<WorkflowEngine>()));
            services.AddScoped<IWorkflowActivityFactory>(sp =>
                new RealOrParkedFactory(new WorkflowActivityFactory(sp)));

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
            services.AddScoped<MeetingItemReleasedDomainEventHandler>();
            services.AddScoped<MeetingItemRoutedBackDomainEventHandler>();

            _root = services.BuildServiceProvider();
        }

        public void Dispose() => _root.Dispose();

        // -- Seed --------------------------------------------------------------------------------

        public async Task SeedAsync(decimal appraisalValue)
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

            // Three committees with different members / quorum so each is recognisable.
            var withMeeting = Committee.Create("Committee With Meeting", "COMMITTEE_WITH_MEETING", null,
                QuorumType.Fixed, 2, MajorityType.Simple, VotingMode.Quorum);
            withMeeting.AddMember("chair-cwm", "Chair", CommitteeMemberPosition.Chairman);
            withMeeting.AddMember("uw-cwm", "UW", CommitteeMemberPosition.UW);
            withMeeting.AddMember("member-cwm", "Member", CommitteeMemberPosition.Member);
            var committee = Committee.Create("Committee", "COMMITTEE", null,
                QuorumType.Fixed, 1, MajorityType.Simple, VotingMode.WaitForAll);
            committee.AddMember("c-1", "C1", CommitteeMemberPosition.Chairman);
            committee.AddMember("c-2", "C2", CommitteeMemberPosition.Member);
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

            // Meeting snapshotted from COMMITTEE_WITH_MEETING (reloaded so member ids exist), plus a
            // member "A" added to this meeting only.
            var persisted = await db.Committees.Include(c => c.Members)
                .SingleAsync(c => c.Code == "COMMITTEE_WITH_MEETING");
            var meeting = Meeting.Create("Credit meeting", null, "1/2569", 1, 2569);
            meeting.SnapshotCommittee(persisted, meetingSeq: 1);
            meeting.AddMember(
                MeetingMember.CreateManual(meeting.Id, "A", "Member A", CommitteeMemberPosition.Member),
                DateTime.Now);
            db.Meetings.Add(meeting);
            _meetingId = meeting.Id;

            var persistence = scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>();
            var instance = WorkflowInstance.Create(_definitionId, _versionId, "APR-1", _appraisalId.ToString(),
                "tester", new Dictionary<string, object>
                {
                    ["appraisalId"] = _appraisalId.ToString(),
                    ["appraisalNumber"] = "APR-1",
                    ["appraisalValue"] = appraisalValue,
                    ["facilityLimit"] = 50_000_000m,
                    ["assignmentType"] = "Internal",
                    ["isOfflineExternal"] = false
                });
            await persistence.SaveWorkflowInstanceAsync(instance);
            _instanceId = instance.Id;
        }

        // -- Steps (each = one request = one scope) -----------------------------------------------

        /// <summary>Executes the real tier switch (optionally after a rework changed the value).</summary>
        public async Task<WorkflowInstance> RunFromSwitchAsync(decimal? reworkedValue = null)
        {
            using var scope = _root.CreateScope();
            var persistence = scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>();
            var instance = await persistence.GetWorkflowInstanceAsync(_instanceId)
                           ?? throw new InvalidOperationException("instance missing");
            var schema = (await persistence.GetSchemaByVersionIdAsync(_versionId))!;

            if (reworkedValue is { } value)
            {
                // What finishing the rework does: new value, workflow running again.
                instance.UpdateVariables(new Dictionary<string, object> { ["appraisalValue"] = value });
                await scope.ServiceProvider.GetRequiredService<IWorkflowLifecycleManager>()
                    .ResumeWorkflowAsync(instance, "Rework completed");
            }

            var result = await scope.ServiceProvider.GetRequiredService<WorkflowEngine>().ExecuteWorkflowAsync(
                schema, instance, schema.Activities.Single(a => a.Id == "approval-tier-switch"));
            result.Status.Should().NotBe(WorkflowExecutionStatus.Failed, result.ErrorMessage);
            return await LoadInstanceAsync();
        }

        public async Task CutOffAndInviteAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var meeting = await db.Meetings.Include(m => m.Items).Include(m => m.Members)
                .SingleAsync(m => m.Id == _meetingId);
            var queued = await db.MeetingQueueItems.Where(q => q.AppraisalId == _appraisalId).ToListAsync();
            queued.Should().ContainSingle("MeetingActivity enqueues the appraisal on first entry");

            meeting.CutOff(queued, [], DateTime.Now);
            meeting.SendInvitation(DateTime.Now);
            await db.SaveChangesAsync();
        }

        public async Task SecretaryRoutesBackAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var meeting = await LoadMeetingAggregateAsync(db);

            meeting.RouteBackItem(_appraisalId, Secretary, "valuation needs rework", DateTime.Now);
            var evt = meeting.DomainEvents.OfType<MeetingItemRoutedBackDomainEvent>().Single();
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<MeetingItemRoutedBackDomainEventHandler>()
                .Handle(evt, CancellationToken.None);
        }

        public async Task ReleaseAsync()
        {
            using var scope = _root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var meeting = await LoadMeetingAggregateAsync(db);

            meeting.ReleaseItem(_appraisalId, Secretary, DateTime.Now);
            var evt = meeting.DomainEvents.OfType<MeetingItemReleasedDomainEvent>().Single();
            await db.SaveChangesAsync();
            // The REAL handler builds the resume input; ForwardToEngine feeds it to the real engine.
            await scope.ServiceProvider.GetRequiredService<MeetingItemReleasedDomainEventHandler>()
                .Handle(evt, CancellationToken.None);
        }

        public async Task MemberVotesRouteBackAsync(string voter)
        {
            using var scope = _root.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<WorkflowEngine>().ResumeWorkflowAsync(
                _instanceId, "pending-approval", voter,
                new Dictionary<string, object> { ["decisionTaken"] = "route_back" });
            result.Status.Should().NotBe(WorkflowExecutionStatus.Failed, result.ErrorMessage);
        }

        // -- Reads ----------------------------------------------------------------------------------

        public async Task<WorkflowInstance> LoadInstanceAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceService>()
                .GetWorkflowInstanceAsync(_instanceId) ?? throw new InvalidOperationException("instance missing");
        }

        public async Task<MeetingItem> LoadItemAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().MeetingItems
                .AsNoTracking().SingleAsync(i => i.AppraisalId == _appraisalId);
        }

        public async Task<Meeting> LoadMeetingAsync()
        {
            using var scope = _root.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Meetings
                .AsNoTracking().SingleAsync(m => m.Id == _meetingId);
        }

        public ApprovalTasksAssignedEvent LastApprovalAssignment() =>
            Publisher.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<ApprovalTasksAssignedEvent>().Last();

        /// <summary>
        /// The latest approval round: voters are the meeting roster (incl. the meeting-only member "A"),
        /// and its committee, quorum and activity variables are COMMITTEE_WITH_MEETING's — not the
        /// value-tier COMMITTEE's, even though the appraisal is now worth 25M.
        /// </summary>
        public void AssertApprovalRoundRunsUnderTheMeeting(WorkflowInstance instance)
        {
            instance.CurrentActivityId.Should().Be("pending-approval");

            var assigned = LastApprovalAssignment();
            assigned.CommitteeCode.Should().Be("COMMITTEE_WITH_MEETING").And.NotBe("COMMITTEE");
            assigned.Members.Select(m => m.Username).Should().BeEquivalentTo(MeetingRoster);

            instance.Variables["pending_approval_committeeCode"].ToString().Should().Be("COMMITTEE_WITH_MEETING");
            instance.Variables["pending_approval_totalMembers"].ToString().Should().Be("4");
            // COMMITTEE_WITH_MEETING's quorum (Fixed 2); COMMITTEE would be Fixed 1.
            JsonSerializer.Deserialize<QuorumConfig>(
                    JsonSerializer.Serialize(instance.Variables["pending_approval_quorum"]),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
                .Should().BeEquivalentTo(new QuorumConfig("Fixed", 2));
            // The roster and committee are consumed once so a later non-meeting round cannot inherit them.
            instance.Variables["meetingCommitteeId"].ToString().Should().BeEmpty();
        }

        // -- Wiring ---------------------------------------------------------------------------------

        private Task<Meeting> LoadMeetingAggregateAsync(WorkflowDbContext db) =>
            db.Meetings.Include(m => m.Items).Include(m => m.Members).SingleAsync(m => m.Id == _meetingId);

        /// <summary>
        /// Stands in for WorkflowService.ResumeWorkflowAsync (SQL transaction + sp_getapplock): same
        /// contract, but calls the real engine directly and fails loudly like the real one does.
        /// </summary>
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

    /// <summary>
    /// SQL Server generates rowversion values; EF InMemory does not, and rejects the null on save.
    /// Stamps a fresh value on every added/modified row so InMemory behaves like the real store.
    /// </summary>
    private sealed class StampRowVersions : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                if (entry.Metadata.FindProperty("RowVersion") is { ClrType: var type } && type == typeof(byte[]))
                    entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Real Switch / Meeting / Approval activities; the human rework steps just park the workflow.</summary>
    private sealed class RealOrParkedFactory(IWorkflowActivityFactory real) : IWorkflowActivityFactory
    {
        private static readonly HashSet<string> RealTypes =
            [ActivityTypes.SwitchActivity, ActivityTypes.MeetingActivity, ActivityTypes.ApprovalActivity];

        public IWorkflowActivity CreateActivity(string activityType) =>
            RealTypes.Contains(activityType) ? real.CreateActivity(activityType) : new ParkedActivity(activityType);

        public IEnumerable<string> GetAvailableActivityTypes() => real.GetAvailableActivityTypes();

        public ActivityTypeDefinition GetActivityTypeDefinition(string activityType) =>
            real.GetActivityTypeDefinition(activityType);
    }

    private sealed class ParkedActivity(string type) : WorkflowActivityBase
    {
        public override string ActivityType => type;
        public override string Name => "Parked rework step";

        protected override Task<ActivityResult> ExecuteActivityAsync(
            ActivityContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(ActivityResult.Pending(new Dictionary<string, object>()));
    }
}
