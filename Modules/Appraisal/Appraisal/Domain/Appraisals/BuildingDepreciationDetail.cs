namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Detailed depreciation calculation for building appraisals.
/// </summary>
public class BuildingDepreciationDetail : Entity<Guid>
{
    public Guid AppraisalPropertyId { get; private set; }

    // Depreciation Method
    public string DepreciationMethod { get; private set; } = null!; // StraightLine, DecliningBalance, AgeLife
    public int UsefulLifeYears { get; private set; }
    public int EffectiveAge { get; private set; }
    public int RemainingLifeYears { get; private set; }
    public decimal? SalvageValuePercent { get; private set; }

    // Calculation
    public decimal ReplacementCostNew { get; private set; }
    public decimal PhysicalDepreciationPct { get; private set; }
    public decimal PhysicalDepreciationAmt { get; private set; }
    public decimal? FunctionalObsolescencePct { get; private set; }
    public decimal? FunctionalObsolescenceAmt { get; private set; }
    public decimal? ExternalObsolescencePct { get; private set; }
    public decimal? ExternalObsolescenceAmt { get; private set; }
    public decimal TotalDepreciationPct { get; private set; }
    public decimal TotalDepreciationAmt { get; private set; }
    public decimal DepreciatedValue { get; private set; }

    // Condition Assessment
    public string? StructuralCondition { get; private set; } // Excellent, Good, Fair, Poor
    public string? MaintenanceLevel { get; private set; } // WellMaintained, Average, Deferred
    public string? ConditionNotes { get; private set; }

    private BuildingDepreciationDetail()
    {
    }

    public static BuildingDepreciationDetail Create(
        Guid appraisalPropertyId,
        string depreciationMethod,
        int usefulLifeYears,
        int effectiveAge,
        decimal replacementCostNew)
    {
        var validMethods = new[] { "StraightLine", "DecliningBalance", "AgeLife" };
        if (!validMethods.Contains(depreciationMethod))
            throw new ArgumentException($"DepreciationMethod must be one of: {string.Join(", ", validMethods)}");

        var remainingLife = usefulLifeYears - effectiveAge;
        if (remainingLife < 0) remainingLife = 0;

        return new BuildingDepreciationDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            DepreciationMethod = depreciationMethod,
            UsefulLifeYears = usefulLifeYears,
            EffectiveAge = effectiveAge,
            RemainingLifeYears = remainingLife,
            ReplacementCostNew = replacementCostNew
        };
    }

    public void CalculateDepreciation(
        decimal physicalDepreciationPct,
        decimal? functionalObsolescencePct = null,
        decimal? externalObsolescencePct = null,
        decimal? salvageValuePercent = null)
    {
        SalvageValuePercent = salvageValuePercent;
        PhysicalDepreciationPct = physicalDepreciationPct;
        PhysicalDepreciationAmt = ReplacementCostNew * physicalDepreciationPct / 100;

        FunctionalObsolescencePct = functionalObsolescencePct;
        FunctionalObsolescenceAmt = functionalObsolescencePct.HasValue
            ? ReplacementCostNew * functionalObsolescencePct.Value / 100
            : null;

        ExternalObsolescencePct = externalObsolescencePct;
        ExternalObsolescenceAmt = externalObsolescencePct.HasValue
            ? ReplacementCostNew * externalObsolescencePct.Value / 100
            : null;

        TotalDepreciationPct = physicalDepreciationPct
                               + (functionalObsolescencePct ?? 0)
                               + (externalObsolescencePct ?? 0);

        TotalDepreciationAmt = PhysicalDepreciationAmt
                               + (FunctionalObsolescenceAmt ?? 0)
                               + (ExternalObsolescenceAmt ?? 0);

        DepreciatedValue = ReplacementCostNew - TotalDepreciationAmt;
        if (DepreciatedValue < 0) DepreciatedValue = 0;
    }

    public void SetConditionAssessment(string? structuralCondition, string? maintenanceLevel, string? notes)
    {
        StructuralCondition = structuralCondition;
        MaintenanceLevel = maintenanceLevel;
        ConditionNotes = notes;
    }
}