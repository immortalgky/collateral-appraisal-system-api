using Shared.Exceptions;

namespace Reporting.Contracts;

/// <summary>
/// A composite report resolved to no child reports at all: nothing in the entity matches any of the forms
/// the report is built from. Distinct from an unknown or disabled report key, which is a misconfiguration.
///
/// The appraisal summary, for example, fans out to land-building / condo / machine forms; an appraisal whose
/// properties are only <c>VEH</c> or <c>VES</c> (both valid PropertyType values with no summary form), or one
/// with no properties at all, matches none of them. That is a fact about the data, not a failure — callers
/// that treat it as one end up logging an error and offering a retry that cannot ever succeed.
///
/// Derives from <see cref="NotFoundException"/> so existing callers that catch that type — and the global
/// handler that maps it to 404 — keep behaving exactly as before; only callers that want to tell the two
/// apart need to know this type exists.
/// </summary>
public sealed class NoApplicableReportException(string reportKey, string entityId)
    : NotFoundException($"Report '{reportKey}' has no applicable content for entity ({entityId}).")
{
    public string ReportKey { get; } = reportKey;

    public string EntityId { get; } = entityId;
}
