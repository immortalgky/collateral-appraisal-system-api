namespace Reporting.Contracts;

/// <summary>
/// One sign-off line rendered in an exported report's footer (e.g. <c>Print Report By: P5229 - …</c>).
/// A null/blank <see cref="Value"/> renders the label with an empty signing line, for a reader to
/// complete by hand — that is the intended shape of the FSD's "Approve Report By" (ผู้ตรวจสอบ).
/// </summary>
public sealed record ReportSignoff(string Label, string? Value);
