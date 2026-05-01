namespace Appraisal.Domain.Appraisals.Hypothesis.CostItems;

/// <summary>
/// Categories for cost items on the Hypothesis pricing analysis.
/// L&amp;B categories: CostOfBuilding, ProjectDevCost, ProjectCost, GovernmentTax.
/// Condo categories: HardCost, SoftCost, CondoGovTax.
/// </summary>
public enum HypothesisCostCategory
{
    // ── Land &amp; Building ─────────────────────────────────────
    /// <summary>
    /// Construction cost per unit/house model (Tab 2 of L&amp;B). ModelName required.
    /// </summary>
    CostOfBuilding = 1,

    /// <summary>
    /// Project Development Cost Estimates (C27..C39). Public Utility, Land Filling, Contingency.
    /// </summary>
    ProjectDevCost = 2,

    /// <summary>
    /// Project Cost Estimates (C43..C65). Permit fees, professional fees, admin, selling/adv.
    /// </summary>
    ProjectCost = 3,

    /// <summary>
    /// Government Taxes and Fees (C66..C73). Transfer fee, specific business tax.
    /// </summary>
    GovernmentTax = 4,

    // ── Condominium ───────────────────────────────────────────
    /// <summary>
    /// Hard Cost (E15..E27). Building construction, furniture/AC, external utilities.
    /// </summary>
    HardCost = 5,

    /// <summary>
    /// Soft Cost (E29..E45). Professional fees, admin, selling/adv, title deed, EIA, registration.
    /// </summary>
    SoftCost = 6,

    /// <summary>
    /// Government Taxes for Condo (E46..E50). Transfer fee, specific business tax.
    /// </summary>
    CondoGovTax = 7
}
