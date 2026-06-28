namespace Reporting.Application.OperationalReports.Shared;

/// <summary>
/// One filter a report declares for its export's "Applied filters" block.
/// <list type="bullet">
/// <item><see cref="RawValue"/> null/blank => the filter was not applied; the resolver omits it.</item>
/// <item><see cref="ParameterGroup"/> non-null => <see cref="RawValue"/> holds code(s) to resolve
/// against <c>parameter.Parameters</c> (comma-separated for multi-value filters); null => the value
/// is already display-ready (dates, free text, already-resolved enums) and is shown verbatim.</item>
/// </list>
/// </summary>
public sealed record FilterField(string Label, string? RawValue, string? ParameterGroup = null);
