namespace Shared.Configurations;

/// <summary>
/// Configuration for file storage settings
/// </summary>
public class FileStorageConfiguration
{
    public const string SectionName = "FileStorage";

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
    /// Path for storing document files
    /// </summary>
    public string ArchivePath { get; set; } = "archive";

    /// <summary>
    /// Path for storing deleted document files
    /// </summary>
    public string DeletedPath { get; set; } = "deleted";

    /// <summary>
    /// Maximum allowed file size in bytes
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;

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
    /// Number of days after which temporary files are cleaned up
    /// </summary>
    public CleanupConfiguration Cleanup { get; set; } = default!;
}

public class CleanupConfiguration
{
    public int TempSessionExpirationHours { get; set; } = 24;
    public int DeletedRetentionDays { get; set; } = 30;
    public int OrphanedCheckDays { get; set; } = 7;
}