namespace Appraisal.Domain.Evaluations;

/// <summary>
/// Admin-configurable criteria definition for service quality evaluations.
/// One row per (BankingSegment × CriteriaSlot 1..5).
/// Immutable identity: BankingSegment + CriteriaSlot + CriteriaKey.
/// Mutable: label, weight, maxScore, guidance, thresholds.
/// </summary>
public class EvaluationCriteriaConfig : Entity<Guid>
{
    // ── Identity (immutable after creation) ─────────────────────────────────
    public string BankingSegment { get; private set; } = null!;   // "Retail" | "IBG"
    public int CriteriaSlot { get; private set; }                  // 1..5 → Criteria{N}Rating column
    public string CriteriaKey { get; private set; } = null!;      // stable slug e.g. "Delivery"

    // ── Labels ──────────────────────────────────────────────────────────────
    public string LabelEn { get; private set; } = null!;
    public string LabelTh { get; private set; } = null!;

    // ── Score parameters ─────────────────────────────────────────────────────
    public decimal Weight { get; private set; }    // decimal(5,4), e.g. 0.2000
    public int MaxScore { get; private set; }      // always 5 for current criteria

    // ── Guidance JSON ────────────────────────────────────────────────────────
    // Bilingual rating-level descriptions:
    // {"1":{"en":"…","th":"…"},"2":{…},…,"5":{…}}
    public string GuidanceJson { get; private set; } = null!;

    // ── Delivery thresholds (slot 2 only, nullable for all other slots) ──────
    // Maps rating level → upper-bound business days:
    // {"5":2,"4":2.5,"3":3,"2":3.5} (Retail) or {"5":5,"4":7,"3":10,"2":12} (IBG)
    public string? ThresholdsJson { get; private set; }

    // ── Display ordering ─────────────────────────────────────────────────────
    public int DisplayOrder { get; private set; }

    // ── Private EF Core constructor ──────────────────────────────────────────
    private EvaluationCriteriaConfig() { }

    // ── Factory ──────────────────────────────────────────────────────────────
    public static EvaluationCriteriaConfig Create(
        string bankingSegment,
        int criteriaSlot,
        string criteriaKey,
        string labelEn,
        string labelTh,
        decimal weight,
        int maxScore,
        string guidanceJson,
        string? thresholdsJson,
        int displayOrder)
    {
        return new EvaluationCriteriaConfig
        {
            Id            = Guid.CreateVersion7(),
            BankingSegment = bankingSegment,
            CriteriaSlot  = criteriaSlot,
            CriteriaKey   = criteriaKey,
            LabelEn       = labelEn,
            LabelTh       = labelTh,
            Weight        = weight,
            MaxScore      = maxScore,
            GuidanceJson  = guidanceJson,
            ThresholdsJson = thresholdsJson,
            DisplayOrder  = displayOrder
        };
    }

    // ── Mutator ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Updates mutable fields. BankingSegment, CriteriaSlot, and CriteriaKey are immutable.
    /// </summary>
    public void Update(
        string labelEn,
        string labelTh,
        decimal weight,
        int maxScore,
        string guidanceJson,
        string? thresholdsJson)
    {
        LabelEn        = labelEn;
        LabelTh        = labelTh;
        Weight         = weight;
        MaxScore       = maxScore;
        GuidanceJson   = guidanceJson;
        ThresholdsJson = thresholdsJson;
    }
}
