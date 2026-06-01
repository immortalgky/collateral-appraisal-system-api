namespace Workflow.Workflow.Pipeline.Validation;

/// <summary>
/// Defines a single queryable appraisal field exposed to admin-authored validation rules.
/// </summary>
/// <param name="Key">Stable camelCase identifier referenced in rule config.</param>
/// <param name="Column">Column name in appraisal.vw_AppraisalValidationContext.</param>
/// <param name="DataType">Semantic type hint for the admin UI (string|number|boolean).</param>
/// <param name="DisplayName">Human-readable label shown in the field picker.</param>
public sealed record FieldDef(string Key, string Column, string DataType, string DisplayName);

/// <summary>
/// Developer-maintained whitelist of appraisal fields that admin rules may reference.
/// Rules specify a <see cref="FieldDef.Key"/>; the step resolves it to a view column here.
/// Admin input never reaches SQL — only the step SELECT and dictionary lookup do.
/// </summary>
public static class AppraisalFieldRegistry
{
    /// <summary>All registered fields. Exposed to the admin field-picker endpoint.</summary>
    public static readonly IReadOnlyList<FieldDef> Fields = new List<FieldDef>
    {
        new("status",                    "Status",                        "string",  "Appraisal Status"),
        new("appraisalType",             "AppraisalType",                 "string",  "Appraisal Type"),
        new("purpose",                   "Purpose",                       "string",  "Purpose"),
        new("channel",                   "Channel",                       "string",  "Channel"),
        new("bankingSegment",            "BankingSegment",                "string",  "Banking Segment"),
        new("isPma",                     "IsPma",                         "boolean", "Is PMA"),
        new("facilityLimit",             "FacilityLimit",                 "number",  "Facility Limit"),
        new("appraisedValue",            "AppraisedValue",                "number",  "Appraised Value"),
        new("propertyCount",             "PropertyCount",                 "number",  "Property Count"),
        new("propsMissingLandOffice",     "PropertiesMissingLandOfficeCount", "number", "Properties Missing Land Office"),
        new("propsMissingTitle",          "PropertiesMissingTitleCount",  "number",  "Properties Missing Title"),
        new("hasNoAppraisedValue",        "HasNoAppraisedValue",          "boolean", "Has No Appraised Value"),
    };

    private static readonly Dictionary<string, FieldDef> _byKey =
        Fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the field definition for <paramref name="key"/>, or <c>null</c> if not registered.
    /// </summary>
    public static FieldDef? Resolve(string key) =>
        _byKey.TryGetValue(key, out var def) ? def : null;
}
