namespace Reporting.Contracts;

/// <summary>
/// One applied-filter line shown in an exported report (e.g. <c>Pay Type: Full Payment</c>).
/// The <see cref="Value"/> is already display-ready — coded values have been resolved to their
/// descriptions and dates/free-text formatted by the caller.
/// </summary>
public sealed record FilterCriterion(string Label, string Value);
