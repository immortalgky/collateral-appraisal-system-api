using FluentAssertions;
using Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Data;
using Notification.Domain.Email;
using Notification.Domain.Notifications.Models;
using Notification.Scheduling;
using Shared.Data.Outbox;
using Shared.Time;

namespace Integration.Notification;

/// <summary>
/// Runs <c>NotificationCleanupJob</c> and <c>OutboxCleanupJob&lt;NotificationDbContext&gt;</c> (an inbox-only
/// context) against the Testcontainers database. Every row carries a unique marker and the assertions look
/// only at those rows; the jobs themselves purge every qualifying old row in the shared database.
/// </summary>
[Collection("Integration")]
public class NotificationCleanupTests(IntegrationTestFixture fixture)
{
    private IServiceProvider Services => fixture.IntegrationTestWebApplicationFactory.Services;

    private static UserNotification Notification(string username, DateTime createdAt, bool isRead) => new()
    {
        Id = Guid.CreateVersion7(),
        Username = username,
        Title = "t",
        Message = "m",
        CreatedAt = createdAt,
        IsRead = isRead
    };

    private static EmailSendLog Email(string marker, DateTime createdAt) =>
        EmailSendLog.Create("Sent", "from@test", marker, 1, 0, createdAt);

    [Fact]
    public async Task Cleanup_deletes_read_after_30_days_and_everything_after_90_days_and_keeps_the_rest()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var marker = $"cleanup-{Guid.NewGuid():N}";
        var read31 = Notification(marker, now.AddDays(-31), isRead: true);
        var unread31 = Notification(marker, now.AddDays(-31), isRead: false);
        var unread91 = Notification(marker, now.AddDays(-91), isRead: false);
        var read29 = Notification(marker, now.AddDays(-29), isRead: true);
        var mail91 = Email(marker, now.AddDays(-91));
        var mail89 = Email(marker, now.AddDays(-89));
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.UserNotifications.AddRange(read31, unread31, unread91, read29);
            db.EmailSendLogs.AddRange(mail91, mail89);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            // The job is internal to the Notification module; build it the way Hangfire does, from the container.
            var jobType = typeof(NotificationRecurringJobs).Assembly.GetType("Notification.Scheduling.NotificationCleanupJob")!;
            var job = ActivatorUtilities.CreateInstance(scope.ServiceProvider, jobType);
            await (Task)jobType.GetMethod("ExecuteAsync")!.Invoke(job, [TestContext.Current.CancellationToken])!;
        }

        await using var check = Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var notifications = await checkDb.UserNotifications.AsNoTracking()
            .Where(n => n.Username == marker).Select(n => n.Id).ToListAsync(TestContext.Current.CancellationToken);
        var mails = await checkDb.EmailSendLogs.AsNoTracking()
            .Where(e => e.Subject == marker).Select(e => e.Id).ToListAsync(TestContext.Current.CancellationToken);
        notifications.Should().BeEquivalentTo([unread31.Id, read29.Id]);
        mails.Should().BeEquivalentTo([mail89.Id]);
    }

    [Fact]
    public async Task Inbox_only_context_cleanup_runs_without_an_outbox_table_and_purges_old_processed_inbox_rows()
    {
        var now = Services.GetRequiredService<IDateTimeProvider>().ApplicationNow;
        var consumer = $"cleanup-{Guid.NewGuid():N}";
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).Should().BeNull();
            foreach (var (id, processedAt) in new[]
                     {
                         (oldId, now.AddDays(-OutboxDeliveryPolicy.ProcessedRetentionDays - 1)),
                         (recentId, now.AddDays(-1))
                     })
            {
                var row = InboxMessage.Create(id, consumer, processedAt);
                row.MarkAsProcessed(processedAt);
                db.Set<InboxMessage>().Add(row);
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            var job = ActivatorUtilities.CreateInstance<OutboxCleanupJob<NotificationDbContext>>(scope.ServiceProvider);
            await job.ExecuteAsync(TestContext.Current.CancellationToken);
        }

        await using var check = Services.CreateAsyncScope();
        var remaining = await check.ServiceProvider.GetRequiredService<NotificationDbContext>()
            .Set<InboxMessage>().AsNoTracking()
            .Where(m => m.ConsumerType == consumer).Select(m => m.MessageId).ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().BeEquivalentTo([recentId]);
    }
}
