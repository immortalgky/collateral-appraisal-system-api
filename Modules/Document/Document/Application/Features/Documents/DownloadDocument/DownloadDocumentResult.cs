namespace Document.Domain.Documents.Features.DownloadDocument;

public record DownloadDocumentResult(
    string FilePath,
    string MimeType,
    string FileName,
    bool FileExists
);
