using Auth.Contracts.Users;
using MassTransit;
using Notification.Data;
using Notification.Domain.Notifications.Models;
using Notification.Domain.Notifications.Services;
using Shared.Messaging.Filters;

namespace Notification.Application.EventHandlers;

/// <summary>
/// The assignment email gave up (#624): MassTransit publishes this Fault once retries are exhausted, or at once for a
/// permanent SMTP rejection (<see cref="Notification.Infrastructure.Email.SmtpFailures"/>).
/// </summary>
public sealed class AssignmentEmailRequestFaultHandler(
    AssignmentEmailFailureNotifier notifier,
    InboxGuard<NotificationDbContext> inboxGuard)
    : IConsumer<Fault<AssignmentEmailRequestedIntegrationEvent>>
{
    public Task Consume(ConsumeContext<Fault<AssignmentEmailRequestedIntegrationEvent>> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, ct =>
            notifier.NotifyAsync(context, context.Message.Message,
                context.Message.Exceptions.FirstOrDefault()?.Message, ct), context.CancellationToken);
}

/// <summary>
/// Reports an assignment email that could not be sent: the Appraisal module marks the row Failed, and the admin who
/// assigned the case plus every IntAdmin get a persisted notification linking to the case's Administration page.
/// </summary>
public sealed class AssignmentEmailFailureNotifier(
    INotificationService notificationService,
    IUserLookupService userLookup,
    ILogger<AssignmentEmailFailureNotifier> logger)
{
    private const int MaxReasonLength = 200;

    public async Task NotifyAsync(
        IPublishEndpoint publisher, AssignmentEmailRequestedIntegrationEvent email, string? reason, CancellationToken ct)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? "ไม่ทราบสาเหตุ" : reason.Trim();
        if (reason.Length > MaxReasonLength) reason = reason[..MaxReasonLength];

        // Published first: if a notification below throws, the retry publishes again, and the Appraisal side only
        // moves a Queued row, so a second Failed changes nothing.
        await publisher.Publish(new AssignmentEmailFailedIntegrationEvent
        {
            AppraisalId = email.AppraisalId,
            EmailId = email.EmailId,
            Reason = reason
        }, ct);

        var admins = await userLookup.GetUsernamesInRoleAsync("IntAdmin", ct: ct);
        var recipients = new[] { email.AssignedBy }.Concat(admins)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var message =
            $"ส่งอีเมลแจ้งงาน {email.AppraisalNumber} ถึง {email.CompanyName ?? "บริษัท"} ไม่สำเร็จ: {reason}";
        var metadata = new Dictionary<string, object>
        {
            ["appraisalId"] = email.AppraisalId,
            ["emailId"] = email.EmailId
        };

        // ponytail: one save per user, so a DB failure midway re-notifies the earlier users on retry (as siblings do).
        foreach (var user in recipients)
            await notificationService.SendNotificationToUserAsync(
                user!, "ส่งอีเมลแจ้งงานไม่สำเร็จ", message, NotificationType.AssignmentEmailFailed,
                $"/appraisals/{email.AppraisalId}/administration", metadata);

        logger.LogWarning("Assignment email {EmailId} for appraisal {AppraisalId} failed: {Reason}",
            email.EmailId, email.AppraisalId, reason);
    }
}
