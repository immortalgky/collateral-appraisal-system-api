using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Workflow.Data;
using Workflow.Sla.Models;
using Workflow.Sla.Services;

namespace Workflow.Tests.Sla;

/// <summary>
/// Tests for BusinessTimeCalculator lunch-break two-session logic.
/// All times use Asia/Bangkok (UTC+7) — stored in config; input datetimes are Unspecified
/// and treated as wall-clock time in the configured timezone.
/// </summary>
public class BusinessTimeCalculatorLunchTests : IDisposable
{
    private readonly WorkflowDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly BusinessTimeCalculator _calculator;

    public BusinessTimeCalculatorLunchTests()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WorkflowDbContext(options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _calculator = new BusinessTimeCalculator(_db, _cache);
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private async Task SeedBusinessHours(bool includeLunch)
    {
        BusinessHoursConfig config;
        if (includeLunch)
        {
            config = BusinessHoursConfig.Create(
                startTime: new TimeOnly(8, 30),
                endTime: new TimeOnly(17, 30),
                timeZone: "Asia/Bangkok",
                lunchStartTime: new TimeOnly(12, 0),
                lunchEndTime: new TimeOnly(13, 0));
        }
        else
        {
            config = BusinessHoursConfig.Create(
                startTime: new TimeOnly(8, 30),
                endTime: new TimeOnly(17, 30),
                timeZone: "Asia/Bangkok");
        }

        _db.BusinessHoursConfigs.Add(config);
        await _db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 10: Cross-day lunch-aware span
    // BH config: 08:30-17:30 lunch 12:00-13:00 (8h/day), Asia/Bangkok
    // startedAt = 2026-05-15 14:00 (Friday), durationHours = 16
    // Expected: 2026-05-19 14:00 (Tuesday)
    // Trace: Fri 14:00→17:30 (3.5h) | Mon 08:30→12:00 (3.5h, 7h) | Mon 13:00→17:30 (4.5h, 11.5h) | Tue 08:30→12:00 (3.5h, 15h) | Tue 13:00→14:00 (1h, 16h)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_LunchAware_CrossDaySpan_CorrectResult()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Unspecified); // Friday 14:00
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 16);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        localResult.Year.Should().Be(2026);
        localResult.Month.Should().Be(5);
        localResult.Day.Should().Be(19);    // Tuesday
        localResult.Hour.Should().Be(14);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // Test 11: Cursor inside lunch — snap forward then consume
    // startedAt = 2026-05-15 12:30 (Friday, inside 12:00-13:00 lunch), durationHours = 1
    // Expected: cursor snaps to 13:00, consumes 1h → 14:00 same day
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_CursorInsideLunch_SnapsForwardAndConsumes()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 12, 30, 0, DateTimeKind.Unspecified); // inside lunch
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 1);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        localResult.Day.Should().Be(15);    // same Friday
        localResult.Hour.Should().Be(14);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // Test 12: Null lunch fields — backward-compatible single-window behavior
    // Config: 08:30-17:30 no lunch. Same as original calculator.
    // startedAt = 2026-05-15 14:00 (Friday), durationHours = 16
    // Expected: Mon 14:00 + remainder...
    //   Fri: 14:00→17:30 = 3.5h, remaining 12.5h
    //   Mon: 08:30→17:30 = 9h, consume 9h, remaining 3.5h
    //   Tue: 08:30→12:00 = 3.5h done → 2026-05-19 12:00
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_NullLunch_SingleWindowBehaviorUnchanged()
    {
        await SeedBusinessHours(includeLunch: false);

        var startedAt = new DateTime(2026, 5, 15, 14, 0, 0, DateTimeKind.Unspecified); // Friday 14:00
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 16);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        // Without lunch: 3.5h Fri + 9h Mon + 3.5h Tue = 16h → Tue 12:00
        localResult.Day.Should().Be(19);    // Tuesday
        localResult.Hour.Should().Be(12);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // Test 13: Cross-session boundary within same day
    // startedAt = 2026-05-15 11:00, durationHours = 2
    // Consume 1h to 12:00 (lunch start) → snap to 13:00 → consume 1h to 14:00
    // Expected: 2026-05-15 14:00
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_CrossSessionBoundary_SkipsLunchCorrectly()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 11, 0, 0, DateTimeKind.Unspecified); // Friday 11:00
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 2);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        localResult.Day.Should().Be(15);    // same Friday
        localResult.Hour.Should().Be(14);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // Test: Whole 8h business day with lunch = 8 actual working hours
    // startedAt = 2026-05-15 08:30 (Friday), durationHours = 8
    // Sessions: 08:30-12:00 (3.5h) + 13:00-17:30 (4.5h) = 8h
    // Expected: 2026-05-15 17:30 same day
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_FullDayWith8Hours_EndsAtDayEnd()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 8, 30, 0, DateTimeKind.Unspecified); // Friday 08:30
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 8);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        localResult.Day.Should().Be(15);    // same Friday
        localResult.Hour.Should().Be(17);
        localResult.Minute.Should().Be(30);
    }

    // ---------------------------------------------------------------------------
    // Validation tests for BusinessHoursConfig factory
    // ---------------------------------------------------------------------------

    [Fact]
    public void BusinessHoursConfig_Create_BothLunchFieldsNull_Valid()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok");
        act.Should().NotThrow();
    }

    [Fact]
    public void BusinessHoursConfig_Create_BothLunchFieldsSet_Valid()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok",
            new TimeOnly(12, 0), new TimeOnly(13, 0));
        act.Should().NotThrow();
    }

    [Fact]
    public void BusinessHoursConfig_Create_OnlyLunchStart_ThrowsArgumentException()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok",
            lunchStartTime: new TimeOnly(12, 0), lunchEndTime: null);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*both*");
    }

    [Fact]
    public void BusinessHoursConfig_Create_LunchStartAfterLunchEnd_ThrowsArgumentException()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok",
            lunchStartTime: new TimeOnly(13, 0), lunchEndTime: new TimeOnly(12, 0));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*LunchStartTime must be earlier*");
    }

    [Fact]
    public void BusinessHoursConfig_Create_LunchOutsideBusinessHours_ThrowsArgumentException()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok",
            lunchStartTime: new TimeOnly(17, 0), lunchEndTime: new TimeOnly(18, 0));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*within the business hours window*");
    }

    // ---------------------------------------------------------------------------
    // S4 edge-case tests
    // ---------------------------------------------------------------------------

    // S4-1: Cursor at exactly EndTime (17:30) — must roll to next business day
    // startedAt = Fri 17:30, durationHours = 1
    // Expected: Mon 09:30 (cursor snaps past dayEnd, moves to Mon 08:30, consumes 1h → 09:30)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_CursorAtExactlyEndTime_RollsToNextDay()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 17, 30, 0, DateTimeKind.Unspecified); // Friday 17:30
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 1);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        // Rolls to Monday, consumes 1h from 08:30 → 09:30
        localResult.Day.Should().Be(18);    // Monday 2026-05-18
        localResult.Hour.Should().Be(9);
        localResult.Minute.Should().Be(30);
    }

    // ---------------------------------------------------------------------------
    // S4-2: Cursor at exactly LunchStartTime (12:00) — must skip lunch
    // startedAt = Fri 12:00, durationHours = 1
    // Expected: Fri 14:00 (snap to 13:00, consume 1h → 14:00)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_CursorAtExactlyLunchStart_SnapsAndConsumes()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Unspecified); // Friday 12:00
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 1);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        // 12:00 is lunchStart boundary — must skip to 13:00, then consume 1h → 14:00
        localResult.Day.Should().Be(15);    // same Friday
        localResult.Hour.Should().Be(14);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // S4-3: Cursor at exactly LunchEndTime (13:00) — no snap needed, consume normally
    // startedAt = Fri 13:00, durationHours = 1
    // Expected: Fri 14:00
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBusinessHoursAsync_CursorAtExactlyLunchEnd_ConsumesNormally()
    {
        await SeedBusinessHours(includeLunch: true);

        var startedAt = new DateTime(2026, 5, 15, 13, 0, 0, DateTimeKind.Unspecified); // Friday 13:00
        var result = await _calculator.AddBusinessHoursAsync(startedAt, 1);

        var localResult = result; // calculator returns Bangkok-local wall-clock directly

        localResult.Day.Should().Be(15);    // same Friday
        localResult.Hour.Should().Be(14);
        localResult.Minute.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // S4-4: Zero-length lunch (lunchStart == lunchEnd) is rejected at Create time
    // The validator uses >= so equality must throw ArgumentException
    // ---------------------------------------------------------------------------

    [Fact]
    public void BusinessHoursConfig_Create_ZeroLengthLunch_ThrowsArgumentException()
    {
        var act = () => BusinessHoursConfig.Create(
            new TimeOnly(8, 30), new TimeOnly(17, 30), "Asia/Bangkok",
            lunchStartTime: new TimeOnly(12, 0), lunchEndTime: new TimeOnly(12, 0));
        act.Should().Throw<ArgumentException>()
            .WithMessage("*LunchStartTime must be earlier*");
    }
}
