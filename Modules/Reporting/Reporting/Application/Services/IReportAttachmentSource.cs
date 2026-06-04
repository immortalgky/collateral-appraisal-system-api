namespace Reporting.Application.Services;

/// <summary>
/// Resolves a DocumentId to its physical file path on disk.
/// Implemented by querying the <c>document.Documents</c> table via Dapper
/// (avoids a hard ProjectReference to the Document module).
/// </summary>
public interface IReportAttachmentSource
{
    /// <summary>
    /// Returns the absolute file path for the given document, or null if not found / deleted.
    /// </summary>
    Task<string?> GetFilePathAsync(Guid documentId, CancellationToken cancellationToken);
}
