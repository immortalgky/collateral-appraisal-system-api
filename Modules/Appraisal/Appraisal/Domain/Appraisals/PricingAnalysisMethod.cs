using Appraisal.Domain.Appraisals.Hypothesis;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Services;

namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Methods under each approach (WQS, SaleGrid, DirectComparison, etc.).
/// </summary>
public class PricingAnalysisMethod : Entity<Guid>
{
    private readonly List<PricingComparableLink> _comparableLinks = [];
    private readonly List<PricingCalculation> _calculations = [];
    private readonly List<PricingComparativeFactor> _comparativeFactors = [];
    private readonly List<PricingFactorScore> _factorScores = [];
    private readonly List<MachineCostItem> _machineCostItems = [];

    public IReadOnlyList<PricingComparableLink> ComparableLinks => _comparableLinks.AsReadOnly();
    public IReadOnlyList<PricingCalculation> Calculations => _calculations.AsReadOnly();
    public IReadOnlyList<PricingComparativeFactor> ComparativeFactors => _comparativeFactors.AsReadOnly();
    public IReadOnlyList<PricingFactorScore> FactorScores => _factorScores.AsReadOnly();
    public IReadOnlyList<MachineCostItem> MachineCostItems => _machineCostItems.AsReadOnly();

    public Guid ApproachId { get; private set; }
    public Guid? ComparativeAnalysisTemplateId { get; private set; }

    // Method
    public string MethodType { get; private set; } =
        null!; // WQS, SaleGrid, DirectComparison, CostApproach, DCF, CapitalizationRate

    public decimal? MethodValue { get; private set; }
    public decimal? ValuePerUnit { get; private set; }
    public string? UnitType { get; private set; } // PerSqWa, PerSqm, PerUnit (PerUnit = whole-unit lumpsum)
    public bool IsSelected { get; private set; }
    public string? Remark { get; private set; }

    /// <summary>
    /// Per-method calc mode: true = system-computed, false = manually entered. This is additive to,
    /// not a replacement for, <see cref="PricingAnalysis.UseSystemCalc"/> — that column answers "was
    /// the analysis's headline figure calculated or typed"; this one answers it per method, because
    /// several methods can each contribute to that figure (a Cost approach sums role-tagged
    /// methods; other approaches show several side by side) and the report needs to know which of
    /// them, specifically, was typed. The two are never synced automatically in either direction —
    /// see <see cref="PricingAnalysis.ContributingMethodsUseSystemCalc"/> for the rollup rule a
    /// caller compares them against. Seeded from the group flag by a one-time backfill script — see
    /// <c>Database/Migration/Scripts/</c> — so existing manual groups start with every method manual
    /// too, rather than silently flipping to system.
    /// </summary>
    public bool UseSystemCalc { get; private set; } = true;

    /// <summary>
    /// This method's component of a Cost approach: Land / Building / LandAndBuilding / Machinery.
    /// Null for any method outside a Cost approach — a Cost approach sums MethodValue across its
    /// selected, role-tagged methods instead of adopting a single selected method's value verbatim.
    /// </summary>
    public string? Role { get; private set; }

    /// <summary>
    /// For a WQS/SaleGrid/DirectComparison method with Role=LandAndBuilding: the BuildingCost method
    /// (in the same approach) whose value is folded into this method's own total. The linked method
    /// is deselected so the Cost rollup does not count its value a second time.
    /// </summary>
    public Guid? LinkedMethodId { get; private set; }

    // Final Value (1:1)
    public PricingFinalValue? FinalValue { get; private set; }

    // RSQ Result (1:1, WQS only)
    public PricingRsqResult? RsqResult { get; private set; }

    // Leasehold Analysis (1:1, Leasehold only)
    public LeaseholdAnalysis? LeaseholdAnalysis { get; private set; }

    // Profit Rent Analysis (1:1, ProfitRent only)
    public ProfitRentAnalysis? ProfitRentAnalysis { get; private set; }

    // Income Analysis (1:1, Income only)
    public IncomeAnalysis? IncomeAnalysis { get; private set; }

    // Hypothesis Analysis (1:1, Hypothesis — variant chosen at generate time)
    public HypothesisAnalysis? HypothesisAnalysis { get; private set; }

    private PricingAnalysisMethod()
    {
        // For EF Core
    }

    public static PricingAnalysisMethod Create(
        Guid approachId,
        string methodType,
        string status = "Selected")
    {
        var validMethods = new[] { "WQS", "SaleGrid", "DirectComparison", "MachineryCost", "BuildingCost", "Income", "Leasehold", "ProfitRent", "Hypothesis" };
        if (!validMethods.Contains(methodType))
            throw new ArgumentException($"MethodType must be one of: {string.Join(", ", validMethods)}");

        if (status != "Selected" && status != "Alternative")
            throw new ArgumentException("Status must be 'Selected' or 'Alternative'");

        return new PricingAnalysisMethod
        {
            Id = Guid.CreateVersion7(),
            ApproachId = approachId,
            MethodType = methodType,
            IsSelected = false
        };
    }

    /// <summary>
    /// Deep-clone for CI carry-forward — copies all scalars, every child collection, and all 1:1 method analyses.
    /// <paramref name="propertyIdMap"/> remaps prior AppraisalPropertyId → new id on MachineCostItems
    /// and on Hypothesis model → building mappings
    /// (caller passes the prior→new property map built during property copy). Items whose property
    /// is unmapped are dropped.
    /// </summary>
    public static PricingAnalysisMethod CloneForApproach(
        PricingAnalysisMethod source,
        Guid newApproachId,
        IReadOnlyDictionary<Guid, Guid>? propertyIdMap = null)
    {
        var clone = new PricingAnalysisMethod
        {
            Id = Guid.CreateVersion7(),
            ApproachId = newApproachId,
            ComparativeAnalysisTemplateId = source.ComparativeAnalysisTemplateId,
            MethodType = source.MethodType,
            MethodValue = source.MethodValue,
            ValuePerUnit = source.ValuePerUnit,
            UnitType = source.UnitType,
            IsSelected = source.IsSelected,
            Remark = source.Remark,
            Role = source.Role,
            UseSystemCalc = source.UseSystemCalc
            // LinkedMethodId intentionally NOT copied: it points at a sibling method's Id, which is
            // only meaningful once that sibling has been cloned too. PricingAnalysisApproach.CloneForAnalysis
            // remaps it in a second pass after every method in the approach has its new Id.
        };

        foreach (var l in source.ComparableLinks)
            clone._comparableLinks.Add(PricingComparableLink.CloneForMethod(l, clone.Id));

        foreach (var c in source.Calculations)
            clone._calculations.Add(PricingCalculation.CloneForMethod(c, clone.Id));

        foreach (var f in source.ComparativeFactors)
            clone._comparativeFactors.Add(PricingComparativeFactor.CloneForMethod(f, clone.Id));

        foreach (var s in source.FactorScores)
            clone._factorScores.Add(PricingFactorScore.CloneForMethod(s, clone.Id));

        foreach (var mci in source.MachineCostItems)
        {
            if (propertyIdMap is not null
                && propertyIdMap.TryGetValue(mci.AppraisalPropertyId, out var newPropId))
            {
                clone._machineCostItems.Add(MachineCostItem.CloneForMethod(mci, clone.Id, newPropId));
            }
            // else: drop — no matching new property to attach to.
        }

        if (source.FinalValue is not null)
            clone.FinalValue = PricingFinalValue.CloneForMethod(source.FinalValue, clone.Id);

        if (source.RsqResult is not null)
            clone.RsqResult = PricingRsqResult.CloneForMethod(source.RsqResult, clone.Id);

        if (source.LeaseholdAnalysis is not null)
            clone.LeaseholdAnalysis = LeaseholdAnalysis.CloneForMethod(source.LeaseholdAnalysis, clone.Id);

        if (source.ProfitRentAnalysis is not null)
            clone.ProfitRentAnalysis = ProfitRentAnalysis.CloneForMethod(source.ProfitRentAnalysis, clone.Id);

        if (source.IncomeAnalysis is not null)
            clone.IncomeAnalysis = Income.IncomeAnalysis.CloneForMethod(source.IncomeAnalysis, clone.Id);

        if (source.HypothesisAnalysis is not null)
            clone.HypothesisAnalysis = Hypothesis.HypothesisAnalysis.CloneForMethod(
                source.HypothesisAnalysis, clone.Id, propertyIdMap);

        return clone;
    }

    public PricingComparableLink LinkComparable(Guid marketComparableId, int displaySequence, decimal? weight = null)
    {
        if (_comparableLinks.Any(c => c.MarketComparableId == marketComparableId))
            throw new InvalidOperationException("Market comparable already linked");

        var link = PricingComparableLink.Create(Id, marketComparableId, displaySequence, weight);
        _comparableLinks.Add(link);
        return link;
    }

    public PricingCalculation AddCalculation(Guid marketComparableId)
    {
        var calculation = PricingCalculation.Create(Id, marketComparableId);
        _calculations.Add(calculation);
        return calculation;
    }

    public void SetValue(decimal value, decimal? valuePerUnit = null, string? unitType = null)
    {
        MethodValue = value;
        ValuePerUnit = valuePerUnit;
        UnitType = unitType;

        // Stamp the unit onto the final-value row so it survives what happens to UnitType here:
        // SetCalcMode nulls this column but leaves that row's figures standing. It lives in the
        // aggregate rather than in each save handler so the rule is stated once, for every caller.
        //
        // Only ever upgrades, never erases — the hazard is clobbering, not forgetting. No caller
        // means "the unit is now unknown": each either names a unit outright or passes
        // `method.UnitType` through to preserve it, and after a calc-mode flip that is NULL. Writing
        // it would wipe the durable answer one line before a handler reads it, which is precisely
        // what this column exists to prevent.
        if (unitType is not null)
            FinalValue?.SetFinalValueUnitType(unitType);
    }

    public void SetComparativeAnalysisTemplate(Guid? templateId)
    {
        ComparativeAnalysisTemplateId = templateId;
    }

    public void SetRemark(string? remark)
    {
        Remark = remark;
    }

    /// <summary>
    /// Flips this method's calc mode and applies the required consequences as ONE atomic
    /// operation — unselecting, clearing the recorded value, and dropping any manual land-area
    /// entry — so a caller can never set the flag without also discarding the now-stale figure it
    /// governed. This logic used to live duplicated (and inconsistently) across save handlers on
    /// an abandoned branch; it lives here instead for the same reason <c>RemoveMethod</c> nulls a
    /// sibling's <see cref="LinkedMethodId"/> itself rather than trusting every caller to do it —
    /// a rule the database cannot enforce for us must be enforced in exactly one place.
    /// <para>
    /// No-op if the mode is not actually changing, so a repeated PUT with the same value is
    /// harmless rather than re-clearing a value someone just set.
    /// </para>
    /// <para>
    /// Does not cascade to a Role=LandAndBuilding method's <see cref="LinkedMethodId"/> sibling:
    /// the flag is deliberately per-method (see the class remarks above), so an appraiser can leave
    /// one side of a linked pair on system calc while overriding only the other.
    /// </para>
    /// <para>
    /// This is for a caller with NOTHING new in hand — a user flipping a mode switch on an
    /// otherwise-untouched method. A caller that already holds a freshly computed or typed value
    /// (the automatic save handlers) must use <see cref="RecordCalcMode"/> instead: this method
    /// would immediately discard the value that caller just wrote.
    /// </para>
    /// </summary>
    public void SetCalcMode(bool useSystemCalc)
    {
        if (useSystemCalc == UseSystemCalc)
            return;

        UseSystemCalc = useSystemCalc;
        SetAsUnselected();
        ClearValue();
        FinalValue?.ExcludeLandArea();
    }

    /// <summary>
    /// Stamps how the value this method ALREADY holds was produced, without touching MethodValue,
    /// ValuePerUnit, UnitType, FinalValue, or selection. The counterpart to <see cref="SetCalcMode"/>
    /// for a caller in the opposite situation: the four automatic save handlers (SetFinalValue,
    /// UpdateFinalValue, SetManualCostBreakdown, SaveComparativeAnalysis) arrive holding the figure
    /// they just computed or received from the appraiser, in the same call — calling
    /// <see cref="SetCalcMode"/> there would clear that figure and deselect the method out from
    /// under the very save that just produced it. Two operations, not one growing a per-caller
    /// special case: "flip with nothing in hand" and "record what I just wrote" are different jobs.
    /// <para>
    /// Deliberately does not touch <see cref="PricingAnalysis.UseSystemCalc"/> (the group-level
    /// toggle) — see that property's remarks and <see cref="PricingAnalysis.ContributingMethodsUseSystemCalc"/>.
    /// </para>
    /// </summary>
    public void RecordCalcMode(bool useSystemCalc)
    {
        UseSystemCalc = useSystemCalc;
    }

    /// <summary>
    /// Clears the recorded value and its breakdown, without touching selection or calc mode.
    /// Private: the only caller is <see cref="SetCalcMode"/>.
    /// </summary>
    private void ClearValue()
    {
        MethodValue = null;
        ValuePerUnit = null;
        UnitType = null;
    }

    public void SetAsSelected()
    {
        IsSelected = true;
    }

    public void SetAsUnselected()
    {
        IsSelected = false;
    }

    private static readonly string[] ValidRoles = ["Land", "Building", "LandAndBuilding", "Machinery"];

    /// <summary>
    /// Sets this method's component within a Cost approach. Null clears it (methods outside a Cost
    /// approach, or a method reverted by <see cref="PricingAnalysisApproach.UnlinkBuildingCostMethod"/>
    /// before being re-tagged "Land").
    /// </summary>
    public void SetRole(string? role)
    {
        if (role is not null && !ValidRoles.Contains(role))
            throw new DomainException($"Role must be one of: {string.Join(", ", ValidRoles)}");

        Role = role;
    }

    /// <summary>
    /// Points this method at the BuildingCost method (in the same approach) whose value is folded
    /// into this method's own total. Null clears the link. Set by
    /// <see cref="PricingAnalysisApproach.LinkOrCreateBuildingCostMethod"/>,
    /// <see cref="PricingAnalysisApproach.UnlinkBuildingCostMethod"/>, and
    /// <see cref="PricingAnalysisApproach.RemoveMethod"/> (which nulls it on siblings of a method
    /// being deleted, since the self-referencing FK is NO ACTION and cannot do this for us).
    /// </summary>
    public void SetLinkedMethod(Guid? linkedMethodId)
    {
        LinkedMethodId = linkedMethodId;
    }

    public void SetFinalValue(PricingFinalValue finalValue)
    {
        FinalValue = finalValue;

        // Carries the unit onto a freshly attached row. A caller that attaches before pricing (the
        // BuildingCost seed, SetValue one line later) leaves it alone here and the right value
        // lands there; one that attaches after (WQS and the other calc services) gets it first
        // time. Same upgrade-only rule as SetValue, so attaching a row that already knows its unit
        // to a method that has forgotten its own cannot blank it.
        if (UnitType is not null)
            finalValue.SetFinalValueUnitType(UnitType);
    }

    /// <summary>
    /// Applies the appraiser's typed-over <see cref="PricingFinalValue.IndicatedValue"/> on top of
    /// MethodValue. No-op when there is no override — MethodValue is left as whichever figure the
    /// method's own calculation (or manual entry) just produced. Single seam for the rule every
    /// pricing save handler must apply: MethodValue = IndicatedValue ?? FinalValue. Call last, after
    /// FinalValue and its IndicatedValue are both set for this save, so the edited figure the
    /// appraiser typed always reaches the number used downstream (rollup, the book, AS400 exports).
    /// </summary>
    public void SyncMethodValueWithIndicatedValue()
    {
        if (FinalValue?.IndicatedValue is { } indicated)
            MethodValue = indicated;
    }

    /// <summary>
    /// Drops the final-value row. The relationship is required and cascades, so severing it deletes
    /// the row rather than orphaning it — which is the point: consumers treat the row's existence as
    /// "per-component figures were recorded", so a blanked-but-present row is worse than none.
    /// </summary>
    public void ClearFinalValue()
    {
        FinalValue = null;
    }

    /// <summary>
    /// Records the land area this method priced, and what that land is worth, when — and only when —
    /// this method prices land BY AREA. Single home for a rule three save handlers used to carry a
    /// copy of each (SetFinalValue, UpdateFinalValue, SaveComparativeAnalysis); it decides money that
    /// reaches the engagement's frozen CurrentValue, the LOS payload and the AS400 regulatory file,
    /// so it is stated once.
    /// <para>
    /// A per-unit RATE (PerSqWa/PerSqm) means the final value prices land per unit area, so area and
    /// value are derivable and must NOT be gated on the building-cost toggle. PerUnit is a
    /// whole-unit lumpsum carrying no land rate, so the row is left alone.
    /// </para>
    /// <para>
    /// The unit is read LIVE first and the row's own stamp only when this method has genuinely
    /// forgotten its own. Stamp-first would let a unit that has since become a lump sum be
    /// multiplied by the land area. The stamp is consulted only while the row still includes land
    /// area, because the one thing that nulls <see cref="UnitType"/> — <see cref="SetCalcMode"/> →
    /// <see cref="ClearValue"/> — also calls <see cref="PricingFinalValue.ExcludeLandArea"/> in the
    /// same operation; reading it unconditionally would let the next save recompute the figures and
    /// flip IncludeLandArea back to true, quietly undoing an exclusion the appraiser asked for.
    /// </para>
    /// <para>
    /// The appraiser's typed-over rate wins over the calculated one: <see cref="ValuePerUnit"/> is
    /// whatever the calc service produced, and <see cref="PricingFinalValue.FinalValueOverride"/>
    /// exists precisely to replace it — reading it second meant a saved override never reached the
    /// land value at all. An explicit <paramref name="explicitLandValue"/> still wins over both; the
    /// cost approach enters that figure by hand.
    /// </para>
    /// </summary>
    /// <param name="landAreaFromTitles">
    /// Area from the property's land titles — authoritative, never taken from the request.
    /// </param>
    /// <param name="explicitLandValue">A land value supplied by the caller, or null to derive one.</param>
    /// <param name="includeLandArea">
    /// The appraiser's own answer to "does this method price land at all" — false clears the area and
    /// value outright. Null means the request did not say, which is not the same as false: the save
    /// DTOs default it, so an ordinary save must not read silence as a decision to exclude.
    /// </param>
    public void ApplyLandAreaValue(
        decimal landAreaFromTitles,
        decimal? explicitLandValue,
        bool? includeLandArea)
    {
        if (FinalValue is null)
            return;

        if (includeLandArea == false)
        {
            FinalValue.ExcludeLandArea();
            return;
        }

        if (landAreaFromTitles <= 0m)
            return;

        var unit = UnitType ?? (FinalValue.IncludeLandArea ? FinalValue.FinalValueUnitType : null);
        if (!PricingUnit.IsPerUnitRate(unit))
            return;

        var rate = FinalValue.FinalValueOverride ?? ValuePerUnit;
        var landValue = explicitLandValue
                        ?? (rate.HasValue ? landAreaFromTitles * rate.Value : (decimal?)null);

        if (landValue.HasValue)
            FinalValue.SetLandAreaValues(landAreaFromTitles, landValue.Value);
    }

    /// <summary>
    /// Mirrors the current MachineCostItems FMV total into the shared <see cref="FinalValue"/>,
    /// creating it if absent. User-authored fields
    /// (FinalValueOverride / IndicatedValue) are deliberately left untouched. Call AFTER recalculation
    /// so the items hold current values. Single source of the MachineryCost mirror formula — shared by
    /// the save path (SaveMachineCostItemsCommandHandler) and the property-delete cleanup path
    /// (PricingReferenceCleanupService).
    /// </summary>
    public void MirrorMachineCostTotalToFinalValue()
    {
        var totalFmv = _machineCostItems.Sum(i => i.FairMarketValue ?? 0);

        if (FinalValue is null)
            SetFinalValue(PricingFinalValue.Create(Id, totalFmv));
        else
            FinalValue.UpdateFinalValue(totalFmv);

        // The only writer of a final value that never routes through SetValue, so it is the only
        // one that has to name the unit itself. Machinery is always a whole-unit lumpsum (a sum of
        // per-machine FMV) — said outright rather than left null and relying on null happening to
        // read as lumpsum. Stamps the ROW only: UnitType is this method's own column and this
        // operation's contract is to mirror the FMV total, not to decide the method's price unit.
        // The literal matches PricingAnalysisApproach.cs's BuildingCost seed; PricingUnit.PerUnit
        // is reachable (same assembly) but the two sibling writers should read alike.
        FinalValue.SetFinalValueUnitType("PerUnit");
    }

    public void SetRsqResult(PricingRsqResult rsqResult)
    {
        RsqResult = rsqResult;
    }

    public void SetLeaseholdAnalysis(LeaseholdAnalysis analysis)
    {
        LeaseholdAnalysis = analysis;
    }

    public void ClearLeaseholdAnalysis()
    {
        LeaseholdAnalysis = null;
    }

    public void SetProfitRentAnalysis(ProfitRentAnalysis analysis)
    {
        ProfitRentAnalysis = analysis;
    }

    public void ClearProfitRentAnalysis()
    {
        ProfitRentAnalysis = null;
    }

    public void SetIncomeAnalysis(IncomeAnalysis analysis)
    {
        IncomeAnalysis = analysis;
    }

    public void ClearIncomeAnalysis()
    {
        IncomeAnalysis = null;
    }

    public void SetHypothesisAnalysis(HypothesisAnalysis analysis)
    {
        HypothesisAnalysis = analysis;
    }

    public void ClearHypothesisAnalysis()
    {
        HypothesisAnalysis = null;
    }

    /// <summary>
    /// Removes a comparable link from this method
    /// </summary>
    public void RemoveComparableLink(Guid linkId)
    {
        var link = _comparableLinks.FirstOrDefault(l => l.Id == linkId);
        if (link is null)
            throw new InvalidOperationException($"Comparable link with ID {linkId} not found.");

        _comparableLinks.Remove(link);
    }

    /// <summary>
    /// Removes a calculation by market-comparable ID
    /// </summary>
    public void RemoveCalculationByComparableId(Guid marketComparableId)
    {
        var calculation = _calculations.FirstOrDefault(c => c.MarketComparableId == marketComparableId);
        if (calculation is not null) _calculations.Remove(calculation);
    }

    /// <summary>
    /// Removes all factor scores for a specific market comparable
    /// </summary>
    public void RemoveFactorScoresByComparableId(Guid marketComparableId)
    {
        var scores = _factorScores.Where(f => f.MarketComparableId == marketComparableId).ToList();
        foreach (var score in scores) _factorScores.Remove(score);
    }

    #region Comparative Factor Methods (Step 1)

    /// <summary>
    /// Adds a comparative factor for Step 1 factor selection.
    /// </summary>
    public PricingComparativeFactor AddComparativeFactor(
        Guid factorId,
        int displaySequence,
        bool isSelectedForScoring = false,
        string? remarks = null,
        string? collateralValue = null)
    {
        if (_comparativeFactors.Any(f => f.FactorId == factorId))
            throw new InvalidOperationException($"Factor {factorId} already exists in comparative factors");

        var factor = PricingComparativeFactor.Create(Id, factorId, displaySequence, isSelectedForScoring, remarks, collateralValue);
        _comparativeFactors.Add(factor);
        return factor;
    }

    /// <summary>
    /// Gets a comparative factor by ID
    /// </summary>
    public PricingComparativeFactor? GetComparativeFactor(Guid id)
    {
        return _comparativeFactors.FirstOrDefault(f => f.Id == id);
    }

    /// <summary>
    /// Removes a comparative factor by ID
    /// </summary>
    public void RemoveComparativeFactor(Guid id)
    {
        var factor = _comparativeFactors.FirstOrDefault(f => f.Id == id);
        if (factor is not null)
            _comparativeFactors.Remove(factor);
    }

    /// <summary>
    /// Clears all comparative factors
    /// </summary>
    public void ClearComparativeFactors()
    {
        _comparativeFactors.Clear();
    }

    #endregion

    #region Factor Score Methods (Step 2)

    /// <summary>
    /// Adds a factor score for Step 2 scoring
    /// </summary>
    public PricingFactorScore AddFactorScore(
        Guid factorId,
        decimal factorWeight,
        int displaySequence,
        Guid? marketComparableId = null)
    {
        // Allow same factor for different comparables
        if (_factorScores.Any(f => f.FactorId == factorId && f.MarketComparableId == marketComparableId))
            throw new InvalidOperationException(
                $"Factor {factorId} for comparable {marketComparableId} already exists");

        var score = PricingFactorScore.Create(Id, factorId, factorWeight, displaySequence, marketComparableId);
        _factorScores.Add(score);
        return score;
    }

    /// <summary>
    /// Gets a factor score by ID
    /// </summary>
    public PricingFactorScore? GetFactorScore(Guid id)
    {
        return _factorScores.FirstOrDefault(f => f.Id == id);
    }

    /// <summary>
    /// Removes a factor score by ID
    /// </summary>
    public void RemoveFactorScore(Guid id)
    {
        var score = _factorScores.FirstOrDefault(f => f.Id == id);
        if (score is not null)
            _factorScores.Remove(score);
    }

    /// <summary>
    /// Clears all factor scores
    /// </summary>
    public void ClearFactorScores()
    {
        _factorScores.Clear();
    }

    /// <summary>
    /// Clears all calculations
    /// </summary>
    public void ClearCalculations()
    {
        _calculations.Clear();
    }

    /// <summary>
    /// Clears all comparable links
    /// </summary>
    public void ClearComparableLinks()
    {
        _comparableLinks.Clear();
    }

    #region Machine Cost Item Methods

    public MachineCostItem AddMachineCostItem(Guid appraisalPropertyId, int displaySequence)
    {
        if (_machineCostItems.Any(i => i.AppraisalPropertyId == appraisalPropertyId))
            throw new InvalidOperationException("Machine cost item already exists for this property");

        var item = MachineCostItem.Create(Id, appraisalPropertyId, displaySequence);
        _machineCostItems.Add(item);
        return item;
    }

    public void RemoveMachineCostItem(Guid itemId)
    {
        var item = _machineCostItems.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
            _machineCostItems.Remove(item);
    }

    public void ClearMachineCostItems()
    {
        _machineCostItems.Clear();
    }

    #endregion

    /// <summary>
    /// Resets all method data: factors, scores, calculations, links, final value, and RSQ result
    /// </summary>
    public void Reset()
    {
        ClearComparativeFactors();
        ClearFactorScores();
        ClearCalculations();
        ClearComparableLinks();
        ClearMachineCostItems();
        FinalValue = null;
        RsqResult = null;
        ClearLeaseholdAnalysis();
        ClearProfitRentAnalysis();
        ClearIncomeAnalysis();
        ClearHypothesisAnalysis();
        MethodValue = null;
        ValuePerUnit = null;
        UnitType = null;
        IsSelected = false;
    }

    /// <summary>
    /// Gets factor scores for a specific comparable (or collateral if null)
    /// </summary>
    public IEnumerable<PricingFactorScore> GetFactorScoresForComparable(Guid? marketComparableId)
    {
        return _factorScores.Where(f => f.MarketComparableId == marketComparableId)
            .OrderBy(f => f.DisplaySequence);
    }

    #endregion
}