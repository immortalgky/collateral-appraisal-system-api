using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Workflow.Contracts.Sla;
using Workflow.Data;
using Workflow.Sla.Models;
using Workflow.Sla.Services;
using Workflow.Tasks.Models;

namespace Workflow.Tests.Sla;

/// <summary>
/// Unit tests for <see cref="SlaCalculator"/>:
/// anchor resolution (Assignment vs AppointmentDate), cumulative prior-leg deduction,
/// Reassigned leg exclusion, and budget-floor clamping.
///
/// Convention: IBusinessTimeCalculator is always mocked. WorkflowDbContext uses InMemory.
/// UseBusinessDays=false throughout so AddBusinessHoursAsync is never called unless noted.
/// </summary>
public class SlaCalculatorTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly IBusinessTimeCalculator _btc;
    private readonly SlaCalculator _calculator;

    // Activity key shared across most tests; mirrors the real appraisal-execution activity.
    private const string ActivityId = "int-appraisal-execution";

    public SlaCalculatorTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _btc = Substitute.For<IBusinessTimeCalculator>();
        _calculator = new SlaCalculator(_db, _btc, Substitute.For<ILogger<SlaCalculator>>());
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task SeedActivityPolicyAsync(
        string activityId,
        int durationHours,
        bool useBusinessDays = false,
        SlaAnchorType? anchorType = null,
        Guid? workflowDefinitionId = null)
    {
        var policy = SlaPolicy.Create(
            activityId, durationHours, useBusinessDays, priority: 1,
            workflowDefinitionId: workflowDefinitionId,
            anchorType: anchorType);
        _db.SlaPolicies.Add(policy);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a CompletedTask row that carries the given <paramref name="activityId"/>.
    /// The public <c>CompletedTask.Create</c> factory omits ActivityId, so this helper
    /// routes through <c>PendingTask.Create</c> →<c>CompletedTask.CreateFromPendingTask</c>.
    /// The intermediate PendingTask is never persisted.
    /// </summary>
    private async Task SeedCompletedTaskAsync(
        Guid correlationId,
        string activityId,
        DateTime assignedAt,
        DateTime completedAt,
        string actionTaken = "Completed")
    {
        var pt = PendingTask.Create(correlationId, "Task", "user1", "Individual",
            assignedAt, Guid.NewGuid(), activityId);
        var ct = CompletedTask.CreateFromPendingTask(pt, actionTaken, completedAt);
        _db.CompletedTasks.Add(ct);
        await _db.SaveChangesAsync();
    }

    private async Task<DateTime?> CalculateAsync(
        string activityId,
        DateTime assignedAt,
        DateTime? appointmentDate = null,
        Guid? correlationId = null,
        Guid? workflowDefinitionId = null)
        => (await _calculator.CalculateActivityDueAtAsync(
            activityId,
            workflowDefinitionId ?? Guid.NewGuid(),
            companyId: null,
            loanType: null,
            appraisalType: null,
            assignedAt: assignedAt,
            defaultTimeout: null,
            workflowDueAt: null,
            correlationId: correlationId,
            appointmentDate: appointmentDate)).DueAt;

    // ── Scenario 1: Appointment-anchored, future appointment ─────────────────────────────────

    /// <summary>
    /// Policy AnchorType=AppointmentDate, DurationHours=8.
    /// DueAt must be anchored to the appointment date, not AssignedAt.
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_AppointmentAnchored_UseAppointmentDateNotAssignedAt()
    {
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.AppointmentDate);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var appointmentDate = assignedAt.AddDays(2); // on-site visit 2 days later

        var dueAt = await CalculateAsync(ActivityId, assignedAt, appointmentDate);

        // DueAt = appointmentDate + 8h (calendar, UseBusinessDays=false)
        dueAt.Should().Be(appointmentDate.AddHours(8));
        // Must NOT use assignedAt as anchor
        dueAt.Should().NotBe(assignedAt.AddHours(8));
    }

    // ── Scenario 2: Appointment-anchored, no appointment yet → returns null ──────────────────

    /// <summary>
    /// Same policy as Scenario 1 but appointmentDate=null.
    /// Calculator must return null ("DueAt deferred — awaiting appointment").
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_AppointmentAnchored_NullAppointment_ReturnsNull()
    {
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.AppointmentDate);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, appointmentDate: null);

        dueAt.Should().BeNull(
            "appointment-anchored SLA cannot be computed without an appointment date; DueAt is deferred");
    }

    // ── Scenario 3a: Explicit Assignment anchor → uses assignedAt, ignores appointmentDate ───

    [Fact]
    public async Task CalculateActivityDueAt_AssignmentAnchored_UsesAssignedAtNotAppointmentDate()
    {
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.Assignment);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var appointmentDate = assignedAt.AddDays(5); // provided but must be ignored

        var dueAt = await CalculateAsync(ActivityId, assignedAt, appointmentDate);

        dueAt.Should().Be(assignedAt.AddHours(8));
        dueAt.Should().NotBe(appointmentDate.AddHours(8));
    }

    // ── Scenario 3b: Null AnchorType defaults to Assignment (backward-compatible) ─────────

    [Fact]
    public async Task CalculateActivityDueAt_NullAnchorType_DefaultsToAssignmentBehavior()
    {
        // Existing rows seeded without AnchorType must continue to use assignedAt.
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: null);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var appointmentDate = assignedAt.AddDays(5);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, appointmentDate);

        dueAt.Should().Be(assignedAt.AddHours(8),
            "null AnchorType is treated as Assignment for backward compatibility");
    }

    // ── Scenario 4: Cumulative deduction (rework — two valid prior legs) ─────────────────────

    /// <summary>
    /// Policy: DurationHours=8 (480 min). Two prior legs, each stubbed to 120 min.
    /// Cumulative = 240 min = 4 h. Remaining = 8 - 4 = 4 h.
    /// DueAt = assignedAt + 4 h.
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_TwoPriorLegs_DeductsCumulativeTimeFromBudget()
    {
        var correlationId = Guid.NewGuid();
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.Assignment);

        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 1, 9, 0, 0), new DateTime(2026, 5, 1, 11, 0, 0));
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 2, 9, 0, 0), new DateTime(2026, 5, 2, 11, 0, 0));

        // Each GetBusinessMinutesBetweenAsync call returns 120 minutes
        _btc.GetBusinessMinutesBetweenAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(120);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, correlationId: correlationId);

        // cumulativeMinutes=240, cumulativeHours=4, remaining=4
        dueAt.Should().Be(assignedAt.AddHours(4));
    }

    // ── Scenario 5: Reassigned legs excluded from cumulative ─────────────────────────────────

    /// <summary>
    /// Two legs: one Completed (120 min), one Reassigned (would be 120 min but must be excluded).
    /// Remaining = 8 - 2 = 6 h.
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_ReassignedLegExcluded_OnlyValidLegsDeductBudget()
    {
        var correlationId = Guid.NewGuid();
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.Assignment);

        // Valid leg
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 1, 9, 0, 0), new DateTime(2026, 5, 1, 11, 0, 0), "Completed");
        // Reassigned leg — must be excluded by the WHERE clause
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 3, 9, 0, 0), new DateTime(2026, 5, 3, 11, 0, 0), "Reassigned");

        _btc.GetBusinessMinutesBetweenAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(120);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, correlationId: correlationId);

        // Only 1 valid leg: 120 min = 2h. Remaining = 8 - 2 = 6h.
        dueAt.Should().Be(assignedAt.AddHours(6));

        // Confirm only 1 GetBusinessMinutesBetween call — Reassigned row was filtered out
        await _btc.Received(1).GetBusinessMinutesBetweenAsync(
            Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // ── Scenario 6: Cumulative fully consumed → DueAt clamped to anchor ──────────────────────

    /// <summary>
    /// Policy: DurationHours=4 (240 min). Prior legs total 300 min (> 240 min budget).
    /// remainingHours = max(0, 4 - 5) = 0 → DueAt = anchor exactly (no negative deadline).
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_CumulativeExceedsBudget_ClampsDueAtToAnchor()
    {
        var correlationId = Guid.NewGuid();
        await SeedActivityPolicyAsync(ActivityId, durationHours: 4, anchorType: SlaAnchorType.Assignment);

        // Two legs, each returning 150 min → total 300 min > 240 min budget
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 1, 9, 0, 0), new DateTime(2026, 5, 1, 11, 30, 0));
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 2, 9, 0, 0), new DateTime(2026, 5, 2, 11, 30, 0));

        _btc.GetBusinessMinutesBetweenAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(150);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, correlationId: correlationId);

        // cumulativeMinutes=300, remainingMinutes=max(0, 240-300)=0 → DueAt=assignedAt+0min
        dueAt.Should().Be(assignedAt,
            "remaining budget is clamped to zero when prior legs have already exceeded the SLA budget");
    }

    // ── Scenario 7 (M2): Sub-hour cumulative consumption preserves minute-level precision ────────

    /// <summary>
    /// M2 regression guard: when a prior leg consumed 70 minutes on an 8-hour (480 min) budget,
    /// the remaining budget must be 410 minutes, not 420 minutes.
    ///
    /// Old code: cumulativeHours = 70 / 60 = 1 (integer division, loses 10 min),
    ///           remainingHours  = 8 - 1 = 7,  DueAt = anchor + 7h (= anchor + 420 min) — WRONG.
    /// New code: remainingMinutes = 480 - 70 = 410, DueAt = anchor + 410 min — CORRECT.
    /// </summary>
    [Fact]
    public async Task CalculateActivityDueAt_SubHourCumulative_PreservesMinutePrecision()
    {
        var correlationId = Guid.NewGuid();
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.Assignment);

        // One prior leg consuming exactly 70 minutes (a non-multiple of 60).
        await SeedCompletedTaskAsync(correlationId, ActivityId,
            new DateTime(2026, 5, 1, 9, 0, 0), new DateTime(2026, 5, 1, 10, 10, 0));

        _btc.GetBusinessMinutesBetweenAsync(
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(70);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var dueAt = await CalculateAsync(ActivityId, assignedAt, correlationId: correlationId);

        // remainingMinutes = 480 - 70 = 410; DueAt = anchor + 410 min (not 420 min / 7 h).
        dueAt.Should().Be(assignedAt.AddMinutes(410),
            "480 min budget − 70 min consumed = 410 min remaining (not 7 h = 420 min from old integer division)");
        dueAt.Should().NotBe(assignedAt.AddHours(7),
            "old integer-division bug would have yielded 7 h; fix must produce 410 min instead");
    }

    // ── StartAt anchor (clock-start for the at-risk monitor) ─────────────────────────────────────

    /// <summary>Assignment-anchored: StartAt = AssignedAt (the at-risk clock starts at assignment).</summary>
    [Fact]
    public async Task CalculateActivityDueAt_AssignmentAnchored_StartAtIsAssignedAt()
    {
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.Assignment);
        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);

        var result = await _calculator.CalculateActivityDueAtAsync(
            ActivityId, Guid.NewGuid(), null, null, null, assignedAt, null, null);

        result.StartAt.Should().Be(assignedAt);
    }

    /// <summary>Appointment-anchored: StartAt = the appointment, NOT AssignedAt — so the monitor's 75%
    /// window is measured from the visit, not from when the task was assigned early.</summary>
    [Fact]
    public async Task CalculateActivityDueAt_AppointmentAnchored_StartAtIsAppointmentDate()
    {
        await SeedActivityPolicyAsync(ActivityId, durationHours: 8, anchorType: SlaAnchorType.AppointmentDate);
        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var appointmentDate = assignedAt.AddDays(2);

        var result = await _calculator.CalculateActivityDueAtAsync(
            ActivityId, Guid.NewGuid(), null, null, null, assignedAt, null, null,
            appointmentDate: appointmentDate);

        result.StartAt.Should().Be(appointmentDate,
            "appointment-anchored clock starts at the appointment, not AssignedAt");
    }

    // ── PART 4: window governs member task (ResolveGoverningStageDueAtAsync) ──────────────────────

    /// <summary>
    /// Seeds a Stage-scope window with an explicit MiddleActivityKeys member list (so the resolver
    /// uses the policy row directly and needs no WorkflowDefinition graph walk). WorkflowDefinitionId
    /// is left null so the window matches any workflow context.
    /// </summary>
    private async Task SeedStagePolicyAsync(
        string startActivityKey, string endActivityKey, string[] middle,
        int durationHours, int priority = 100,
        SlaAnchorType? anchorType = null, bool useBusinessDays = false)
    {
        var middleJson = System.Text.Json.JsonSerializer.Serialize(middle);
        var policy = SlaPolicy.Create(
            activityId: "*", durationHours, useBusinessDays, priority,
            scope: SlaPolicyScope.Stage,
            startActivityKey: startActivityKey,
            endActivityKey: endActivityKey,
            middleActivityKeys: middleJson,
            anchorType: anchorType);
        _db.SlaPolicies.Add(policy);
        await _db.SaveChangesAsync();
    }

    private Task<GoverningStageResult?> ResolveAsync(
        string activityId, DateTime assignedAt,
        DateTime? appointmentDate = null, Guid? correlationId = null)
        => _calculator.ResolveGoverningStageDueAtAsync(
            activityId,
            Guid.NewGuid(),
            companyId: null, loanType: null, appraisalType: null,
            assignedAt: assignedAt,
            correlationId: correlationId,
            appointmentDate: appointmentDate);

    /// <summary>A window member resolves to the window deadline (anchored at the start, no prior legs).</summary>
    [Fact]
    public async Task ResolveGoverningStage_MemberActivity_ReturnsWindowDeadline()
    {
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10, anchorType: SlaAnchorType.Assignment);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var result = await ResolveAsync("B", assignedAt, correlationId: Guid.NewGuid());

        result.Should().NotBeNull();
        result!.AnchorType.Should().Be(SlaAnchorType.Assignment);
        // No member legs → cumulative 0; start "A" has no entry → fallback to assignedAt; DueAt = +10h.
        result.DueAt.Should().Be(assignedAt.AddHours(10));
    }

    /// <summary>An activity in no window returns null — its per-activity clock stands.</summary>
    [Fact]
    public async Task ResolveGoverningStage_NonMemberActivity_ReturnsNull()
    {
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10);

        var result = await ResolveAsync("Z", new DateTime(2026, 6, 1, 9, 0, 0), correlationId: Guid.NewGuid());

        result.Should().BeNull("activity Z is not a member of any window — its per-activity clock stands");
    }

    /// <summary>When two windows cover the same member, the lower-Priority (override) one governs.</summary>
    [Fact]
    public async Task ResolveGoverningStage_MultipleWindows_LowestPriorityWins()
    {
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10, priority: 100, anchorType: SlaAnchorType.Assignment);
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 4, priority: 50, anchorType: SlaAnchorType.Assignment);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var result = await ResolveAsync("B", assignedAt, correlationId: Guid.NewGuid());

        result!.DueAt.Should().Be(assignedAt.AddHours(4),
            "the Priority-50 override window governs over the Priority-100 default");
    }

    /// <summary>An appointment-anchored window with no appointment is still GOVERNED but DueAt defers to null.</summary>
    [Fact]
    public async Task ResolveGoverningStage_AppointmentAnchoredNoAppointment_GovernedButDueAtNull()
    {
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10, anchorType: SlaAnchorType.AppointmentDate);

        var result = await ResolveAsync("B", new DateTime(2026, 6, 1, 9, 0, 0),
            appointmentDate: null, correlationId: Guid.NewGuid());

        result.Should().NotBeNull("the activity is still governed by the window even when the appointment is unset");
        result!.AnchorType.Should().Be(SlaAnchorType.AppointmentDate);
        result.DueAt.Should().BeNull("appointment-anchored window awaiting an appointment defers the deadline");
    }

    /// <summary>An appointment-anchored window anchors the deadline to the appointment, not assignment.</summary>
    [Fact]
    public async Task ResolveGoverningStage_AppointmentAnchoredWithAppointment_AnchorsToAppointment()
    {
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10, anchorType: SlaAnchorType.AppointmentDate);

        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var appointmentDate = assignedAt.AddDays(3);
        var result = await ResolveAsync("B", assignedAt, appointmentDate: appointmentDate, correlationId: Guid.NewGuid());

        result!.DueAt.Should().Be(appointmentDate.AddHours(10));
        result.StartAt.Should().Be(appointmentDate,
            "the window's at-risk clock starts at the appointment for appointment-anchored windows");
    }

    /// <summary>
    /// All members of a window share ONE fixed deadline = start-entry + full budget. A member assigned
    /// later (after the start activity has completed) gets the same window close, NOT a fresh window from
    /// "now", and consumed start-leg time is NOT subtracted (that would double-count the window open).
    /// </summary>
    [Fact]
    public async Task ResolveGoverningStage_AnchorsToStartEntry_SharedFixedDeadline()
    {
        var correlationId = Guid.NewGuid();
        await SeedStagePolicyAsync("A", "C", new[] { "B" }, durationHours: 10, anchorType: SlaAnchorType.Assignment);

        // Start activity A entered (and completed) at T0 — the window opened then.
        var startEntry = new DateTime(2026, 5, 1, 9, 0, 0);
        await SeedCompletedTaskAsync(correlationId, "A", startEntry, startEntry.AddHours(2));

        // Member B is assigned much later, but the window deadline is the FIXED close anchored at T0.
        var assignedAt = new DateTime(2026, 6, 1, 9, 0, 0);
        var result = await ResolveAsync("B", assignedAt, correlationId: correlationId);

        // DueAt = T0 + 10h (full budget) — NOT restarted from B's assignment, and NOT pulled earlier by
        // subtracting A's 2h consumption (that double-count was the HIGH-1 bug).
        result!.DueAt.Should().Be(startEntry.AddHours(10));
        result.DueAt.Should().NotBe(assignedAt.AddHours(10),
            "the window does not restart from the current assignment");
        result.DueAt.Should().NotBe(startEntry.AddHours(8),
            "consumed start-leg time must not be subtracted on top of anchoring at the window open");

        // The at-risk clock-start is the shared window open (start-entry), not B's late assignment.
        result.StartAt.Should().Be(startEntry);

        // The start member itself resolves to the very same shared deadline.
        var startMember = await ResolveAsync("A", startEntry, correlationId: correlationId);
        startMember!.DueAt.Should().Be(result.DueAt, "every member of a window shares one deadline");
    }
}
