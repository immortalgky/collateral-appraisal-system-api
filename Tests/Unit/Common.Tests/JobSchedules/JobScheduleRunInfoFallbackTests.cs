using Common.Application.Features.JobSchedules;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Scheduling;
using Shared.Time;

namespace Common.Tests.JobSchedules;

/// <summary>
/// Hangfire run info (last/next run) is decoration on the schedule list. A storage failure must neither fail
/// the list nor turn an already-saved PUT into a 500: the DTO goes out with null run fields.
/// </summary>
public class JobScheduleRunInfoFallbackTests
{
    private const string JobId = "job-a";
    private const string Cron = "0 * * * *";

    private readonly JobScheduleRegistry _registry = new();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<JobScheduleEndpoints> _logger = Substitute.For<ILogger<JobScheduleEndpoints>>();
    private readonly JobStorage _brokenStorage = Substitute.For<JobStorage>();

    public JobScheduleRunInfoFallbackTests()
    {
        _clock.ApplicationTimeZone.Returns(TimeZoneInfo.Utc);
        _brokenStorage.GetConnection().Returns(_ => throw new InvalidOperationException("storage is down"));

        _registry.Add(new JobScheduleRegistration
        {
            JobId = JobId,
            Module = "Test",
            Definition = new RecurringJobDefinition(JobId, Cron, "test job", (_, _, _) => { }),
            LoadAsync = (_, _) => Task.FromResult<JobSchedule?>(JobSchedule.Create(JobId, Cron)),
            UpdateAsync = (_, mutate, _) =>
            {
                var row = JobSchedule.Create(JobId, Cron);
                mutate(row);
                return Task.FromResult<JobSchedule?>(row);
            }
        });
    }

    private static void AssertNoRunInfo(JobScheduleDto dto)
    {
        dto.JobId.Should().Be(JobId);
        dto.LastExecution.Should().BeNull();
        dto.NextExecution.Should().BeNull();
        dto.LastJobState.Should().BeNull();
        dto.LastDurationMs.Should().BeNull();
        dto.RunInfoAvailable.Should().BeFalse();
    }

    private bool LoggedWarning() => _logger.ReceivedCalls().Any(c =>
        c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning);

    [Fact]
    public async Task List_WhenHangfireStorageThrows_Returns200WithNullRunFields()
    {
        var result = await JobScheduleEndpoints.List(
            _registry, Substitute.For<IServiceProvider>(), _brokenStorage, _clock, _logger, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<List<JobScheduleDto>>>().Subject;
        AssertNoRunInfo(ok.Value!.Should().ContainSingle().Subject);
        LoggedWarning().Should().BeTrue();
    }

    [Fact]
    public async Task Update_WhenHangfireStorageThrowsAfterTheSave_Returns200WithNullRunFields()
    {
        var result = await JobScheduleEndpoints.Update(
            JobId,
            new UpdateJobScheduleRequest("30 * * * *", null, IsEnabled: true),
            _registry,
            Substitute.For<IRecurringJobManager>(),
            Substitute.For<IServiceProvider>(),
            _brokenStorage,
            _clock,
            _logger,
            CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<JobScheduleDto>>().Subject;
        AssertNoRunInfo(ok.Value!);
        ok.Value!.EffectiveCron.Should().Be("30 * * * *", "the update was applied before run info was read");
        LoggedWarning().Should().BeTrue();
    }

    [Fact]
    public async Task List_WhenHangfireIsReadable_ButHoldsNoRecord_RunInfoIsAvailableWithNullFields()
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(Substitute.For<Hangfire.Storage.IStorageConnection>());

        var result = await JobScheduleEndpoints.List(
            _registry, Substitute.For<IServiceProvider>(), storage, _clock, _logger, CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<List<JobScheduleDto>>>().Subject.Value!.Should().ContainSingle().Subject;
        dto.RunInfoAvailable.Should().BeTrue();
        dto.LastExecution.Should().BeNull();
        dto.LastJobState.Should().BeNull();
        LoggedWarning().Should().BeFalse();
    }

    [Fact]
    public async Task List_WithAHangfireRecord_ConvertsLastAndNextToAppTime_AndParsesTheDuration()
    {
        var lastUtc = new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc);
        var nextUtc = new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc);
        _clock.ToApplicationTime(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>().AddHours(7));

        var connection = Substitute.For<Hangfire.Storage.IStorageConnection>();
        connection.GetAllEntriesFromHash($"recurring-job:{JobId}").Returns(new Dictionary<string, string>
        {
            ["Cron"] = Cron,
            ["TimeZoneId"] = "UTC",
            ["LastJobId"] = "42",
            ["LastExecution"] = JobHelper.SerializeDateTime(lastUtc),
            ["NextExecution"] = JobHelper.SerializeDateTime(nextUtc)
        });
        connection.GetJobData("42").Returns(new Hangfire.Storage.JobData { State = "Succeeded" });
        connection.GetStateData("42").Returns(new Hangfire.Storage.StateData
        {
            Name = "Succeeded",
            Data = new Dictionary<string, string> { ["PerformanceDuration"] = "1234" }
        });
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(connection);

        var result = await JobScheduleEndpoints.List(
            _registry, Substitute.For<IServiceProvider>(), storage, _clock, _logger, CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<List<JobScheduleDto>>>().Subject.Value!.Should().ContainSingle().Subject;
        dto.RunInfoAvailable.Should().BeTrue();
        dto.LastExecution.Should().Be(lastUtc.AddHours(7));
        dto.NextExecution.Should().Be(nextUtc.AddHours(7));
        dto.LastJobState.Should().Be("Succeeded");
        dto.LastDurationMs.Should().Be(1234);
    }

    [Fact]
    public async Task List_WhenOneJobsStateRecordIsCorrupt_ThatJobOnlyLosesItsDuration()
    {
        var lastUtc = new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc);
        var connection = Substitute.For<Hangfire.Storage.IStorageConnection>();
        connection.GetAllEntriesFromHash($"recurring-job:{JobId}").Returns(new Dictionary<string, string>
        {
            ["Cron"] = Cron,
            ["TimeZoneId"] = "UTC",
            ["LastJobId"] = "42",
            ["LastExecution"] = JobHelper.SerializeDateTime(lastUtc)
        });
        connection.GetJobData("42").Returns(new Hangfire.Storage.JobData { State = "Succeeded" });
        // Hangfire's own GetRecurringJobs reads the state once (for LastJobState); the duration lookup is the second read.
        connection.GetStateData("42").Returns(
            _ => new Hangfire.Storage.StateData { Name = "Succeeded", Data = new Dictionary<string, string>() },
            _ => throw new InvalidOperationException("corrupt state"));
        var storage = Substitute.For<JobStorage>();
        storage.GetConnection().Returns(connection);
        _clock.ToApplicationTime(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>());

        var result = await JobScheduleEndpoints.List(
            _registry, Substitute.For<IServiceProvider>(), storage, _clock, _logger, CancellationToken.None);

        var dto = result.Should().BeOfType<Ok<List<JobScheduleDto>>>().Subject.Value!.Should().ContainSingle().Subject;
        dto.RunInfoAvailable.Should().BeTrue();
        dto.LastExecution.Should().Be(lastUtc);
        dto.LastJobState.Should().Be("Succeeded");
        dto.LastDurationMs.Should().BeNull();
    }
}
