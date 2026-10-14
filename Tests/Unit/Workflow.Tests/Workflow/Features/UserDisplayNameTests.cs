using Auth.Contracts.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Identity;
using Workflow.Data;
using Workflow.Data.Repository;
using Workflow.Domain;
using Workflow.Workflow.Activities.Approval;
using Workflow.Workflow.Features.CompleteActivity;
using Workflow.Workflow.Features.GetApprovalHistory;
using Workflow.Workflow.Features.GetApprovalList;
using Workflow.Workflow.Models;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Services;
using Xunit;

namespace Workflow.Tests.Workflow.Features;

/// <summary>
/// Display names are resolved through <see cref="IUserLookupService"/> for the approval list/history
/// members and the complete-activity next assignee. Codes stay the identity; names are display only.
/// </summary>
public class UserDisplayNameTests
{
    private static IReadOnlyDictionary<string, UserLookupDto> Users(params UserLookupDto[] users) =>
        users.ToDictionary(u => u.Username, StringComparer.OrdinalIgnoreCase);

    private static IUserLookupService Lookup(params UserLookupDto[] users)
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Users(users));
        return lookup;
    }

    // ── Resolve rule ──────────────────────────────────────────────────────

    [Fact]
    public void Resolve_KnownUser_ReturnsTrimmedFullName()
    {
        UserDisplayName.Resolve(Users(new UserLookupDto("P0236", "Somchai", "Jaidee")), "P0236")
            .Should().Be("Somchai Jaidee");
    }

    [Theory]
    [InlineData(" John ", " Doe ", "John Doe")]
    [InlineData("John", "", "John")]
    [InlineData("", " Doe", "Doe")]
    [InlineData("  ", " ", null)]
    public void Resolve_TrimsEachPart_AndSkipsBlankParts(string first, string last, string? expected)
    {
        UserDisplayName.Resolve(Users(new UserLookupDto("P0236", first, last)), "P0236").Should().Be(expected);
    }

    [Fact]
    public void Resolve_BlankName_ReturnsNull()
    {
        UserDisplayName.Resolve(Users(new UserLookupDto("P0236", " ", "")), "P0236").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_NullOrBlankUsername_ReturnsNull_WithoutThrowing(string? username)
    {
        UserDisplayName.Resolve(Users(new UserLookupDto("P0236", "Somchai", "Jaidee")), username)
            .Should().BeNull();
    }

    [Fact]
    public void Resolve_UnknownUser_ReturnsNull()
    {
        UserDisplayName.Resolve(Users(), "GROUP_X").Should().BeNull();
    }

    // ── Approval list + history (live path) ───────────────────────────────

    private static (WorkflowInstance Instance, WorkflowDbContext Db) InstanceWithMembers()
    {
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(), "wf", correlationId: Guid.NewGuid().ToString(), startedBy: "tester",
            initialVariables: new Dictionary<string, object>
            {
                ["pending_approval_members"] = new List<ApprovalMemberInfo>
                {
                    new("john.doe", "Chair"),
                    new("P9999", "Member"),
                    new(null!, "Member"), // old-shaped / overridden roster entry with no username
                },
            });
        var db = new WorkflowDbContext(new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (instance, db);
    }

    [Fact]
    public async Task ApprovalList_Members_GetDisplayName_WithCodeFallbackForMissingUser()
    {
        var (instance, db) = InstanceWithMembers();
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);

        var handler = new GetApprovalListQueryHandler(
            instances, Substitute.For<IApprovalVoteRepository>(), db,
            Substitute.For<ICurrentUserService>(),
            Lookup(new UserLookupDto("john.doe", "John", "Doe")),
            NullLogger<GetApprovalListQueryHandler>.Instance);

        var result = await handler.Handle(new GetApprovalListQuery(instance.Id, "pending-approval"), default);

        result.Members.Select(m => (m.Username, m.DisplayName)).Should().Equal(
            ("john.doe", "John Doe"),
            ("P9999", "P9999"),
            (null, null));
    }

    [Fact]
    public async Task ApprovalHistory_Members_GetDisplayName_WithCodeFallbackForMissingUser()
    {
        var (instance, db) = InstanceWithMembers();
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var votes = Substitute.For<IApprovalVoteRepository>();
        var appraisalId = Guid.NewGuid();
        votes.GetLatestRoundVotesByAppraisalAsync(appraisalId, "pending-approval", Arg.Any<CancellationToken>())
            .Returns([ApprovalVote.Create(
                appraisalId, instance.Id, "pending-approval", Guid.NewGuid(), "john.doe", "Chair", "approve", null)]);

        var handler = new GetApprovalHistoryQueryHandler(
            votes, instances, db, null!, Substitute.For<ICurrentUserService>(),
            Lookup(new UserLookupDto("john.doe", "John", "Doe")),
            NullLogger<GetApprovalHistoryQueryHandler>.Instance);

        var result = await handler.Handle(new GetApprovalHistoryQuery(appraisalId), default);

        result!.Members.Select(m => (m.Username, m.DisplayName)).Should().Equal(
            ("john.doe", "John Doe"),
            ("P9999", "P9999"),
            (null, null));
    }

    // ── Complete activity: nextAssigneeDisplayName (resolved by the endpoint, outside the transaction) ──

    private static Task<CompleteActivityResponse> Enrich(
        string? nextAssignee, IUserLookupService lookup, string status = "Running", TimeSpan? timeout = null,
        CancellationToken ct = default) =>
        CompleteActivityEndpoint.WithNextAssigneeDisplayNameAsync(
            new CompleteActivityResponse { Status = status, NextAssignee = nextAssignee },
            lookup, NullLogger.Instance, ct, timeout);

    [Fact]
    public async Task Complete_NextAssigneeIsKnownUser_ReturnsDisplayName()
    {
        var response = await Enrich("P0236", Lookup(new UserLookupDto("P0236", "Somchai", "Jaidee")));

        response.NextAssignee.Should().Be("P0236");
        response.NextAssigneeDisplayName.Should().Be("Somchai Jaidee");
    }

    [Fact]
    public async Task Complete_NextAssigneeNotAUser_NameIsNull_CodeStillReturned()
    {
        var response = await Enrich("POOL_CHECKER", Lookup());

        response.NextAssignee.Should().Be("POOL_CHECKER");
        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task Complete_NoNextAssignee_NameIsNull_AndNoLookup(string? nextAssignee)
    {
        var lookup = Lookup();
        var response = await Enrich(nextAssignee, lookup);

        response.NextAssigneeDisplayName.Should().BeNull();
        await lookup.DidNotReceiveWithAnyArgs().GetByUsernamesAsync(default!, default);
    }

    [Fact]
    public async Task Complete_LookupThrows_ResponseIsReturned_NameIsNull()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new InvalidOperationException("auth down"));

        var response = await Enrich("P0236", lookup);

        response.Status.Should().Be("Running");
        response.NextAssignee.Should().Be("P0236");
        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    [InlineData("ValidationFailed")]
    public async Task Complete_NonLiveStatus_NameIsNull_AndNoLookup(string status)
    {
        var lookup = Lookup(new UserLookupDto("P0236", "Somchai", "Jaidee"));
        var response = await Enrich("P0236", lookup, status);

        response.NextAssigneeDisplayName.Should().BeNull();
        await lookup.DidNotReceiveWithAnyArgs().GetByUsernamesAsync(default!, default);
    }

    [Fact]
    public async Task Complete_SuspendedWorkflow_ResolvesName()
    {
        var response = await Enrich("P0236", Lookup(new UserLookupDto("P0236", "Somchai", "Jaidee")), "Suspended");

        response.NextAssigneeDisplayName.Should().Be("Somchai Jaidee");
    }

    [Fact]
    public async Task Complete_LookupHangs_TimesOut_ResponseIsStillReturned()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return (IReadOnlyDictionary<string, UserLookupDto>)Users();
            });

        var response = await Enrich("P0236", lookup, timeout: TimeSpan.FromMilliseconds(50));

        response.NextAssignee.Should().Be("P0236");
        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ApprovalList_CallerCancelled_IsNotSwallowed()
    {
        var (instance, db) = InstanceWithMembers();
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = new GetApprovalListQueryHandler(
            instances, Substitute.For<IApprovalVoteRepository>(), db,
            Substitute.For<ICurrentUserService>(), lookup, NullLogger<GetApprovalListQueryHandler>.Instance);

        var act = () => handler.Handle(new GetApprovalListQuery(instance.Id, "pending-approval"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static IUserLookupService ThrowingLookup()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new InvalidOperationException("auth down"));
        return lookup;
    }

    [Fact]
    public async Task Complete_LookupCancelled_ResponseIsStillReturned()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw new OperationCanceledException());

        var response = await Enrich("P0236", lookup);

        response.NextAssignee.Should().Be("P0236");
        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ApprovalList_LookupThrows_RosterStillReturned_WithUsernameFallback()
    {
        var (instance, db) = InstanceWithMembers();
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);

        var handler = new GetApprovalListQueryHandler(
            instances, Substitute.For<IApprovalVoteRepository>(), db,
            Substitute.For<ICurrentUserService>(), ThrowingLookup(),
            NullLogger<GetApprovalListQueryHandler>.Instance);

        var result = await handler.Handle(new GetApprovalListQuery(instance.Id, "pending-approval"), default);

        result.Members.Select(m => m.DisplayName).Should().Equal("john.doe", "P9999", null);
    }

    [Fact]
    public async Task ApprovalHistory_LookupThrows_RosterStillReturned_WithUsernameFallback()
    {
        var (instance, db) = InstanceWithMembers();
        var instances = Substitute.For<IWorkflowInstanceRepository>();
        instances.GetByIdAsync(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        var votes = Substitute.For<IApprovalVoteRepository>();
        var appraisalId = Guid.NewGuid();
        votes.GetLatestRoundVotesByAppraisalAsync(appraisalId, "pending-approval", Arg.Any<CancellationToken>())
            .Returns([ApprovalVote.Create(
                appraisalId, instance.Id, "pending-approval", Guid.NewGuid(), "john.doe", "Chair", "approve", null)]);

        var handler = new GetApprovalHistoryQueryHandler(
            votes, instances, db, null!, Substitute.For<ICurrentUserService>(), ThrowingLookup(),
            NullLogger<GetApprovalHistoryQueryHandler>.Instance);

        var result = await handler.Handle(new GetApprovalHistoryQuery(appraisalId), default);

        result!.Members.Select(m => m.DisplayName).Should().Equal("john.doe", "P9999", null);
    }

    // A SqlException-like failure type that is NOT an OperationCanceledException.
    private sealed class FakeSqlException() : Exception("Operation cancelled by user");

    private static IUserLookupService LookupThrowing(Exception ex)
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyDictionary<string, UserLookupDto>>(_ => throw ex);
        return lookup;
    }

    [Fact]
    public async Task Complete_LookupThrowsSqlLikeException_OnTimeout_ResponseIsStillReturned()
    {
        // SqlClient surfaces CancelAfter as SqlException, not OperationCanceledException.
        var response = await Enrich("P0236", LookupThrowing(new FakeSqlException()));

        response.NextAssignee.Should().Be("P0236");
        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Complete_CallerAborted_AndLookupThrowsSqlLikeException_ResponseIsStillReturned()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var response = await Enrich("P0236", LookupThrowing(new FakeSqlException()), ct: cts.Token);

        response.NextAssigneeDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ReadPath_CallerAborted_ThrowsOperationCanceled_WhateverTheLookupRaised()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => UserDisplayName.TryLookupAsync(
            LookupThrowing(new FakeSqlException()), ["john.doe"], NullLogger.Instance, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadPath_OtherFailureWhileCallerLive_FallsBackToEmptyMap()
    {
        var users = await UserDisplayName.TryLookupAsync(
            LookupThrowing(new FakeSqlException()), ["john.doe"], NullLogger.Instance, default);

        users.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPath_LookupHangs_TimesOut_FallsBackToEmptyMap()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return (IReadOnlyDictionary<string, UserLookupDto>)Users();
            });

        var users = await UserDisplayName.TryLookupAsync(
            lookup, ["john.doe"], NullLogger.Instance, default, timeout: TimeSpan.FromMilliseconds(50));

        users.Should().BeEmpty();
    }

    private sealed class LevelLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<Microsoft.Extensions.Logging.LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    [Fact]
    public async Task Lookup_UnexpectedFailure_LogsError()
    {
        var logger = new LevelLogger();
        await UserDisplayName.TryLookupAsync(LookupThrowing(new FakeSqlException()), ["a"], logger, default);

        logger.Levels.Should().Equal(Microsoft.Extensions.Logging.LogLevel.Error);
    }

    [Fact]
    public async Task Lookup_Timeout_LogsWarning()
    {
        var lookup = Substitute.For<IUserLookupService>();
        lookup.GetByUsernamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(Timeout.Infinite, ci.Arg<CancellationToken>());
                return (IReadOnlyDictionary<string, UserLookupDto>)Users();
            });
        var logger = new LevelLogger();

        await UserDisplayName.TryLookupAsync(lookup, ["a"], logger, default, timeout: TimeSpan.FromMilliseconds(50));

        logger.Levels.Should().Equal(Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public async Task Lookup_CallerAbortOnPostCommitPath_LogsDebugOnly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var logger = new LevelLogger();

        await UserDisplayName.TryLookupAsync(
            LookupThrowing(new FakeSqlException()), ["a"], logger, cts.Token, honourCallerCancellation: false);

        logger.Levels.Should().Equal(Microsoft.Extensions.Logging.LogLevel.Debug);
    }
}
