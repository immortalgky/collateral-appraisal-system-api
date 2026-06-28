using System.Data;
using FluentAssertions;
using NSubstitute;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Tests.Infrastructure;
using Workflow.Contracts.Sla;

namespace Reporting.Tests;

/// <summary>
/// Unit tests for <see cref="PartySlaEvaluator"/>.
///
/// SQL is not executed here. Instead, FakeSqlConnectionFactory serves pre-built DataTables
/// in the same order the production code issues queries:
///   1. legs DataTable        (workflow.CompletedTasks)
///   2. appointment date      (appraisal.Appointments — used by H1 vendor-cycle clip)
///   3. vendor budget         (workflow.SlaPolicies, StartActivityKey = ext-appraisal-execution)
///   4. bank budget           (workflow.SlaPolicies, StartActivityKey = int-appraisal-execution)
///
/// IBusinessTimeCalculator is mocked to return (int)(to - from).TotalMinutes so that
/// expected values can be computed from the timestamps used in each scenario.
///
/// NOTE: EvaluateAsync does NOT filter "Reassigned" legs in C# — it relies entirely on the
/// SQL WHERE clause (ActionTaken &lt;&gt; 'Reassigned'). The FakeSqlConnectionFactory does not
/// apply that filter, so tests must only include non-Reassigned rows in the legs DataTable
/// to simulate what the real database returns.
/// </summary>
public class PartySlaEvaluatorTests
{
    private readonly FakeSqlConnectionFactory _factory = new();
    private readonly IBusinessTimeCalculator _businessTime = Substitute.For<IBusinessTimeCalculator>();
    private readonly PartySlaEvaluator _evaluator;

    // Stub: return the actual calendar minute difference so expected values are calculable.
    private static readonly DateTime BaseTime = new(2026, 7, 1, 8, 0, 0);

    public PartySlaEvaluatorTests()
    {
        // Stub: return the real calendar-minute difference so test expectations stay deterministic
        // without hard-coding business-hours rules.
        _businessTime
            .GetBusinessMinutesBetweenAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var from = (DateTime)callInfo[0];
                var to   = (DateTime)callInfo[1];
                return Task.FromResult((int)(to - from).TotalMinutes);
            });

        _evaluator = new PartySlaEvaluator(_factory, _businessTime);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Build a DataTable with the same columns the SQL returns for workflow.CompletedTasks.
    /// Rows that would be excluded by the SQL WHERE (e.g. Reassigned) must NOT appear here.
    /// </summary>
    private static DataTable BuildLegsTable(
        IEnumerable<(string? ActivityId, DateTime AssignedAt, DateTime CompletedAt, string? ActionTaken)> legs)
    {
        var dt = new DataTable();
        dt.Columns.Add("ActivityId",   typeof(string));
        dt.Columns.Add("AssignedAt",   typeof(DateTime));
        dt.Columns.Add("CompletedAt",  typeof(DateTime));
        dt.Columns.Add("ActionTaken",  typeof(string));

        foreach (var (actId, at, ct, action) in legs)
        {
            var row = dt.NewRow();
            row["ActivityId"]  = (object?)actId  ?? DBNull.Value;
            row["AssignedAt"]  = at;
            row["CompletedAt"] = ct;
            row["ActionTaken"] = (object?)action ?? DBNull.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>
    /// Build a budget DataTable. When <paramref name="durationHours"/> is null the table has
    /// no rows, so Dapper returns an empty IEnumerable&lt;int&gt;, FirstOrDefault() returns 0,
    /// and the evaluator treats 0 as "no budget configured".
    /// </summary>
    private static DataTable BuildBudgetTable(int? durationHours)
    {
        var dt = new DataTable();
        dt.Columns.Add("DurationHours", typeof(int));

        if (durationHours.HasValue)
        {
            var row = dt.NewRow();
            row["DurationHours"] = durationHours.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>
    /// Build the appointment DataTable returned by GetLatestNonCancelledAppointmentDateAsync.
    /// Pass null to simulate "no appointment confirmed yet" (empty result → null in C#).
    /// </summary>
    private static DataTable BuildAppointmentTable(DateTime? appointmentDateTime)
    {
        var dt = new DataTable();
        dt.Columns.Add("AppointmentDateTime", typeof(DateTime));

        if (appointmentDateTime.HasValue)
        {
            var row = dt.NewRow();
            row["AppointmentDateTime"] = appointmentDateTime.Value;
            dt.Rows.Add(row);
        }

        return dt;
    }

    /// <summary>
    /// Enqueue the standard 4-table sequence expected by a non-empty EvaluateAsync call:
    /// legs → appointment date (H1 clip source) → vendor budget → bank budget.
    /// Pass <paramref name="appointmentDate"/> = null to simulate no confirmed appointment.
    /// </summary>
    private void EnqueueAll(DataTable legs, DateTime? appointmentDate = null, int? vendorHours = null, int? bankHours = null)
    {
        _factory.EnqueueResult(legs);
        _factory.EnqueueResult(BuildAppointmentTable(appointmentDate));
        _factory.EnqueueResult(BuildBudgetTable(vendorHours));
        _factory.EnqueueResult(BuildBudgetTable(bankHours));
    }

    // ── Test 1: Vendor → Bank → Vendor creates 2 vendor cycles + 1 bank cycle ────────────────

    /// <summary>
    /// Three legs in Vendor → Bank → Vendor order.
    /// The evaluator must detect 2 vendor cycles (index 0 = original, index 1 = rework)
    /// and 1 bank cycle.
    /// </summary>
    [Fact]
    public async Task VendorBankVendor_ProducesTwoVendorCyclesAndOneBankCycle()
    {
        var t = BaseTime;

        var legs = new[]
        {
            // Leg 1 – Vendor (starts with "ext-appraisal")
            ("ext-appraisal-assignment",   t,           t.AddHours(1), (string?)null),
            // Leg 2 – Bank (no "ext-appraisal" prefix)
            ("appraisal-book-verification", t.AddHours(1), t.AddHours(2), (string?)null),
            // Leg 3 – Vendor again (rework cycle)
            ("ext-appraisal-execution",    t.AddHours(2), t.AddHours(4), (string?)null),
        };

        EnqueueAll(BuildLegsTable(legs));

        var result = await _evaluator.EvaluateAsync(
            correlationId: Guid.NewGuid(),
            workflowDefinitionId: null,
            companyId: null,
            loanType: null,
            appraisalType: null);

        result.Should().NotBeNull();
        result!.Vendor.Cycles.Should().HaveCount(2, "vendor appears twice separated by a bank leg");
        result.Bank.Cycles.Should().HaveCount(1, "bank appears once");
    }

    // ── Test 2: IsRework flag is set correctly ──────────────────────────────────────────────────

    /// <summary>
    /// The first vendor cycle has IsRework=false; the second (returned-to-vendor) cycle has
    /// IsRework=true because cycleIndex > 0.
    /// </summary>
    [Fact]
    public async Task VendorBankVendor_SecondVendorCycleIsMarkedAsRework()
    {
        var t = BaseTime;

        var legs = new[]
        {
            ("ext-appraisal-assignment",    t,             t.AddHours(1), (string?)null),
            ("appraisal-book-verification", t.AddHours(1), t.AddHours(2), (string?)null),
            ("ext-appraisal-execution",     t.AddHours(2), t.AddHours(4), (string?)null),
        };

        EnqueueAll(BuildLegsTable(legs));

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result!.Vendor.Cycles[0].IsRework.Should().BeFalse("index 0 is the original vendor pass");
        result!.Vendor.Cycles[1].IsRework.Should().BeTrue("index 1 is a vendor rework cycle");
        result!.Bank.Cycles[0].IsRework.Should().BeFalse("bank has only one cycle");
    }

    // ── Test 3: All-vendor legs form one cycle, bank is empty ───────────────────────────────────

    /// <summary>
    /// When all legs belong to vendor activities and there is no party switch, the evaluator
    /// must group them into exactly one vendor cycle and produce an empty bank party.
    /// </summary>
    [Fact]
    public async Task AllVendorLegs_ProduceSingleVendorCycleWithNoRework_AndEmptyBank()
    {
        var t = BaseTime;

        var legs = new[]
        {
            ("ext-appraisal-assignment",  t,             t.AddHours(1), (string?)null),
            ("ext-appraisal-execution",   t.AddHours(1), t.AddHours(3), (string?)null),
            ("ext-appraisal-submission",  t.AddHours(3), t.AddHours(4), (string?)null),
        };

        EnqueueAll(BuildLegsTable(legs));

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().NotBeNull();
        result!.Vendor.Cycles.Should().HaveCount(1, "all legs are consecutive vendor legs → single cycle");
        result!.Vendor.Cycles[0].IsRework.Should().BeFalse("only one vendor cycle — no rework");
        result!.Bank.Cycles.Should().BeEmpty("no bank legs present");
    }

    // ── Test 4: Empty legs → returns null ───────────────────────────────────────────────────────

    /// <summary>
    /// When the legs query returns no rows (workflow not started yet), EvaluateAsync must
    /// return null immediately and must NOT make the two budget queries.
    /// </summary>
    [Fact]
    public async Task NoLegs_ReturnsNull()
    {
        // Enqueue only the legs table (empty). The budget tables are NOT queued because
        // EvaluateAsync returns early when legs.Count == 0 — before reaching the budget queries.
        _factory.EnqueueResult(BuildLegsTable([]));

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().BeNull("no completed tasks means no SLA data to evaluate");
    }

    // ── Test 5: TotalBusinessMinutes equals the sum of per-cycle minutes ────────────────────────

    /// <summary>
    /// The Vendor party's TotalBusinessMinutes must equal the sum of its individual cycle minutes.
    /// This validates that BuildPartyAsync accumulates totalMinutes correctly.
    /// </summary>
    [Fact]
    public async Task VendorTotalMinutes_EqualsSum_OfAllCycleMinutes()
    {
        var t = BaseTime;

        // Vendor cycle 1: 60 calendar minutes (= 60 business minutes via stub)
        // Bank cycle:     60 calendar minutes
        // Vendor cycle 2: 120 calendar minutes (= 120 business minutes via stub)
        var legs = new[]
        {
            ("ext-appraisal-assignment",    t,             t.AddHours(1), (string?)null),
            ("appraisal-book-verification", t.AddHours(1), t.AddHours(2), (string?)null),
            ("ext-appraisal-execution",     t.AddHours(2), t.AddHours(4), (string?)null),
        };

        EnqueueAll(BuildLegsTable(legs));

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().NotBeNull();

        var cycleSum = result!.Vendor.Cycles.Sum(c => c.BusinessMinutes);
        result!.Vendor.TotalBusinessMinutes.Should().Be(cycleSum,
            "TotalBusinessMinutes must be the running sum of all cycle BusinessMinutes");

        // Absolute values: cycle1=60, cycle2=120 → total=180
        result!.Vendor.TotalBusinessMinutes.Should().Be(180m,
            "60 min first cycle + 120 min rework cycle = 180 total");
    }

    // ── Test 6: Budget is converted from hours to minutes ───────────────────────────────────────

    /// <summary>
    /// M1: Each cycle carries the FULL budget (not an even-split slice), and BudgetMet
    /// compares per-cycle minutes against that full budget.  CumulativeBudgetMet is the
    /// cumulative guard that fires when the sum of all cycles exceeds the total budget.
    ///
    /// With 2 vendor cycles and a 480-minute total budget:
    ///   Cycle 1 (60 min) ≤ 480 → BudgetMet=true.
    ///   Cycle 2 (120 min) ≤ 480 → BudgetMet=true.
    ///   CumulativeBudgetMet = true because 180 ≤ 480.
    ///   PartySlaParty.BudgetMinutes = 480 (the overall stage budget).
    ///   PartyCycle.BudgetMinutes   = 480 (the full budget each cycle is measured against).
    /// </summary>
    [Fact]
    public async Task VendorBudget_ConvertedFromHoursToMinutes_AndCheckedPerCycle()
    {
        var t = BaseTime;

        var legs = new[]
        {
            ("ext-appraisal-assignment",    t,             t.AddHours(1), (string?)null),
            ("appraisal-book-verification", t.AddHours(1), t.AddHours(2), (string?)null),
            ("ext-appraisal-execution",     t.AddHours(2), t.AddHours(4), (string?)null),
        };

        // Vendor budget = 8 hours = 480 minutes; no bank budget.
        EnqueueAll(BuildLegsTable(legs), vendorHours: 8, bankHours: null);

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().NotBeNull();

        // M1: each cycle carries the full 480-minute budget (not an even-split slice).
        result!.Vendor.BudgetMinutes.Should().Be(480m, "8 hours × 60 = 480 minutes overall");
        result!.Vendor.Cycles.Should().AllSatisfy(c =>
            c.BudgetMinutes.Should().Be(480m, "each cycle is measured against the FULL budget, not budget/n"));

        // Both cycles are within the 480-min budget.
        result!.Vendor.Cycles.Should().AllSatisfy(c =>
            c.BudgetMet.Should().BeTrue(
                "cycle 1 uses 60 min and cycle 2 uses 120 min — both ≤ 480-min full budget"));

        result!.Vendor.CumulativeBudgetMet.Should().BeTrue(
            "total 180 min is within the 480-min overall budget");

        // Bank has no budget configured — CumulativeBudgetMet is true by convention when null.
        result!.Bank.BudgetMinutes.Should().BeNull("no bank budget was seeded");
        result!.Bank.CumulativeBudgetMet.Should().BeTrue(
            "null budget = no policy configured → treated as met by convention");
    }

    // ── Test 7 (H1): Vendor cycle start is clipped to appointment date ──────────────────────────

    /// <summary>
    /// H1 fix: when an appointment date exists, the first leg's AssignedAt in each vendor cycle
    /// is clipped to max(AssignedAt, appointmentDate).
    ///
    /// Scenario: vendor leg starts at T+0 and ends at T+4h (240 calendar minutes).
    /// Appointment date = T+1h.  After clipping, cycleStart = T+1h, so the measured business
    /// minutes become (T+4h − T+1h) = 180 minutes, not the raw 240.
    ///
    /// This proves that pre-appointment wait time is excluded from the vendor SLA.
    /// </summary>
    [Fact]
    public async Task VendorCycleStart_IsClippedToAppointmentDate_WhenAppointmentIsAfterAssignment()
    {
        var t = BaseTime;
        var appointmentDate = t.AddHours(1); // 1 hour after vendor was assigned.

        var legs = new[]
        {
            // Without clipping: 4h = 240 min. With clipping to T+1h: 3h = 180 min.
            ("ext-appraisal-assignment", t, t.AddHours(4), (string?)null),
        };

        EnqueueAll(BuildLegsTable(legs), appointmentDate: appointmentDate);

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().NotBeNull();
        result!.Vendor.Cycles.Should().HaveCount(1);
        result!.Vendor.Cycles[0].BusinessMinutes.Should().Be(180m,
            "cycle start is clipped from T+0 to T+1h (appointment date), leaving 3h = 180 min");
        result!.Vendor.TotalBusinessMinutes.Should().Be(180m,
            "total equals the single clipped cycle");
    }

    /// <summary>
    /// H1 non-regression: when no appointment exists, the vendor cycle start is NOT clipped.
    /// The full leg duration (from AssignedAt) is counted.
    /// </summary>
    [Fact]
    public async Task VendorCycleStart_IsNotClipped_WhenNoAppointmentExists()
    {
        var t = BaseTime;

        var legs = new[]
        {
            ("ext-appraisal-assignment", t, t.AddHours(4), (string?)null),
        };

        // appointmentDate = null → no appointment confirmed, no clipping.
        EnqueueAll(BuildLegsTable(legs), appointmentDate: null);

        var result = await _evaluator.EvaluateAsync(
            Guid.NewGuid(), null, null, null, null);

        result.Should().NotBeNull();
        result!.Vendor.Cycles.Should().HaveCount(1);
        result!.Vendor.Cycles[0].BusinessMinutes.Should().Be(240m,
            "no appointment → no clipping → full 4h = 240 min counted");
    }
}
