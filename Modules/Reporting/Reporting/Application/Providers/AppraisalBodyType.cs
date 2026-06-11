namespace Reporting.Application.Providers;

/// <summary>Internal appraisal-book / summary body variant, derived from the appraisal data.</summary>
internal enum AppraisalBodyType
{
    Standard,
    Construction,
    Block,
}

/// <summary>
/// Single source of the block / construction / standard dispatch rule shared by
/// <see cref="AppraisalBookDataProvider"/> and <see cref="AppraisalSummaryDataProvider"/>:
/// a <c>appraisal.Projects</c> row → Block; a Progressive appraisal → Construction; otherwise Standard.
/// Keeping the rule in one place stops the two reports from classifying the same appraisal differently.
/// </summary>
internal static class AppraisalBodyTypeClassifier
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
