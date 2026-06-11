namespace Notification.Contracts.Email;

/// <summary>
/// A typed reference to an attachment that will be resolved at send time.
/// <para>
/// Known types (dispatched by <c>IEmailAttachmentAssembler</c>):
/// <list type="bullet">
///   <item><c>"document"</c> — value is a document Guid; resolved via <c>IDocumentContentProvider</c>.</item>
///   <item><c>"report"</c>  — value is <c>"reportKey:entityId"</c>; resolved via <c>IReportPdfGenerator</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed record EmailAttachmentRef(string Type, string Value);
