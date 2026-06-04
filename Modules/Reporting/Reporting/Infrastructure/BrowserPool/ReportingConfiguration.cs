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
}
