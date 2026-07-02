using FluentAssertions;
using NSubstitute;
using Workflow.Contracts.Sla;

namespace Workflow.Tests.Sla;

/// <summary>
/// Unit tests for <see cref="BusinessTimeCalculatorExtensions.ComputeElapsedRemainingHoursAsync"/>'s
/// clock-start clamp (PART 5 / Fix A): "Remaining" must never exceed the configured budget, even when
/// the deadline is anchored ahead of now (a future appointment).
/// </summary>
public class BusinessTimeRemainingTests
{
    [Fact]
    public async Task Remaining_ClampsNowToClockStart_WhenAppointmentIsInFuture()
    {
        var btc = Substitute.For<IBusinessTimeCalculator>();
        var now = new DateTime(2026, 6, 1, 9, 0, 0);
        var clockStart = now.AddDays(2);   // appointment 2 days out — clock hasn't started yet
        var due = clockStart.AddHours(8);  // budget = 8 business hours measured from the appointment

        // Only the clamped range (clockStart → due) should be queried; it returns the full 8h budget.
        btc.GetBusinessMinutesBetweenAsync(clockStart, due, Arg.Any<CancellationToken>()).Returns(480);

        var (_, remaining) = await btc.ComputeElapsedRemainingHoursAsync(
            now, start: now, due: due, clockStart: clockStart);

        remaining.Should().Be(8, "before the appointment, Remaining is the full budget, not wait + budget");
        // It must measure from the clamped clock-start, never from `now`.
        await btc.DidNotReceive().GetBusinessMinutesBetweenAsync(now, due, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remaining_DoesNotClamp_WhenNowIsAfterClockStart()
    {
        var btc = Substitute.For<IBusinessTimeCalculator>();
        var clockStart = new DateTime(2026, 6, 1, 9, 0, 0);
        var now = clockStart.AddHours(2);  // clock already running
        var due = clockStart.AddHours(8);

        btc.GetBusinessMinutesBetweenAsync(now, due, Arg.Any<CancellationToken>()).Returns(360); // 6h left

        var (_, remaining) = await btc.ComputeElapsedRemainingHoursAsync(
            now, start: clockStart, due: due, clockStart: clockStart);

        remaining.Should().Be(6, "once the clock has started, Remaining counts down from now");
    }

    [Fact]
    public async Task Remaining_NullClockStart_PreservesLegacyBehaviour()
    {
        var btc = Substitute.For<IBusinessTimeCalculator>();
        var now = new DateTime(2026, 6, 1, 9, 0, 0);
        var due = now.AddHours(40);

        btc.GetBusinessMinutesBetweenAsync(now, due, Arg.Any<CancellationToken>()).Returns(600); // 10h

        var (_, remaining) = await btc.ComputeElapsedRemainingHoursAsync(
            now, start: now, due: due, clockStart: null);

        remaining.Should().Be(10, "with no clock-start, Remaining is measured straight from now");
    }

    [Fact]
    public async Task Elapsed_IsZero_WhenClockStartIsInFuture()
    {
        // PART 6 / E1: the task handlers pass `start = SlaStartAt`, so for an appointment-anchored task
        // assigned before the visit, Elapsed is 0 until the appointment (and Elapsed + Remaining = budget).
        var btc = Substitute.For<IBusinessTimeCalculator>();
        var now = new DateTime(2026, 6, 1, 9, 0, 0);
        var clockStart = now.AddDays(2);          // SlaStartAt (the appointment) is in the future
        var due = clockStart.AddHours(8);

        // Elapsed = businessMinutes(start → now); start is after now, so the calculator returns 0.
        btc.GetBusinessMinutesBetweenAsync(clockStart, now, Arg.Any<CancellationToken>()).Returns(0);
        btc.GetBusinessMinutesBetweenAsync(clockStart, due, Arg.Any<CancellationToken>()).Returns(480);

        var (elapsed, remaining) = await btc.ComputeElapsedRemainingHoursAsync(
            now, start: clockStart, due: due, clockStart: clockStart);

        elapsed.Should().Be(0, "before the clock-start, no business time has elapsed");
        remaining.Should().Be(8, "the full budget remains until the appointment");
    }
}
