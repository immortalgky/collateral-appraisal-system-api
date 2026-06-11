using Reporting.Data;

namespace Reporting.Application.Services;

/// <summary>
/// Maps a <c>reportTypeKey</c> to its registered provider and templateId.
/// Adding a new report type = new provider registration + new entry here.
/// </summary>
public interface IReportRegistry
{
    /// <summary>
    /// Looks up the registration for <paramref name="reportTypeKey"/>.
    /// Returns null if the key is unknown (no definition row and no provider, or a definition
    /// row with no matching provider). A <b>disabled</b> report still returns a registration
    /// (carrying <c>IsEnabled = false</c>) so callers can choose how to refuse it — the
    /// synchronous service and the enqueue endpoint both reject disabled reports.
    /// </summary>
    ReportRegistration? TryGet(string reportTypeKey);
}

/// <summary>Immutable registration record for one report type.</summary>
public sealed record ReportRegistration(
    string ReportTypeKey,
    string TemplateId,
    IReportDataProvider Provider,
    ReportGenerationMode GenerationMode,
    bool IsEnabled,
    string Category);
