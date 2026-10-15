namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// The appraisal number as AS400 sends it (COLLATREV "CCSURV") versus as CAS stores it.
///
/// AS400 prefixes a block project's appraisal number with <c>B</c>; CAS stores it without, so
/// <c>B62A00645</c> is our <c>62A00645</c>. The same rule lives in
/// <c>collateral.vw_HostCollateralLinkKeys.CasAppraisalNumber</c> for the COLLATLINK feed — keep them
/// identical. The result is stored (<see cref="ReappraisalCandidate.NormalizedSurveyNumber"/>) rather
/// than applied inside join predicates, which must stay resolvable to an index seek.
/// </summary>
public static class As400AppraisalNumber
{
    /// <remarks>
    /// Mirrors the SQL spelling: LTRIM/RTRIM strip spaces only, and <c>LEFT(x, 1) = 'B'</c> is
    /// case-insensitive under the database's CI collation. Upper-cased because SQL compares book numbers
    /// case-insensitively: in C# (the ingestor's book keys) "62a00645" and "62A00645" must be one book too.
    /// </remarks>
    public static string Normalize(string surveyNumber)
    {
        var s = surveyNumber.Trim(' ').ToUpperInvariant();
        return s.Length > 1 && s[0] == 'B' ? s[1..] : s;
    }

    /// <summary>
    /// A unit ticket CAS issued for block-project units: eight characters with a literal 'U' at position 3
    /// (appraisal numbers are all digits). Same shape test as collateral.vw_HostCollateralLinkKeys.
    /// </summary>
    public static bool IsUnitTicket(string normalized) =>
        normalized.Length == 8 && normalized[2] == 'U';
}
