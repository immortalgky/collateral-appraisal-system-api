namespace Reporting.Data;

/// <summary>
/// Config-driven definition for one report type. Stored in reporting.ReportDefinitions.
/// Allows enabling/disabling reports, re-pointing to a different template, and switching
/// between Sync and Async generation WITHOUT a redeploy.
/// </summary>
public class ReportDefinition
{
    /// <summary>Business key — matches <c>IReportDataProvider.ReportTypeKey</c>.</summary>
    public string ReportTypeKey { get; private set; } = default!;

    /// <summary>
    /// Template file identifier. Defaults to ReportTypeKey (file: Templates/{TemplateId}.html).
    /// Can be changed to point to a different template without code changes.
    /// </summary>
    public string TemplateId { get; private set; } = default!;

    /// <summary>Thai display name shown in the UI report picker.</summary>
    public string DisplayNameTh { get; private set; } = default!;

    /// <summary>English display name shown in the UI report picker.</summary>
    public string DisplayNameEn { get; private set; } = default!;

    /// <summary>Logical grouping (e.g. "AppraisalSummary", "Meeting", "Appointment").</summary>
    public string Category { get; private set; } = default!;

    /// <summary>Whether this report is generated in-request (Sync) or via background job (Async).</summary>
    public ReportGenerationMode GenerationMode { get; private set; }

    /// <summary>When false the report is hidden from the FE and generation is rejected.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Optimistic-concurrency / change-tracking version. Increment when config changes.</summary>
    public int Version { get; private set; }

    private ReportDefinition() { }

    public static ReportDefinition Create(
        string reportTypeKey,
        string templateId,
        string displayNameTh,
        string displayNameEn,
        string category,
        ReportGenerationMode generationMode,
        bool isEnabled = true,
        int version = 1)
    {
        return new ReportDefinition
        {
            ReportTypeKey = reportTypeKey,
            TemplateId = templateId,
            DisplayNameTh = displayNameTh,
            DisplayNameEn = displayNameEn,
            Category = category,
            GenerationMode = generationMode,
            IsEnabled = isEnabled,
            Version = version,
        };
    }
}
