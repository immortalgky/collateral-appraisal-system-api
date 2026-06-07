namespace Reporting.Infrastructure.BrowserPool;

/// <summary>
/// Configuration bound from appsettings <c>Reporting</c> section.
/// </summary>
public sealed class ReportingConfiguration
{
    public const string SectionName = "Reporting";

    /// <summary>
    /// Optional absolute path to a Chromium/Chrome executable.
    /// When empty, PuppeteerSharp downloads Chromium on first startup.
    /// Example (Windows): C:\chrome\chrome.exe
    /// </summary>
    public string? ChromiumExecutablePath { get; set; }

    /// <summary>
    /// Maximum number of concurrent Chromium pages (one per in-flight render).
    /// Default: 4.
    /// </summary>
    public int MaxConcurrentPages { get; set; } = 4;

    /// <summary>
    /// Subfolder under the storage base path where generated report PDFs are written.
    /// Default: "reports". Kept separate from the Document module's "documents" folder.
    /// </summary>
    public string ReportsSubfolder { get; set; } = "reports";

    /// <summary>
    /// How many days to retain completed/failed job rows and their on-disk PDF artifacts
    /// before the nightly cleanup job purges them. Default: 7 days.
    /// </summary>
    public int ArtifactRetentionDays { get; set; } = 7;
}
