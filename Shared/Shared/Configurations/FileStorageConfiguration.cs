namespace Shared.Configurations;

public enum StorageMode { Local, Nas }

/// <summary>
/// Configuration for file storage settings
/// </summary>
public class FileStorageConfiguration
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Storage mode: Local (wwwroot) or Nas (network share)
    /// </summary>
    public StorageMode Mode { get; set; } = StorageMode.Local;

    /// <summary>
    /// Base path for NAS storage (e.g. "\\\\nas_app_dev\\CAS")
    /// </summary>
    public string? NasBasePath { get; set; }

    /// <summary>
    /// Root path for file storage
    /// </summary>
    public string RootPath { get; set; } = "/uploads";

    /// <summary>
    /// Temporary file storage path
    /// </summary>
    public string TempPath { get; set; } = "temp";

    /// <summary>
    /// Path for storing document files
    /// </summary>
    public string DocumentsPath { get; set; } = "documents";

    /// <summary>
    /// Path for storing archived files
    /// </summary>
    public string ArchivePath { get; set; } = "archive";

    /// <summary>
    /// Path for storing deleted document files
    /// </summary>
    public string DeletedPath { get; set; } = "deleted";

    /// <summary>
    /// Maximum allowed file size in bytes
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// The largest request body an upload may have. A multipart request carries MIME boundaries
    /// and the other form fields alongside the file, so it is always a little larger than the file
    /// itself — without the allowance a file of exactly <see cref="MaxFileSizeBytes"/> would be
    /// rejected by its own envelope. Both the server limits and the endpoints' cheap
    /// Content-Length pre-check read this; the exact per-file limit is enforced by the validator.
    /// </summary>
    public long MaxRequestBodyBytes => (long)MaxFileSizeBytes + 1024 * 1024;

    /// <summary>
    /// How many bytes of attachments one report book may carry, and therefore also the largest
    /// single appendix that can fit in one. The assembler holds every page of the book in memory
    /// at once, which is why this is a limit of its own rather than the upload limit.
    /// <para>
    /// Read by both the attach-time refusal (<c>AddAppendixDocumentCommandHandler</c>) and the
    /// assembler's own budget, so the size a user is told when attaching is the size the book can
    /// actually carry. Attachments are still counted cumulatively while a book is assembled: three
    /// files of 40 MB each pass the attach check and the third is dropped from the book with a
    /// warning, because what a document will eventually be bound alongside is not knowable when it
    /// is attached.
    /// </para>
    /// </summary>
    public long MaxAppendixFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// How much memory one image may occupy once decoded — width × height × 4 bytes, measured
    /// after whatever scaling the codec can apply while decoding.
    /// <para>
    /// Bytes on disk say nothing about this. A 5 MB JPEG can hold 100 megapixels, while a 40 MB
    /// PNG holds a quarter of that; and a JPEG can be decoded straight to 1/8 scale where a PNG
    /// must be decoded whole. So the byte limit that guards storage cannot also guard memory, and
    /// this one does: an image over budget is refused at upload, and one already stored is served
    /// as a placeholder rather than taken to the server out of memory.
    /// </para>
    /// </summary>
    public long MaxImageDecodeBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>
    /// Maximum number of files allowed per upload session
    /// </summary>
    public int MaxFilesPerSession { get; set; } = 20;

    /// <summary>
    /// Maximum total session size in bytes
    /// </summary>
    public int MaxTotalSessionSizeBytes { get; set; } = 200 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions
    /// </summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx"
    ];

    /// <summary>
    /// Configuration settings for file cleanup operations
    /// </summary>
    public CleanupConfiguration Cleanup { get; set; } = default!;

    /// <summary>
    /// Configuration for image variant/thumbnail generation
    /// </summary>
    public ImageVariantsConfiguration ImageVariants { get; set; } = new();
}

public class CleanupConfiguration
{
    public int TempSessionExpirationHours { get; set; } = 24;
    public int DeletedRetentionDays { get; set; } = 30;
    public int OrphanedCheckDays { get; set; } = 7;
}

public class ImageVariantsConfiguration
{
    public bool GenerateThumbnails { get; set; }
    public Dictionary<string, ImageSizeConfiguration> Sizes { get; set; } = new();
}

public class ImageSizeConfiguration
{
    public int Width { get; set; }
    public int Height { get; set; }
}