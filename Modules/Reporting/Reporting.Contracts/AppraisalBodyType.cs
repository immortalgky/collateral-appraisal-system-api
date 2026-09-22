namespace Reporting.Contracts;

/// <summary>Appraisal-book / summary body variant, derived from the appraisal data.</summary>
public enum AppraisalBodyType
{
    Standard,
    Construction,
    Block,
}

/// <summary>
/// The single source of the block / construction / standard dispatch rule: an <c>appraisal.Projects</c> row
/// → Block; a Progressive appraisal → Construction; otherwise Standard. Note the precedence — a Progressive
/// appraisal that is also a block project is a Block, not a Construction.
///
/// Lives in Contracts rather than inside Reporting because callers outside the module need the same answer.
/// The Appraisal module files the generated summary under a document type that must match the form that was
/// actually rendered (D042 Construction Progress vs D043 Property Valuation); a second copy of this rule
/// there would let the label and the content drift apart silently — which it did, until they were unified.
/// </summary>
public static class AppraisalBodyTypeClassifier
{
    /// <summary>Mirrors AppraisalTypes.Progressive (read via Dapper — no compile dep on the Appraisal assembly).</summary>
    public const string ProgressiveAppraisalType = "Progressive";

    public static AppraisalBodyType Classify(bool projectExists, string? appraisalType) =>
        projectExists
            ? AppraisalBodyType.Block
            : string.Equals(appraisalType, ProgressiveAppraisalType, StringComparison.OrdinalIgnoreCase)
                ? AppraisalBodyType.Construction
                : AppraisalBodyType.Standard;
}
