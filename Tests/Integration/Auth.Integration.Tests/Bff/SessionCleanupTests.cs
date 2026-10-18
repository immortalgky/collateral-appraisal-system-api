using Auth.Domain.Sessions;
using Auth.Infrastructure;
using Auth.Scheduling;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Scheduling;
using Shared.Time;

namespace Integration.Auth.Integration.Tests.Bff;

[Collection("Integration")]
public class SessionCleanupTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.BffWebApplicationFactory.Services;

    private static UserSession Row(Guid userId, DateTime lastActivity, DateTime? endedAt) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Ticket = [1],
        CreatedAt = lastActivity,
        LastActivityAt = lastActivity,
        EndedAt = endedAt,
        EndReason = endedAt is null ? null : SessionEndReason.Logout
    };

    [Fact]
    public async Task Purge_removes_sessions_ninety_days_after_they_ended_and_keeps_the_rest()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var userId = Guid.NewGuid();
        var oldEnded = Row(userId, now.AddDays(-200), now.AddDays(-100));
        var oldAbandoned = Row(userId, now.AddDays(-100), endedAt: null); // never explicitly ended
        var recentlyEnded = Row(userId, now.AddDays(-12), now.AddDays(-10));
        var live = Row(userId, now.AddMinutes(-5), endedAt: null);
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.UserSessions.AddRange(oldEnded, oldAbandoned, recentlyEnded, live);
            await db.SaveChangesAsync();
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            // The job is internal to the Auth module; build it the way Hangfire does, from the container.
            var jobType = typeof(AuthRecurringJobs).Assembly.GetType("Auth.Scheduling.UserSessionCleanupJob")!;
            var job = ActivatorUtilities.CreateInstance(scope.ServiceProvider, jobType);
            await (Task)jobType.GetMethod("ExecuteAsync")!.Invoke(job, [CancellationToken.None])!;
        }

        await using var check = Services.CreateAsyncScope();
        var remaining = await check.ServiceProvider.GetRequiredService<AuthDbContext>().UserSessions
            .AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Id).ToListAsync();
        remaining.Should().BeEquivalentTo([recentlyEnded.Id, live.Id]);
    }

    [Fact]
    public async Task The_purge_job_is_scheduled_in_the_auth_job_table()
    {
        await using var scope = Services.CreateAsyncScope();
        var schedule = await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Set<JobSchedule>()
            .AsNoTracking().SingleAsync(s => s.JobId == "user-session-cleanup");

        schedule.IsEnabled.Should().BeTrue();
        schedule.CronExpression.Should().Be("0 3 * * *");
    }
}
