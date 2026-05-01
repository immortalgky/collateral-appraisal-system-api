namespace Appraisal.Domain.Appraisals.Hypothesis.CostItems;

/// <summary>
/// Stable semantic identifier for a Hypothesis cost item row.
/// Description is a mutable UI label; Kind is the calc-service key.
/// Paired with <see cref="HypothesisCostCategory"/> to uniquely identify a row.
/// </summary>
public enum CostItemKind
{
    // ── L&B — ProjectDevCost ─────────────────────────────────────────────
    PublicUtilityConstruction = 1,
    LandFilling = 2,

    // ── L&B — ProjectCost ────────────────────────────────────────────────
    AllocationPermitFee = 10,
    LandTitleDeedDivisionFee = 11,
    ProfessionalFee = 12,
    AdminFee = 13,
    SellingAdvertising = 14,

    // ── L&B — GovernmentTax ──────────────────────────────────────────────
    TransferFee = 20,
    SpecificBusinessTax = 21,

    // ── Condo — HardCost ─────────────────────────────────────────────────
    CondoBuildingConstruction = 30,
    Furniture = 31,
    ExternalUtilities = 32,

    // ── Condo — SoftCost ─────────────────────────────────────────────────
    // ProfessionalFee (12) shared — use same value
    // AdminFee (13) shared
    // SellingAdvertising (14) shared
    CondoTitleDeedFee = 40,
    EIA = 41,
    CondoRegistrationFee = 42,

    // ── Condo — CondoGovTax ──────────────────────────────────────────────
    // TransferFee (20) shared
    // SpecificBusinessTax (21) shared

    /// <summary>
    /// Ad-hoc rows added by the user (not part of the seeded template).
    /// </summary>
    Other = 99,

    // ── CostOfBuilding ───────────────────────────────────────────────────
    /// <summary>
    /// Per-model construction cost rows — model-keyed, not calc-service-looked-up.
    /// </summary>
    BuildingConstruction = 100
}
