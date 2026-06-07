namespace Collateral.CollateralMasters.Models;

/// <summary>
/// How the buyer financed the purchase of a project unit.
/// Stored as the enum NAME string in the database ("Cash" / "Loan") for readability,
/// consistent with the Appraisal module convention.
///
/// NOTE: This is a Collateral-module-local copy of the same concept in
/// <c>Appraisal.Domain.Projects.UnitPurchaseMethod</c>. Both enums are intentionally
/// identical in value and name so that Phase 2 sync logic can translate directly via
/// string name without a lookup table.
/// Cross-module enum import is avoided to preserve bounded-context independence.
/// </summary>
public enum UnitPurchaseMethod
{
    Cash = 1,
    Loan = 2
}
