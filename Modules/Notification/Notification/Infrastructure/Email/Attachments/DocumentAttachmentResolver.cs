using Document.Contracts;
using Notification.Contracts.Email;

namespace Notification.Infrastructure.Email.Attachments;

/// <summary>
/// Resolves <c>Type="document"</c> attachment refs.
/// The ref value is a document Guid string; content is loaded via
/// <see cref="IDocumentContentProvider"/> which reads the file from the NAS/local storage path.
/// Returns an empty list if the document is unavailable.
/// </summary>
internal sealed class DocumentAttachmentResolver(
    IDocumentContentProvider contentProvider,
    ILogger<DocumentAttachmentResolver> logger) : IEmailAttachmentResolver
{
    public string Type => "document";

    public async Task<IReadOnlyList<EmailAttachment>> ResolveAsync(string value, CancellationToken ct)
    {
        if (!Guid.TryParse(value, out var documentId))
        {
            logger.LogWarning(
                "DocumentAttachmentResolver: value '{Value}' is not a valid Guid. Skipping.", value);
            return [];
        }

        var content = await contentProvider.GetAsync(documentId, ct);
        if (content is null)
            return [];

        return [new EmailAttachment(content.FileName, content.Bytes, content.MimeType)];
    }
}
