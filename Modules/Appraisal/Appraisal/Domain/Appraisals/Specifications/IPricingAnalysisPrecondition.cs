namespace Appraisal.Domain.Appraisals.Specifications;

/// <summary>
/// A single business rule that must hold before a pricing analysis can be created
/// or started for a property group. Implementations are pure functions over
/// <see cref="ReadinessSnapshot"/> — no DB access, no side effects, easy to unit-test.
/// </summary>
public interface IPricingAnalysisPrecondition
{
    IEnumerable<RuleViolation> Check(ReadinessSnapshot snapshot);
}

/// <summary>
/// Stable violation codes. Keep these in sync with the React client's
/// <c>ViolationCode</c> union and i18n keys (<c>readiness.&lt;CODE&gt;</c>).
/// </summary>
public static class ViolationCodes
{
    public const string MarketSurveyRequired      = "MARKET_SURVEY_REQUIRED";
    public const string BuildingDetailRequired    = "BUILDING_DETAIL_REQUIRED";
    public const string RentalInfoRequired        = "RENTAL_INFO_REQUIRED";
    public const string RentalScheduleRequired    = "RENTAL_SCHEDULE_REQUIRED";
    public const string PropertyNotSaved          = "PROPERTY_NOT_SAVED";
}
