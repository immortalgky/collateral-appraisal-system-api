using Reporting.Application.Services;

namespace Reporting.Infrastructure;

/// <summary>
/// Maps report type keys to their registrations.
/// Adding a new report type = new <see cref="IReportDataProvider"/> implementation
/// + a new registration in <see cref="ReportingModule.AddReportingModule"/>.
/// </summary>
internal sealed class ReportRegistry(IEnumerable<IReportDataProvider> providers) : IReportRegistry
{
    // TemplateId convention: same as reportTypeKey (file: Templates/{key}.html)
    private readonly IReadOnlyDictionary<string, ReportRegistration> _registrations =
        providers.ToDictionary(
            p => p.ReportTypeKey,
            p => new ReportRegistration(p.ReportTypeKey, p.ReportTypeKey, p),
            StringComparer.OrdinalIgnoreCase);

    public ReportRegistration? TryGet(string reportTypeKey) =>
        _registrations.TryGetValue(reportTypeKey, out var reg) ? reg : null;
}
