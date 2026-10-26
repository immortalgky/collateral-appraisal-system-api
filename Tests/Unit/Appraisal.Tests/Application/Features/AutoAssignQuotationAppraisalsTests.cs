using Appraisal.Contracts.Services;
using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Quotations.AutoAssign;
using Appraisal.Application.Features.Quotations.FinalizeQuotation;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Quotations;
using Appraisal.Infrastructure;
using Appraisal.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Auth.Contracts.Companies;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Configuration;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Covers the per-appraisal outcomes of the post-finalize auto-assign loop, and the finalize go-live guard.
/// </summary>
public class AutoAssignQuotationAppraisalsTests
{
    private static readonly Guid WinnerId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IQuotationRepository _repository = Substitute.For<IQuotationRepository>();
    private readonly IAppraisalUnitOfWork _unitOfWork = Substitute.For<IAppraisalUnitOfWork>();
    private readonly IQuotationAutoAssignStateReader _reader = Substitute.For<IQuotationAutoAssignStateReader>();
    private readonly ICompanyLookupService _companies = Substitute.For<ICompanyLookupService>();
    private readonly IUserGroupService _groups = Substitute.For<IUserGroupService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IIntegrationEventOutbox _outbox = Substitute.For<IIntegrationEventOutbox>();

    // What the reader returns for a single-appraisal re-read after the assign command ran.
    private readonly Dictionary<Guid, AppraisalAssignState> _after = new();
    private readonly Dictionary<Guid, AppraisalAssignState> _before = new();
    private readonly Dictionary<Guid, int> _reads = new();

    public AutoAssignQuotationAppraisalsTests()
    {
        _user.IsInRole("IntAdmin").Returns(true);
        _user.UserCode.Returns("P0001");
        _companies.GetByIdAsync(WinnerId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(WinnerId, "Winner Co", null, true));
        _groups.GetGroupsForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(["IntAppraisalStaff"]);
        // The loop reads one appraisal at a time: the first read is the pre-assign read, later ones the post-assign read.
        _reader.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var states = new List<AppraisalAssignState>();
                foreach (var id in ci.Arg<IReadOnlyCollection<Guid>>())
                {
                    var n = _reads[id] = _reads.GetValueOrDefault(id) + 1;
                    var state = n > 1 && _after.TryGetValue(id, out var after) ? after : _before.GetValueOrDefault(id);
                    if (state is not null)
                        states.Add(state);
                }

                return (IReadOnlyList<AppraisalAssignState>)states;
            });
    }

    private static AppraisalAssignState State(Guid id, string? activity = "appraisal-assignment",
        string? assignment = "Pending", string? workflowStatus = "Running", string? appraisalType = "New", string? workingBy = null,
        string? taskAssignedTo = "IntAdmin", string? taskAssignedType = "2", bool hasOpenFollowup = false) =>
        new()
        {
            AppraisalId = id,
            AppraisalNumber = "A-" + id.ToString("N")[..4],
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowStatus = workflowStatus,
            PendingActivityId = activity,
            AssignmentStatus = assignment,
            AppraisalType = appraisalType,
            WorkingBy = workingBy,
            TaskAssignedTo = taskAssignedTo,
            TaskAssignedType = taskAssignedType,
            HasOpenFollowup = hasOpenFollowup
        };

    private QuotationRequest Quotation(params Guid[] appraisalIds)
    {
        var now = DateTime.Now;
        var quotation = QuotationRequest.Create(now.AddDays(1), "admin", now);
        foreach (var id in appraisalIds)
            quotation.AddAppraisal(id, "admin", now);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(quotation, "Finalized");
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(quotation, WinnerId);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedQuotationId))!
            .SetValue(quotation, Guid.NewGuid());
        _reader.GetPricedAppraisalIdsAsync(quotation.SelectedQuotationId!.Value, Arg.Any<CancellationToken>())
            .Returns(appraisalIds);
        _repository.GetWithAppraisalsAsync(quotation.Id, Arg.Any<CancellationToken>()).Returns(quotation);
        return quotation;
    }

    private AutoAssignQuotationAppraisalsCommandHandler Handler()
    {
        var services = new ServiceCollection().AddSingleton(_sender).BuildServiceProvider();
        var dateTime = Substitute.For<IDateTimeProvider>();
        dateTime.ApplicationNow.Returns(DateTime.Now);
        return new AutoAssignQuotationAppraisalsCommandHandler(
            _repository, _unitOfWork, _reader, _companies, _groups, services.GetRequiredService<IServiceScopeFactory>(), _user, _outbox,
            dateTime, NullLogger<AutoAssignQuotationAppraisalsCommandHandler>.Instance);
    }

    private Task<IReadOnlyList<AutoAssignItemResult>> RunAsync(QuotationRequest quotation,
        params AutoAssignStaff[] staff) =>
        Handler().Handle(new AutoAssignQuotationAppraisalsCommand(quotation.Id, staff), Ct);

    [Fact]
    public async Task Ready_appraisal_that_lands_on_ext_assignment_is_Assigned_with_server_side_actor()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a);
        _before[b] = State(b);
        _after[a] = State(a, "ext-appraisal-assignment");
        _after[b] = State(b, "ext-appraisal-assignment");

        var results = await RunAsync(quotation, new AutoAssignStaff(a, " staff-7 "));

        Assert.All(results, r => Assert.Equal("Assigned", r.Result));
        await _sender.Received(1).Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == a
                && c.DecisionTaken == "EXT"
                && c.AssignmentMethod == "Quotation"
                && c.AssigneeCompanyId == WinnerId.ToString()
                && c.AssigneeCompanyName == "Winner Co"
                && c.InternalAppraiserId == "staff-7"
                && c.InternalFollowupAssignmentMethod == "manual"),
            Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == b
                && c.InternalAppraiserId == null
                && c.InternalFollowupAssignmentMethod == "roundrobin"),
            Arg.Any<CancellationToken>());
        Assert.All(quotation.Appraisals, l => Assert.Equal("Assigned", l.AutoAssignResult));
        Assert.All(quotation.Appraisals, l => Assert.NotNull(l.AutoAssignedAt));
        Assert.All(results, r => Assert.Null(r.ReasonCode));
        Assert.All(quotation.Appraisals, l => Assert.Null(l.AutoAssignReasonCode));
        _outbox.DidNotReceiveWithAnyArgs().Publish<QuotationAutoAssignNotificationIntegrationEvent>(default!);
    }

    [Fact]
    public async Task Instance_bounced_back_to_appraisal_assignment_is_Failed_with_the_fixed_no_match_message()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a);
        _after[a] = State(a, "appraisal-assignment");

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Failed", result.Result);
        Assert.Contains("returned no match", result.Message);
        Assert.Equal("Failed", quotation.Appraisals.Single().AutoAssignResult);
        Assert.Equal(result.Message, quotation.Appraisals.Single().AutoAssignMessage);
        Assert.Equal("noMatch", result.ReasonCode);
        Assert.Equal("noMatch", quotation.Appraisals.Single().AutoAssignReasonCode);
    }

    [Fact]
    public async Task Exception_fails_only_that_appraisal_and_keeps_internals_out_of_the_message()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var quotation = Quotation(a, b, c);
        foreach (var id in new[] { a, b, c })
        {
            _before[id] = State(id);
            _after[id] = State(id, "ext-appraisal-assignment");
        }

        _sender.Send(Arg.Is<AssignAppraisalCommand>(x => x.AppraisalId == a), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BadRequestException("Selected company is excluded from this appraisal assignment."));
        _sender.Send(Arg.Is<AssignAppraisalCommand>(x => x.AppraisalId == b), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Login failed for user 'sa': SELECT * FROM auth.AspNetUsers at Server=db01"));

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Failed", results[a].Result);
        Assert.Equal("Selected company is excluded from this appraisal assignment.", results[a].Message);
        Assert.Equal("Failed", results[b].Result);
        Assert.Equal("Assignment failed. Check the appraisal and assign it on the Administration page.", results[b].Message);
        Assert.DoesNotContain("SELECT", results[b].Message);
        Assert.Equal("assignRejected", results[a].ReasonCode);
        Assert.Equal("assignFailed", results[b].ReasonCode);
        Assert.Null(results[c].ReasonCode);
        Assert.Equal("Assigned", results[c].Result);
    }

    [Fact]
    public async Task An_appraisal_with_no_base_fee_is_left_for_the_admin_with_its_own_reason()
    {
        // AssignAppraisalCommandHandler refuses an assignment that would bill no base fee (01); auto-assign must not
        // assign it, and must say why with a code the UI can translate (the other appraisals still go through).
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        foreach (var id in new[] { a, b })
        {
            _before[id] = State(id);
            _after[id] = State(id, "ext-appraisal-assignment");
        }

        _sender.Send(Arg.Is<AssignAppraisalCommand>(x => x.AppraisalId == a), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BadRequestException("Cannot assign: no base appraisal fee (01) would be billed.", null,
                AssignAppraisalCommandHandler.FeeBaseMissingCode));

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Failed", results[a].Result);
        Assert.Equal("feeBaseMissing", results[a].ReasonCode);
        Assert.Contains("no base appraisal fee (01)", results[a].Message);
        Assert.Equal("Assigned", results[b].Result);
    }

    [Fact]
    public async Task Appraisal_not_waiting_at_appraisal_assignment_is_Parked_and_not_assigned()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a, "appraisal-initiation");
        _before[b] = State(b, "int-pma-input");

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Parked", results[a].Result);
        Assert.Contains("appraisal-initiation", results[a].Message);
        Assert.Equal("Parked", results[b].Result);
        Assert.Contains("int-pma-input", results[b].Message);
        Assert.All(results.Values, r => Assert.Equal("notAtAssignment", r.ReasonCode));
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Appraisal_whose_assignment_is_not_Pending_is_Skipped_not_Parked()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = _after[a] = State(a, "ext-appraisal-assignment", assignment: "Assigned");

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Skipped", result.Result);
        Assert.Equal("Assignment is not pending (status 'Assigned')", result.Message);
        Assert.Equal("notPending", result.ReasonCode);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Unavailable_winner_fails_ready_appraisals_without_assigning()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a);
        _before[b] = State(b, "appraisal-initiation");
        _companies.GetByIdAsync(WinnerId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(WinnerId, "Winner Co", null, false));

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Failed", results[a].Result);
        Assert.Equal("Winning company is not available for assignment", results[a].Message);
        Assert.Equal("winnerUnavailable", results[a].ReasonCode);
        Assert.Equal("Parked", results[b].Result);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Cancelled_completed_failed_or_missing_workflow_is_Skipped()
    {
        var cancelled = Guid.NewGuid();
        var completed = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var failed = Guid.NewGuid();
        var quotation = Quotation(cancelled, completed, missing, failed);
        _before[failed] = State(failed, null, workflowStatus: "Failed");
        _before[cancelled] = State(cancelled, null, workflowStatus: "Cancelled");
        _before[completed] = State(completed, null, workflowStatus: "Completed");
        // `missing` has no row at all in the reader.

        var results = await RunAsync(quotation);

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.Equal("Skipped", r.Result));
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Finalizer_is_notified_when_any_appraisal_is_not_Assigned_and_only_then()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a);
        _after[a] = State(a, "ext-appraisal-assignment");
        _before[b] = State(b, "appraisal-initiation");

        await RunAsync(quotation);

        _outbox.Received(1).Publish(
            Arg.Is<QuotationAutoAssignNotificationIntegrationEvent>(e =>
                e.Recipient == "P0001"
                && e.QuotationRequestId == quotation.Id
                && e.ActionUrl == $"/quotations/{quotation.Id}"
                && e.Message.Contains("1 of 2")
                && e.Message.Contains("1 need")),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task Skipped_appraisals_alone_do_not_trigger_the_incomplete_notification()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a, null, workflowStatus: "Cancelled");
        // `b` is not returned by the reader at all (soft-deleted).

        var results = await RunAsync(quotation);

        Assert.All(results, r => Assert.Equal("Skipped", r.Result));
        Assert.Contains(results, r => r.Message == "Appraisal was deleted");
        _outbox.DidNotReceiveWithAnyArgs().Publish<QuotationAutoAssignNotificationIntegrationEvent>(default!);
    }

    [Fact]
    public async Task Construction_inspection_is_assigned_to_the_quotation_winner_like_any_other_appraisal()
    {
        // The explicit winner beats the company forced from the prior appraisal (CompanySelectionActivity).
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a, appraisalType: "Progressive");
        _after[a] = State(a, "ext-appraisal-assignment", assignment: "Assigned", appraisalType: "Progressive");

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Assigned", result.Result);
        Assert.Equal(AutoAssignClassification.Ready, AutoAssignClassifier.Classify(_before[a], true, true, "P0001"));
        await _sender.Received(1).Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == a && c.AssigneeCompanyId == WinnerId.ToString()
                                                && c.AssignmentMethod == "Quotation"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Task_held_by_another_admin_fails_but_the_callers_own_lock_does_not_block()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a, workingBy: "P0999");
        _before[b] = State(b, workingBy: "p0001"); // the caller, different casing
        _after[b] = State(b, "ext-appraisal-assignment");

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Failed", results[a].Result);
        Assert.Equal("Another admin is working on this task (P0999)", results[a].Message);
        Assert.Equal("taskHeld", results[a].ReasonCode);
        Assert.Equal("Assigned", results[b].Result);
        await _sender.DidNotReceive().Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == a), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Personal_task_held_by_another_admin_fails_but_the_callers_own_and_pool_tasks_do_not()
    {
        var other = Guid.NewGuid();
        var own = Guid.NewGuid();
        var pool = Guid.NewGuid();
        var quotation = Quotation(other, own, pool);
        _before[other] = State(other, taskAssignedTo: "P0999", taskAssignedType: "1");
        _before[own] = State(own, taskAssignedTo: "p0001", taskAssignedType: "1");
        _before[pool] = State(pool, taskAssignedTo: "IntAdmin", taskAssignedType: "2");
        _after[own] = State(own, "ext-appraisal-assignment");
        _after[pool] = State(pool, "ext-appraisal-assignment");

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Failed", results[other].Result);
        Assert.Equal("Task is assigned to another admin (P0999)", results[other].Message);
        Assert.Equal("Assigned", results[own].Result);
        Assert.Equal("Assigned", results[pool].Result);
        await _sender.DidNotReceive().Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == other), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Staff_outside_the_follow_up_pool_fails_that_appraisal_without_assigning()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a);
        _groups.GetGroupsForUserAsync("outsider", Arg.Any<CancellationToken>()).Returns(["Admin"]);

        var result = Assert.Single(await RunAsync(quotation, new AutoAssignStaff(a, "outsider")));

        Assert.Equal("Failed", result.Result);
        Assert.Equal("Selected follow-up staff is not eligible", result.Message);
        Assert.Equal("staffIneligible", result.ReasonCode);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Appraisal_the_winner_did_not_price_fails_without_assigning()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a);
        _before[b] = State(b);
        _after[a] = State(a, "ext-appraisal-assignment");
        _reader.GetPricedAppraisalIdsAsync(quotation.SelectedQuotationId!.Value, Arg.Any<CancellationToken>())
            .Returns([a]);

        var results = (await RunAsync(quotation)).ToDictionary(r => r.AppraisalId);

        Assert.Equal("Assigned", results[a].Result);
        Assert.Equal("Failed", results[b].Result);
        Assert.Equal("Winning company has no price for this appraisal", results[b].Message);
        Assert.Equal("unpriced", results[b].ReasonCode);
        await _sender.DidNotReceive().Send(
            Arg.Is<AssignAppraisalCommand>(c => c.AppraisalId == b), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task State_is_re_read_right_before_each_assign_and_a_moved_appraisal_is_Parked()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a, "appraisal-initiation"); // the fresh read, taken inside the loop

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Parked", result.Result);
        await _reader.Received(1).GetAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == a), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Non_admin_and_non_finalized_quotations_are_rejected()
    {
        var quotation = Quotation(Guid.NewGuid());
        _user.IsInRole("IntAdmin").Returns(false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => RunAsync(quotation));

        _user.IsInRole("IntAdmin").Returns(true);
        typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(quotation, "WinnerTentative");
        await Assert.ThrowsAsync<BadRequestException>(() => RunAsync(quotation));
    }

    [Fact]
    public async Task Finalize_with_auto_assign_is_a_400_before_anything_is_finalized_while_go_live_switch_is_off()
    {
        var config = Substitute.For<ISystemConfigurationReader>();
        config.GetBoolAsync("ExternalCompanyAssignmentEnabled", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var dateTime = Substitute.For<IDateTimeProvider>();
        var handler = new FinalizeQuotationCommandHandler(
            _repository, _user, _outbox, Substitute.For<IQuotationActivityLogger>(), dateTime, config);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new FinalizeQuotationCommand(Guid.NewGuid(), Guid.NewGuid(), AutoAssignRequested: true), Ct));

        await _repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        _outbox.DidNotReceiveWithAnyArgs().Publish<QuotationFinalizedIntegrationEvent>(default!);
    }

    [Theory]
    [InlineData("Running", true)]
    [InlineData("Suspended", true)]
    [InlineData("Cancelled", false)]
    [InlineData("Completed", false)]
    [InlineData("Failed", false)]
    [InlineData(null, false)]
    public void Preview_workflowActive_is_false_without_a_live_instance(string? status, bool expected)
    {
        var state = State(Guid.NewGuid(), workflowStatus: status);
        if (status is null)
            state = state with { WorkflowInstanceId = null };

        Assert.Equal(expected, state.WorkflowActive);
    }

    [Fact]
    public async Task Committed_assignment_is_never_reported_Failed_when_the_state_re_read_throws()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a);
        var calls = 0;
        _reader.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ++calls == 1
                ? Task.FromResult<IReadOnlyList<AppraisalAssignState>>([_before[a]])
                : throw new TimeoutException("db"));

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Assigned", result.Result);
        Assert.Equal("Assigned; final state could not be verified", result.Message);
        Assert.Equal("unverified", result.ReasonCode);
    }

    [Fact]
    public async Task Unexpected_error_before_the_loop_still_records_Failed_for_every_appraisal()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _companies.GetByIdAsync(WinnerId, Arg.Any<CancellationToken>())
            .Returns<CompanyLookupDto?>(_ => throw new TimeoutException("db"));

        var results = await RunAsync(quotation);

        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal("Failed", r.Result);
            Assert.Equal("Auto-assign stopped before this appraisal was processed", r.Message);
            Assert.Equal("stopped", r.ReasonCode);
        });
        Assert.All(quotation.Appraisals, l => Assert.Equal("Failed", l.AutoAssignResult));
        Assert.All(quotation.Appraisals, l => Assert.Equal("stopped", l.AutoAssignReasonCode));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Results_and_notification_are_saved_once_and_a_failed_save_does_not_throw()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        _before[a] = State(a, "appraisal-initiation");
        _before[b] = State(b, "appraisal-initiation");
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<int>(_ => throw new Exception("save"));

        var results = await RunAsync(quotation);

        Assert.Equal(2, results.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── preview: mirrors the checks the loop runs ───────────────────────────────────────────

    [Fact]
    public async Task Preview_reports_pricing_and_who_holds_the_task()
    {
        var free = Guid.NewGuid();
        var locked = Guid.NewGuid();
        var personal = Guid.NewGuid();
        var unpriced = Guid.NewGuid();
        var companyQuotationId = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        _reader.GetQuotationAppraisalIdsAsync(quotationId, Arg.Any<CancellationToken>())
            .Returns([free, locked, personal, unpriced]);
        _reader.GetWinnerAsync(quotationId, Arg.Any<CancellationToken>())
            .Returns(new QuotationWinner(companyQuotationId, WinnerId));
        _reader.GetPricedAppraisalIdsAsync(companyQuotationId, Arg.Any<CancellationToken>())
            .Returns([free, locked, personal]);
        _before[free] = State(free);
        _before[locked] = State(locked, workingBy: "P0999");
        _before[personal] = State(personal, taskAssignedTo: "P0888", taskAssignedType: "1");
        _before[unpriced] = State(unpriced);
        var config = Substitute.For<ISystemConfigurationReader>();
        config.GetBoolAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new GetAutoAssignPreviewQueryHandler(_reader, config, _companies, _user);

        var result = await handler.Handle(new GetAutoAssignPreviewQuery(quotationId), Ct);

        var byId = result.Appraisals.ToDictionary(a => a.AppraisalId);
        Assert.Equal("Ready", byId[free].PredictedOutcome);
        Assert.Null(byId[free].TaskHeldBy);
        Assert.Equal("P0999", byId[locked].TaskHeldBy);
        Assert.Equal("P0888", byId[personal].TaskHeldBy);
        Assert.Equal("unpriced", byId[unpriced].Reason);
    }

    [Fact]
    public async Task Preview_marks_ready_appraisals_winnerUnavailable_when_there_is_no_winner_or_the_company_is_unavailable()
    {
        var quotationId = Guid.NewGuid();
        var a = Guid.NewGuid();
        _before[a] = State(a);
        _reader.GetQuotationAppraisalIdsAsync(quotationId, Arg.Any<CancellationToken>()).Returns([a]);
        _reader.GetPricedAppraisalIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([a]);
        _reader.GetWinnerAsync(quotationId, Arg.Any<CancellationToken>()).Returns(new QuotationWinner(null, null));
        var config = Substitute.For<ISystemConfigurationReader>();
        var handler = new GetAutoAssignPreviewQueryHandler(_reader, config, _companies, _user);

        Assert.Equal("winnerUnavailable", (await handler.Handle(new GetAutoAssignPreviewQuery(quotationId), Ct)).Appraisals.Single().Reason);

        _reader.GetWinnerAsync(quotationId, Arg.Any<CancellationToken>()).Returns(new QuotationWinner(Guid.NewGuid(), WinnerId));
        _companies.GetByIdAsync(WinnerId, Arg.Any<CancellationToken>())
            .Returns(new CompanyLookupDto(WinnerId, "Winner Co", null, false));
        Assert.Equal("winnerUnavailable", (await handler.Handle(new GetAutoAssignPreviewQuery(quotationId), Ct)).Appraisals.Single().Reason);
    }

    // ── fee source: the most recently FINALIZED quotation wins ─────────────────────────────

    [Fact]
    public async Task Finalized_lookup_orders_by_SelectedAt_not_RequestDate()
    {
        var appraisalId = Guid.NewGuid();
        var company = Guid.NewGuid();
        await using var db = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        QuotationRequest Finalized(DateTime requested, DateTime finalizedAt)
        {
            var q = QuotationRequest.Create(requested.AddDays(1), "admin", requested);
            q.AddAppraisal(appraisalId, "admin", requested);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(q, "Finalized");
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(q, finalizedAt);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(q, company);
            return q;
        }

        // Requested later but finalized earlier, versus requested earlier but finalized later: SelectedAt wins.
        var newerRequest = Finalized(new DateTime(2026, 10, 10), new DateTime(2026, 10, 11));
        var laterFinalize = Finalized(new DateTime(2026, 10, 1), new DateTime(2026, 10, 15));
        db.QuotationRequests.AddRange(newerRequest, laterFinalize);
        await db.SaveChangesAsync(Ct);

        var found = await new QuotationRepository(db).GetFinalizedByAppraisalIdAsync(appraisalId, company, Ct);

        Assert.Equal(laterFinalize.Id, found!.Id);
    }

    [Fact]
    public async Task Finalized_lookup_returns_the_latest_finalized_quotation_only_if_that_company_won_it()
    {
        var appraisalId = Guid.NewGuid();
        var companyX = Guid.NewGuid();
        var companyY = Guid.NewGuid();
        await using var db = new AppraisalDbContext(new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        QuotationRequest Finalized(Guid winner, DateTime finalizedAt)
        {
            var q = QuotationRequest.Create(finalizedAt.AddDays(1), "admin", finalizedAt.AddDays(-5));
            q.AddAppraisal(appraisalId, "admin", finalizedAt.AddDays(-5));
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.Status))!.SetValue(q, "Finalized");
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedAt))!.SetValue(q, finalizedAt);
            typeof(QuotationRequest).GetProperty(nameof(QuotationRequest.SelectedCompanyId))!.SetValue(q, winner);
            return q;
        }

        var wonByX = Finalized(companyX, new DateTime(2026, 10, 1));
        var wonByY = Finalized(companyY, new DateTime(2026, 10, 15)); // the current round chose Y
        db.QuotationRequests.AddRange(wonByX, wonByY);
        await db.SaveChangesAsync(Ct);
        var repository = new QuotationRepository(db);

        // X won only the superseded round: it must not be billed from it.
        Assert.Null(await repository.GetFinalizedByAppraisalIdAsync(appraisalId, companyX, Ct));
        Assert.Equal(wonByY.Id, (await repository.GetFinalizedByAppraisalIdAsync(appraisalId, companyY, Ct))!.Id);
        Assert.Null(await repository.GetFinalizedByAppraisalIdAsync(appraisalId, Guid.NewGuid(), Ct));
    }

    // ── finalize endpoint: finalize has committed, so auto-assign can never turn it into an HTTP error ──

    private static FinalizeQuotationResult Finalized(params Guid[] appraisalIds) =>
        new(Guid.NewGuid(), Guid.NewGuid(), WinnerId, 100m, "Finalized", appraisalIds);

    [Fact]
    public async Task Endpoint_returns_200_with_Failed_for_every_appraisal_when_the_auto_assign_command_throws()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var finalized = Finalized(a, b);
        _sender.Send(Arg.Any<FinalizeQuotationCommand>(), Arg.Any<CancellationToken>()).Returns(finalized);
        _sender.Send(Arg.Any<AutoAssignQuotationAppraisalsCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("db"));

        var response = await FinalizeQuotationEndpoint.FinalizeAsync(
            Guid.NewGuid(), new FinalizeQuotationRequest(Guid.NewGuid(), null, new AutoAssignRequest()),
            _sender, NullLogger.Instance, Ct);

        var body = Assert.IsType<Ok<FinalizeQuotationResult>>(response).Value!;
        Assert.Equal("Finalized", body.Status);
        Assert.Equal([a, b], body.AutoAssignResults!.Select(r => r.AppraisalId));
        Assert.All(body.AutoAssignResults!, r =>
        {
            Assert.Equal("Failed", r.Result);
            Assert.Equal(AutoAssignQuotationAppraisalsCommandHandler.UnexpectedStopMessage, r.Message);
            Assert.Equal("unexpectedStop", r.ReasonCode);
        });
    }

    [Fact]
    public async Task Endpoint_without_autoAssign_does_not_request_it_and_returns_no_results()
    {
        _sender.Send(Arg.Any<FinalizeQuotationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Finalized(Guid.NewGuid()));

        var response = await FinalizeQuotationEndpoint.FinalizeAsync(
            Guid.NewGuid(), new FinalizeQuotationRequest(Guid.NewGuid()), _sender, NullLogger.Instance, Ct);

        Assert.Null(Assert.IsType<Ok<FinalizeQuotationResult>>(response).Value!.AutoAssignResults);
        await _sender.Received(1).Send(
            Arg.Is<FinalizeQuotationCommand>(c => !c.AutoAssignRequested), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(
            Arg.Any<AutoAssignQuotationAppraisalsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Endpoint_with_autoAssign_requests_it_on_the_finalize_command()
    {
        var finalized = Finalized(Guid.NewGuid());
        var results = new List<AutoAssignItemResult> { new(finalized.AppraisalIds[0], "Assigned", null) };
        _sender.Send(Arg.Any<FinalizeQuotationCommand>(), Arg.Any<CancellationToken>()).Returns(finalized);
        _sender.Send(Arg.Any<AutoAssignQuotationAppraisalsCommand>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AutoAssignItemResult>)results);

        var response = await FinalizeQuotationEndpoint.FinalizeAsync(
            Guid.NewGuid(), new FinalizeQuotationRequest(Guid.NewGuid(), null, new AutoAssignRequest()),
            _sender, NullLogger.Instance, Ct);

        Assert.Equal(results, Assert.IsType<Ok<FinalizeQuotationResult>>(response).Value!.AutoAssignResults);
        await _sender.Received(1).Send(
            Arg.Is<FinalizeQuotationCommand>(c => c.AutoAssignRequested), Arg.Any<CancellationToken>());
    }

    // ── classifier: one ladder shared by the loop and the preview ───────────────────────────

    [Fact]
    public void Classifier_applies_the_checks_in_the_documented_order()
    {
        var id = Guid.NewGuid();
        string? Reason(AppraisalAssignState? s, bool winner = true, bool priced = true) =>
            AutoAssignClassifier.Classify(s, winner, priced, "P0001").Reason;

        Assert.Equal("deleted", Reason(null));
        Assert.Equal("workflowEnded", Reason(State(id, workflowStatus: "Cancelled")));
        Assert.Equal("noAssignment", Reason(State(id, assignment: null)));
        Assert.Equal("notPending", Reason(State(id, "ext-appraisal-assignment", assignment: "Assigned")));
        Assert.Equal("assignmentNotReset", Reason(State(id, "appraisal-assignment", assignment: "Assigned")));
        // Construction inspection has no ladder step of its own: it is judged like any other appraisal.
        Assert.Equal("notAtAssignment",
            Reason(State(id, "appraisal-initiation", appraisalType: "Progressive"), winner: false));
        // An unavailable winner or missing price never hides that the appraisal is simply not there yet.
        Assert.Equal("notAtAssignment", Reason(State(id, "appraisal-initiation"), winner: false, priced: false));
        Assert.Equal("taskHeld", Reason(State(id, workingBy: "P0999"), winner: false, priced: false));
        // An open follow-up blocks normal completion; it is reported before winner/price problems.
        Assert.Equal("documentFollowupOpen", Reason(State(id, hasOpenFollowup: true), winner: false, priced: false));
        Assert.Equal("taskHeld", Reason(State(id, workingBy: "P0999", hasOpenFollowup: true)));
        Assert.Equal("winnerUnavailable", Reason(State(id), winner: false, priced: false));
        Assert.Equal("unpriced", Reason(State(id), priced: false));
        Assert.Equal(AutoAssignClassification.Ready, AutoAssignClassifier.Classify(State(id), true, true, "P0001"));
    }

    [Fact]
    public void Classifier_outcomes_match_the_documented_categories()
    {
        var id = Guid.NewGuid();
        string? Outcome(AppraisalAssignState? s, bool winner = true) =>
            AutoAssignClassifier.Classify(s, winner, true, "P0001").Outcome;

        Assert.Equal("Skipped", Outcome(null));
        Assert.Null(Outcome(State(id, appraisalType: "Progressive")));
        Assert.Equal("Parked", Outcome(State(id, "appraisal-initiation"), winner: false));
        Assert.Equal("Failed", Outcome(State(id, taskAssignedTo: "P0888", taskAssignedType: "1")));
        Assert.Null(Outcome(State(id)));
        // Task back at appraisal-assignment but assignment not reset: needs a person; elsewhere it is a skip.
        Assert.Equal("Failed", Outcome(State(id, "appraisal-assignment", assignment: "Rejected")));
        Assert.Equal("Skipped", Outcome(State(id, "int-appraisal-execution", assignment: "InProgress")));
    }

    [Fact]
    public async Task Preview_exposes_the_classifiers_predicted_outcome_and_reason()
    {
        var ready = Guid.NewGuid();
        var parked = Guid.NewGuid();
        var quotationId = Guid.NewGuid();
        var companyQuotationId = Guid.NewGuid();
        _reader.GetQuotationAppraisalIdsAsync(quotationId, Arg.Any<CancellationToken>()).Returns([ready, parked]);
        _reader.GetWinnerAsync(quotationId, Arg.Any<CancellationToken>())
            .Returns(new QuotationWinner(companyQuotationId, WinnerId));
        _reader.GetPricedAppraisalIdsAsync(companyQuotationId, Arg.Any<CancellationToken>()).Returns([ready, parked]);
        _before[ready] = State(ready);
        _before[parked] = State(parked, "appraisal-initiation");
        var config = Substitute.For<ISystemConfigurationReader>();
        var handler = new GetAutoAssignPreviewQueryHandler(_reader, config, _companies, _user);

        var byId = (await handler.Handle(new GetAutoAssignPreviewQuery(quotationId), Ct))
            .Appraisals.ToDictionary(a => a.AppraisalId);

        Assert.Equal("Ready", byId[ready].PredictedOutcome);
        Assert.Null(byId[ready].Reason);
        Assert.Equal("Parked", byId[parked].PredictedOutcome);
        Assert.Equal("notAtAssignment", byId[parked].Reason);
    }

    [Fact]
    public async Task Staff_eligibility_is_looked_up_once_per_distinct_staff_id()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var quotation = Quotation(a, b);
        foreach (var id in new[] { a, b })
        {
            _before[id] = State(id);
            _after[id] = State(id, "ext-appraisal-assignment");
        }

        await RunAsync(quotation, new AutoAssignStaff(a, "staff-7"), new AutoAssignStaff(b, "STAFF-7"));

        await _groups.Received(1).GetGroupsForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failing_notification_publish_does_not_lose_the_recorded_results()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a, "appraisal-initiation"); // Parked -> incomplete -> publish
        _outbox.When(o => o.Publish(Arg.Any<QuotationAutoAssignNotificationIntegrationEvent>(),
                Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>()))
            .Do(_ => throw new InvalidOperationException("outbox"));

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Parked", result.Result);
        Assert.Equal("Parked", quotation.Appraisals.Single().AutoAssignResult);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pma_appraisal_ignores_a_posted_staff_id_and_is_not_blocked_by_the_group_check()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a) with { IsPma = true };
        _after[a] = State(a, "ext-appraisal-assignment");
        _groups.GetGroupsForUserAsync("outsider", Arg.Any<CancellationToken>()).Returns(["Admin"]);

        var result = Assert.Single(await RunAsync(quotation, new AutoAssignStaff(a, "outsider")));

        Assert.Equal("Assigned", result.Result);
        await _sender.Received(1).Send(
            Arg.Is<AssignAppraisalCommand>(c => c.InternalAppraiserId == null
                && c.InternalFollowupAssignmentMethod == "roundrobin"),
            Arg.Any<CancellationToken>());
        await _groups.DidNotReceiveWithAnyArgs().GetGroupsForUserAsync(default!, default);
    }

    // ── finalize request validation (runs before the finalize transaction) ─────────────────

    private static FluentValidation.Results.ValidationResult Validate(params AutoAssignStaff?[]? staff) =>
        new FinalizeQuotationCommandValidator().Validate(
            new FinalizeQuotationCommand(Guid.NewGuid(), Guid.NewGuid(), AutoAssignRequested: true,
                AutoAssignStaff: staff));

    [Fact]
    public void Finalize_validator_rejects_null_empty_and_duplicate_staff_entries()
    {
        var a = Guid.NewGuid();

        Assert.False(Validate(new AutoAssignStaff(a, "s1"), null).IsValid);
        Assert.False(Validate(new AutoAssignStaff(Guid.Empty, "s1")).IsValid);
        Assert.False(Validate(new AutoAssignStaff(a, "s1"), new AutoAssignStaff(a, "s2")).IsValid);
    }

    [Fact]
    public void Finalize_validator_accepts_no_staff_blank_staff_ids_and_distinct_appraisals()
    {
        Assert.True(Validate((AutoAssignStaff?[]?)null).IsValid);
        Assert.True(Validate().IsValid);
        Assert.True(Validate(new AutoAssignStaff(Guid.NewGuid(), ""), new AutoAssignStaff(Guid.NewGuid(), null!)).IsValid);
    }

    [Fact]
    public async Task Incomplete_notification_falls_back_to_the_quotation_id_when_it_has_no_number()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a, "appraisal-initiation");

        await RunAsync(quotation);

        _outbox.Received(1).Publish(
            Arg.Is<QuotationAutoAssignNotificationIntegrationEvent>(e => e.Message.Contains(quotation.Id.ToString())),
            Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task Committed_assignment_is_reported_Assigned_when_the_re_read_finds_nothing()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        var calls = 0;
        _reader.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? (IReadOnlyList<AppraisalAssignState>)[State(a)]
                : []); // the post-assign read returns no row

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Assigned", result.Result);
        Assert.Equal("Assigned; final state could not be verified", result.Message);
        Assert.Equal("unverified", result.ReasonCode);
    }

    [Fact]
    public async Task Handler_level_failures_record_their_reason_code()
    {
        var unreadable = Guid.NewGuid();
        var validation = Guid.NewGuid();
        var quotation = Quotation(unreadable, validation);
        _before[validation] = State(validation);
        _reader.GetAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(unreadable)), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AppraisalAssignState>>(_ => throw new TimeoutException("db"));
        _groups.GetGroupsForUserAsync("flaky", Arg.Any<CancellationToken>())
            .Returns<List<string>>(_ => throw new TimeoutException("auth"));

        var results = (await RunAsync(quotation, new AutoAssignStaff(validation, "flaky")))
            .ToDictionary(r => r.AppraisalId);

        Assert.Equal("stateUnreadable", results[unreadable].ReasonCode);
        Assert.Equal("staffValidationFailed", results[validation].ReasonCode);
        Assert.All(results.Values, r => Assert.Equal("Failed", r.Result));
    }

    [Fact]
    public async Task Committed_assign_at_an_unexpected_activity_is_Assigned_with_workflowElsewhere()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a);
        _after[a] = State(a, "ext-appraisal-execution");

        var result = Assert.Single(await RunAsync(quotation));

        // The assign committed, so a workflow that moved on is Assigned (not counted as incomplete).
        Assert.Equal("Assigned", result.Result);
        Assert.Equal("workflowElsewhere", result.ReasonCode);
        Assert.Equal("Workflow is at 'ext-appraisal-execution' after assignment", result.Message);
        var link = quotation.Appraisals.Single();
        Assert.Equal("Assigned", link.AutoAssignResult);
        Assert.Equal("workflowElsewhere", link.AutoAssignReasonCode);
        _outbox.DidNotReceiveWithAnyArgs().Publish<QuotationAutoAssignNotificationIntegrationEvent>(default!);
    }

    [Fact]
    public void Classifier_emits_a_reason_code_for_every_non_ready_outcome()
    {
        var id = Guid.NewGuid();
        string? Code(AppraisalAssignState? state, bool assignable = true, bool priced = true) =>
            AutoAssignClassifier.Classify(state, assignable, priced, "P0001").Reason;

        Assert.Equal("deleted", Code(null));
        Assert.Equal("workflowEnded", Code(State(id, workflowStatus: "Cancelled")));
        Assert.Equal("noAssignment", Code(State(id, assignment: null)));
        Assert.Equal("assignmentNotReset", Code(State(id, assignment: "Assigned")));
        Assert.Equal("notPending", Code(State(id, "ext-appraisal-assignment", assignment: "Assigned")));
        Assert.Null(Code(State(id, appraisalType: "Progressive")));
        Assert.Equal("notAtAssignment", Code(State(id, "appraisal-initiation")));
        Assert.Equal("taskHeld", Code(State(id, workingBy: "P0999")));
        Assert.Equal("documentFollowupOpen", Code(State(id, hasOpenFollowup: true)));
        Assert.Equal("winnerUnavailable", Code(State(id), assignable: false));
        Assert.Equal("unpriced", Code(State(id), priced: false));
        Assert.Null(Code(State(id)));
    }

    [Fact]
    public void Unpriced_winner_is_accepted_when_the_admin_already_keyed_the_base_fee()
    {
        // The admin's own 01 outranks the quotation at assignment, as in CompanyAssignedIntegrationEventHandler.
        var id = Guid.NewGuid();

        Assert.Equal(AutoAssignClassification.Ready,
            AutoAssignClassifier.Classify(State(id) with { HasAdminBaseFee = true }, true, priced: false, "P0001"));
        Assert.Equal("unpriced",
            AutoAssignClassifier.Classify(State(id), true, priced: false, "P0001").Reason);
        // It excuses the missing price only, not an unavailable winner.
        Assert.Equal("winnerUnavailable",
            AutoAssignClassifier.Classify(State(id) with { HasAdminBaseFee = true }, false, priced: false, "P0001").Reason);
    }

    [Fact]
    public async Task Open_document_follow_up_fails_the_appraisal_without_assigning()
    {
        var a = Guid.NewGuid();
        var quotation = Quotation(a);
        _before[a] = State(a, hasOpenFollowup: true);

        var result = Assert.Single(await RunAsync(quotation));

        Assert.Equal("Failed", result.Result);
        Assert.Equal("An open document follow-up must be cleared first. Assign it on the Administration page.",
            result.Message);
        await _sender.DidNotReceiveWithAnyArgs().Send(default(AssignAppraisalCommand)!, default);
    }

    [Fact]
    public async Task Finalize_with_auto_assign_on_a_task_started_quotation_is_a_400_before_it_finalizes()
    {
        var config = Substitute.For<ISystemConfigurationReader>();
        config.GetBoolAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        var now = DateTime.Now;
        var fromTask = QuotationRequest.CreateFromTask(now.AddDays(1), "admin", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "admin", now);
        Assert.False(fromTask.IsStandalone);
        _repository.GetByIdAsync(fromTask.Id, Arg.Any<CancellationToken>()).Returns(fromTask);
        var handler = new FinalizeQuotationCommandHandler(
            _repository, _user, _outbox, Substitute.For<IQuotationActivityLogger>(),
            Substitute.For<IDateTimeProvider>(), config);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new FinalizeQuotationCommand(fromTask.Id, Guid.NewGuid(), AutoAssignRequested: true), Ct));

        Assert.Equal("Auto-assign is only available for standalone quotations", ex.Message);
        Assert.NotEqual("Finalized", fromTask.Status);
        _outbox.DidNotReceiveWithAnyArgs().Publish<QuotationFinalizedIntegrationEvent>(default!);
    }

    [Fact]
    public void Standalone_is_true_only_for_quotations_not_started_from_a_task()
    {
        var now = DateTime.Now;
        Assert.True(QuotationRequest.Create(now.AddDays(1), "admin", now).IsStandalone);
    }

    [Fact]
    public async Task Preview_reports_whether_the_quotation_is_standalone()
    {
        var quotationId = Guid.NewGuid();
        _reader.GetQuotationAppraisalIdsAsync(quotationId, Arg.Any<CancellationToken>()).Returns([]);
        _reader.GetWinnerAsync(quotationId, Arg.Any<CancellationToken>()).Returns(new QuotationWinner(null, null));
        _reader.IsStandaloneAsync(quotationId, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new GetAutoAssignPreviewQueryHandler(
            _reader, Substitute.For<ISystemConfigurationReader>(), _companies, _user);

        Assert.True((await handler.Handle(new GetAutoAssignPreviewQuery(quotationId), Ct)).Standalone);
    }
}
