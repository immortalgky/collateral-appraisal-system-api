namespace Reporting.Application.Services;

/// <summary>
/// Maps a <c>reportTypeKey</c> to its registered provider and templateId.
/// Adding a new report type = new provider registration + new entry here.
/// </summary>
public interface IReportRegistry
{
    /// <summary>
    /// Looks up the registration for <paramref name="reportTypeKey"/>.
    /// Returns null if the key is unknown.
    /// </summary>
    ReportRegistration? TryGet(string reportTypeKey);
}

/// <summary>Immutable registration record for one report type.</summary>
public sealed record ReportRegistration(
    string ReportTypeKey,
    string TemplateId,
    IReportDataProvider Provider);
