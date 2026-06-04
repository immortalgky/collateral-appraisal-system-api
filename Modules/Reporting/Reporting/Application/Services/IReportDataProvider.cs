namespace Reporting.Application.Services;

/// <summary>
/// Non-generic base interface. Each report type registers one implementation.
/// Keyed by <see cref="ReportTypeKey"/> so the registry can look up the right
/// provider without generics complexity at the composition root.
/// </summary>
public interface IReportDataProvider
{
    string ReportTypeKey { get; }

    /// <summary>
    /// Resolve the data model for the given entity (e.g. appraisalId).
    /// Returns a boxed strongly-typed model; callers cast to the expected type.
    /// </summary>
    Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken);
}
