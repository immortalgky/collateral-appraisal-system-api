using Auth.Contracts.Users;
using MassTransit;
using Notification.Domain.Notifications.Models;

namespace Notification.Domain.Notifications.Services;

/// <summary>
/// Reports an assignment email that could not be sent (#624): the Appraisal module marks the row Failed, and the
/// admin who assigned the case plus every IntAdmin get a persisted notification linking to the case's Administration
/// page.
/// </summary>
public sealed class AssignmentEmailFailureNotifier(
    INotificationService notificationService,
    IUserLookupService userLookup,
    ILogger<AssignmentEmailFailureNotifier> logger)
{
    /// <summary>A short reason for the notice (the Appraisal side caps its column on its own).</summary>
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

        // The assigner only when they are a user ("System" on automatic paths), under the account's own spelling:
        // the realtime group user-{Username} is case-sensitive.
        string? assigner = null;
        if (!string.IsNullOrWhiteSpace(email.AssignedBy))
        {
            var found = await userLookup.GetByUsernamesAsync([email.AssignedBy], ct);
            assigner = found.TryGetValue(email.AssignedBy, out var user) ? user.Username : null;
        }

        var admins = await userLookup.GetUsernamesInGroupAsync("IntAdmin", ct);
        var recipients = new[] { assigner }.Concat(admins)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var message =
            $"ส่งอีเมลแจ้งงาน {email.AppraisalNumber ?? "-"} ถึง {email.CompanyName ?? "บริษัท"} ไม่สำเร็จ: {reason}";
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
