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
    /// <summary>The <c>AssignmentEmails.FailureReason</c> column width.</summary>
    public const int MaxReasonLength = 200;

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
