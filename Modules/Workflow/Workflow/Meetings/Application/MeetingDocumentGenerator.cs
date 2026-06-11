using Document.Contracts;
using Reporting.Contracts;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Application;

/// <summary>
/// Generates a meeting PDF report, persists it as a Document, links it to the meeting
/// aggregate, and enqueues the DocumentLinked outbox event — all in one call.
/// The caller's transactional Unit-of-Work commits the meeting-side changes;
/// IDocumentCreator keeps its own Document-module SaveChanges (unchanged behaviour).
/// </summary>
public interface IMeetingDocumentGenerator
{
    /// <summary>
    /// Generates and links an "Invitation" or "Minute" document for the meeting.
    /// Throws if PDF generation fails — let exceptions propagate to the caller.
    /// </summary>
    Task<MeetingDocument> GenerateAndLinkAsync(
        Meeting meeting,
        string documentType,
        CancellationToken ct);
}

internal sealed class MeetingDocumentGenerator(
    IReportPdfGenerator reportPdfGenerator,
    IDocumentCreator documentCreator,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventOutbox outbox)
    : IMeetingDocumentGenerator
{
    // Maps the user-facing DocumentType to the registered report key.
    private static readonly Dictionary<string, string> ReportKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Invitation", MeetingDocumentConstants.InvitationReportKey },
            { "Minute",     MeetingDocumentConstants.MinuteReportKey }
        };

    public async Task<MeetingDocument> GenerateAndLinkAsync(
        Meeting meeting,
        string documentType,
        CancellationToken ct)
    {
        if (!ReportKeyMap.TryGetValue(documentType, out var reportKey))
            throw new ArgumentException(
                $"Unknown document type '{documentType}'. Valid values: Invitation, Minute.",
                nameof(documentType));

        var userCode = currentUserService.UserCode
            ?? currentUserService.Username
            ?? "system";
        var userName = currentUserService.Username ?? "system";
        var now = dateTimeProvider.ApplicationNow;

        // Generate the PDF — synchronous in-process call, returns raw bytes.
        // Any exception (e.g. Puppeteer render failure) propagates to the caller.
        var reportFile = await reportPdfGenerator.GenerateAsync(reportKey, meeting.Id.ToString(), ct);

        // Meeting-specific filename so regenerated docs aren't all "meeting-invitation.pdf".
        var fileName = BuildFileName(reportKey, meeting.MeetingNo);

        // Persist as a real Document so it gets a stable id and can be downloaded later.
        var documentId = await documentCreator.CreateFromBytesAsync(
            reportFile.Bytes,
            fileName,
            reportFile.ContentType,
            documentType: MeetingDocumentConstants.DocumentType,
            documentCategory: MeetingDocumentConstants.DocumentCategory,
            uploadedBy: userCode,
            uploadedByName: userName,
            ct);

        // Link the document to the meeting aggregate.
        var data = new MeetingDocumentData(
            DocumentId: documentId,
            DocumentType: documentType,
            FileName: fileName,
            Source: "Generated",
            CreatedBy: userCode,
            CreatedAt: now);

        var meetingDoc = meeting.AddDocument(data);

        // Publish link event so the Document module increments ReferenceCount.
        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                RequestId: meeting.Id,       // owner id — meeting id reuses the RequestId field
                DocumentId: documentId,
                DocumentType: documentType),
            correlationId: meeting.Id.ToString());

        return meetingDoc;
    }

    /// <summary>
    /// e.g. "meeting-invitation-47-2569.pdf"; falls back to the report key when no MeetingNo.
    /// </summary>
    internal static string BuildFileName(string reportKey, string? meetingNo)
    {
        if (string.IsNullOrWhiteSpace(meetingNo))
            return $"{reportKey}.pdf";

        var safe = meetingNo.Replace('/', '-').Replace('\\', '-').Trim();
        return $"{reportKey}-{safe}.pdf";
    }
}
