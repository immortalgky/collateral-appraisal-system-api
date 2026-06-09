namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Reporting module (via ReportGenerationJob) when a PDF generation job
/// fails. Consumed by the Notification module to persist a durable UserNotification and
/// push a realtime ReceiveNotification to the requesting user.
///
/// RequestedByCode is the bank UserCode (= AspNetUsers.UserName, e.g. "P5229") — never a Guid.
/// </summary>
public record ReportGenerationFailedIntegrationEvent : IntegrationEvent
{
    public Guid JobId { get; init; }
    public string ReportTypeKey { get; init; } = default!;

    /// <summary>Error message from the generation failure. Truncated to 2 000 chars on the source side.</summary>
    public string Error { get; init; } = default!;

    /// <summary>Bank UserCode of the user who requested the report. Used as notification recipient.</summary>
    public string RequestedByCode { get; init; } = default!;
}
