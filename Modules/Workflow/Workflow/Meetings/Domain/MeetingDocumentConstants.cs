namespace Workflow.Meetings.Domain;

/// <summary>
/// Shared string constants for meeting documents.
/// Values must stay in sync with the Document module's type/category registry
/// and with the ReportDefinitions registered in the Reporting module.
/// Do NOT change the values — the frontend uploads with the same strings.
/// </summary>
public static class MeetingDocumentConstants
{
    // Document module type/category used when persisting generated PDFs.
    public const string DocumentType = "MEETING";
    public const string DocumentCategory = "meeting";

    // Report keys registered in the Reporting module's ReportDefinitions.
    public const string InvitationReportKey = "meeting-invitation";
    public const string MinuteReportKey = "meeting-minute";
}
