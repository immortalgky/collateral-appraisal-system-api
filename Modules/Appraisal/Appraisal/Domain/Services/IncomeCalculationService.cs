using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Income;
using Appraisal.Domain.Appraisals.Income.MethodDetails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parameter.Contracts.PricingParameters;

namespace Appraisal.Domain.Services;

/// <summary>
/// Server-side calculation engine for income-approach (DCF + Direct) pricing analysis.
/// Mirrors the TypeScript derived-rules in buildDiscountedCashFlowDerivedRules.ts exactly.
/// Use decimal throughout — no double arithmetic.
/// </summary>
public class IncomeCalculationService : IPricingCalculationService
{
    private readonly ILogger<IncomeCalculationService> _logger;

    public IncomeCalculationService() : this(NullLogger<IncomeCalculationService>.Instance) { }

    public IncomeCalculationService(ILogger<IncomeCalculationService> logger)
    {
        _logger = logger;
    }

    // ── IPricingCalculationService ────────────────────────────────────────

    /// <summary>
    /// Thin bridge for the PricingCalculationServiceResolver dispatch path.
    /// Pulls IncomeAnalysis from the method, calculates, then applies the result.
    /// No tax-bracket derivation on this path (no DB access available here).
    /// </summary>
    public void Recalculate(PricingAnalysisMethod method)
    {
        if (method.IncomeAnalysis is null)
            return;

        var result = Calculate(method.IncomeAnalysis);
        method.IncomeAnalysis.ApplyCalculationResult(result);
    }

    // ── public Calculate ──────────────────────────────────────────────────

    /// <summary>
    /// Calculates the income analysis without bracket derivation (uses client-supplied tax values).
    /// Prefer the overload that accepts <paramref name="taxBrackets"/> for server-authoritative results.
    /// </summary>
    public IncomeCalculationResult Calculate(IncomeAnalysis analysis)
        => Calculate(analysis, null);

    /// <summary>
    /// Calculates the income analysis.
    /// When <paramref name="taxBrackets"/> is non-null and non-empty, Method-10 tax values are
    /// derived server-side from <c>TotalPropertyPrice</c> using the seeded brackets (flat-rate
    /// lookup: find the matching bracket, multiply entire price by that bracket's rate).
    /// When null or empty the client-supplied <c>TotalPropertyTax</c> array is used as-is
    /// (backward-compatible path; a warning is logged).
    /// <para>
    /// <c>FinalValueRounded</c> in the result is always server-computed as
    /// <c>round(finalValue / 1000, MidpointRounding.AwayFromZero) × 1000</c>.
    /// User-adjustable values are handled separately via <see cref="IncomeAnalysis.FinalValueAdjust"/>.
    /// </para>
    /// </summary>
    public IncomeCalculationResult Calculate(
        IncomeAnalysis analysis,
        IReadOnlyList<TaxBracketDto>? taxBrackets
        )
    {
        var years = analysis.TotalNumberOfYears;
        var daysInYear = analysis.TotalNumberOfDayInYear;

        var hasBrackets = taxBrackets is { Count: > 0 };
        if (!hasBrackets)
        {
            _logger.LogWarning(
                "IncomeCalculationService.Calculate called without tax brackets. " +
                "Method-10 will use client-supplied TotalPropertyTax values. " +
                "Pass brackets from GetPricingTaxBracketsQuery for server-authoritative results.");
        }

        // ── Pass 1: compute all non-method-13 methods ──────────────────────
        var methodValues = new Dictionary<Guid, decimal[]>();

        // We need cross-method refs for method 08 (refs method-01) and method 11 (refs method-06).
        // Collect those after the first pass.
        var deferred13 = new List<(IncomeAssumption assumption, Method13Detail detail)>();

        // Collect totalSaleableAreaDeductByOccRate arrays by method type for cross-ref.
        // Key: methodTypeCode -> first computed array found.
        var crossRefSaleableAreaOccRate = new Dictionary<string, decimal[]>();

        foreach (var section in analysis.Sections)
        {
            foreach (var category in section.Categories)
            {
                foreach (var assumption in category.Assumptions)
                {
                    var code = assumption.Method.MethodTypeCode;
                    var detailJson = assumption.Method.DetailJson;

                    if (code == "13")
                    {
                        // Defer until all other methods are computed.
                        var d13 = MethodDetailSerializer.Deserialize<Method13Detail>(code, detailJson);
                        deferred13.Add((assumption, d13));
                        continue;
                    }

                    decimal[] values = ComputeMethod(code, detailJson, years, daysInYear, crossRefSaleableAreaOccRate, taxBrackets);
                    methodValues[assumption.Id] = values;
                }
            }
        }

        // ── Pass 2: resolve method 13 (proportional) — iterative to convergence ──
        // Each iteration: aggregate categories/sections using current values, then
        // re-resolve every M13 against those aggregates. When an M13 references an
        // aggregate that itself contains other M13s (e.g. Admin Fee = 5% of GOP, and
        // GOP = Income − Expenses where Expenses contains multiple M13s), a single
        // pass uses stale (pre-M13) aggregates and produces wrong values.
        // A fixed-point iteration converges in ≤3 passes for any non-circular ref graph.

        var assumptionValues = new Dictionary<Guid, decimal[]>(methodValues);
        var categoryValues = AggregateCategoryValues(analysis, assumptionValues, years);
        var sectionValues = AggregateSectionValues(analysis, categoryValues, assumptionValues, years);

        const int MaxIterations = 5;
        bool didNotConverge = false;
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            bool anyChanged = false;
            foreach (var (assumption, detail) in deferred13)
            {
                var refValues = ResolveRefTarget(detail, sectionValues, categoryValues, assumptionValues, years);
                decimal[] values = new decimal[years];
                for (int y = 0; y < years; y++)
                    values[y] = (detail.ProportionPct / 100m) * (refValues != null ? refValues[y] : 0m);

                if (!assumptionValues.TryGetValue(assumption.Id, out var prev) || !ArraysEqual(prev, values))
                    anyChanged = true;

                methodValues[assumption.Id] = values;
                assumptionValues[assumption.Id] = values;
            }

            categoryValues = AggregateCategoryValues(analysis, assumptionValues, years);
            sectionValues = AggregateSectionValues(analysis, categoryValues, assumptionValues, years);

            if (!anyChanged) break;
            didNotConverge = iter == MaxIterations - 1;
        }

        if (didNotConverge)
            _logger.LogWarning(
                "Method-13 iteration did not converge after {MaxIterations} passes — possible circular reference or proportion >= 100%. Result may be inaccurate. AnalysisId={AnalysisId}.",
                MaxIterations, analysis.Id);

        // ── Summary (DCF pipeline) ─────────────────────────────────────────
        var contractRentalFee = ParseJsonArray(analysis.Summary.ContractRentalFeeJson, years);
        var (grossRevenue, grossRevenueProportional) = ComputeGrossRevenue(
            analysis, sectionValues, contractRentalFee, years);

        var (terminalRevenue, totalNet, discount, presentValue, finalValue) =
            ComputeDcfSummary(analysis, grossRevenue);

        // Always compute FinalValueRounded as round-to-nearest-1000 (half-up).
        // roundToThousand(500) === 1000, roundToThousand(499) === 0.
        var finalValueRounded = Math.Round(finalValue / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        return new IncomeCalculationResult
        {
            MethodValues = methodValues,
            AssumptionValues = assumptionValues,
            CategoryValues = categoryValues,
            SectionValues = sectionValues,
            ContractRentalFee = contractRentalFee,
            GrossRevenue = grossRevenue,
            GrossRevenueProportional = grossRevenueProportional,
            TerminalRevenue = terminalRevenue,
            TotalNet = totalNet,
            Discount = discount,
            PresentValue = presentValue,
            FinalValue = finalValue,
            FinalValueRounded = finalValueRounded
        };
    }
    
    public IncomeCalculationResult Calculate(
        IncomeAnalysis analysis,
        IReadOnlyList<TaxBracketDto>? taxBrackets,
        decimal[]? contractRentalFeeOverride)
    {
        var years = analysis.TotalNumberOfYears;
        var daysInYear = analysis.TotalNumberOfDayInYear;

        var hasBrackets = taxBrackets is { Count: > 0 };
        if (!hasBrackets)
        {
            _logger.LogWarning(
                "IncomeCalculationService.Calculate called without tax brackets. " +
                "Method-10 will use client-supplied TotalPropertyTax values. " +
                "Pass brackets from GetPricingTaxBracketsQuery for server-authoritative results.");
        }

        // ── Pass 1: compute all non-method-13 methods ──────────────────────
        var methodValues = new Dictionary<Guid, decimal[]>();

        // We need cross-method refs for method 08 (refs method-01) and method 11 (refs method-06).
        // Collect those after the first pass.
        var deferred13 = new List<(IncomeAssumption assumption, Method13Detail detail)>();

        // Collect totalSaleableAreaDeductByOccRate arrays by method type for cross-ref.
        // Key: methodTypeCode -> first computed array found.
        var crossRefSaleableAreaOccRate = new Dictionary<string, decimal[]>();

        foreach (var section in analysis.Sections)
        {
            foreach (var category in section.Categories)
            {
                foreach (var assumption in category.Assumptions)
                {
                    var code = assumption.Method.MethodTypeCode;
                    var detailJson = assumption.Method.DetailJson;

                    if (code == "13")
                    {
                        // Defer until all other methods are computed.
                        var d13 = MethodDetailSerializer.Deserialize<Method13Detail>(code, detailJson);
                        deferred13.Add((assumption, d13));
                        continue;
                    }

                    decimal[] values = ComputeMethod(code, detailJson, years, daysInYear, crossRefSaleableAreaOccRate, taxBrackets);
                    methodValues[assumption.Id] = values;
                }
            }
        }

        // ── Pass 2: resolve method 13 (proportional) — iterative to convergence ──
        // Each iteration: aggregate categories/sections using current values, then
        // re-resolve every M13 against those aggregates. When an M13 references an
        // aggregate that itself contains other M13s (e.g. Admin Fee = 5% of GOP, and
        // GOP = Income − Expenses where Expenses contains multiple M13s), a single
        // pass uses stale (pre-M13) aggregates and produces wrong values.
        // A fixed-point iteration converges in ≤3 passes for any non-circular ref graph.

        var assumptionValues = new Dictionary<Guid, decimal[]>(methodValues);
        var categoryValues = AggregateCategoryValues(analysis, assumptionValues, years);
        var sectionValues = AggregateSectionValues(analysis, categoryValues, assumptionValues, years);

        const int MaxIterations = 5;
        bool didNotConverge = false;
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            bool anyChanged = false;
            foreach (var (assumption, detail) in deferred13)
            {
                var refValues = ResolveRefTarget(detail, sectionValues, categoryValues, assumptionValues, years);
                decimal[] values = new decimal[years];
                for (int y = 0; y < years; y++)
                {
                    if (detail.StartIn > 1 && y < detail.StartIn - 1)
                    {
                        values[y] = 0;
                        continue;
                    }

                    values[y] = (detail.ProportionPct / 100m) * (refValues != null ? refValues[y] : 0m);
                } 
                   

                if (!assumptionValues.TryGetValue(assumption.Id, out var prev) || !ArraysEqual(prev, values))
                    anyChanged = true;

                methodValues[assumption.Id] = values;
                assumptionValues[assumption.Id] = values;
            }

            categoryValues = AggregateCategoryValues(analysis, assumptionValues, years);
            sectionValues = AggregateSectionValues(analysis, categoryValues, assumptionValues, years);

            if (!anyChanged) break;
            didNotConverge = iter == MaxIterations - 1;
        }

        if (didNotConverge)
            _logger.LogWarning(
                "Method-13 iteration did not converge after {MaxIterations} passes — possible circular reference or proportion >= 100%. Result may be inaccurate. AnalysisId={AnalysisId}.",
                MaxIterations, analysis.Id);

        // ── Summary (DCF pipeline) ─────────────────────────────────────────
        var contractRentalFee = contractRentalFeeOverride is { Length: > 0 }
            ? NormalizeLength(contractRentalFeeOverride, years)
            : ParseJsonArray(analysis.Summary.ContractRentalFeeJson, years);
        var (grossRevenue, grossRevenueProportional) = ComputeGrossRevenue(
            analysis, sectionValues, contractRentalFee, years);

        var (terminalRevenue, totalNet, discount, presentValue, finalValue) =
            ComputeDcfSummary(analysis, grossRevenue);

        // Always compute FinalValueRounded as round-to-nearest-1000 (half-up).
        // roundToThousand(500) === 1000, roundToThousand(499) === 0.
        var finalValueRounded = Math.Round(finalValue / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        return new IncomeCalculationResult
        {
            MethodValues = methodValues,
            AssumptionValues = assumptionValues,
            CategoryValues = categoryValues,
            SectionValues = sectionValues,
            ContractRentalFee = contractRentalFee,
            GrossRevenue = grossRevenue,
            GrossRevenueProportional = grossRevenueProportional,
            TerminalRevenue = terminalRevenue,
            TotalNet = totalNet,
            Discount = discount,
            PresentValue = presentValue,
            FinalValue = finalValue,
            FinalValueRounded = finalValueRounded
        };
    }

    // ── Dispatch ──────────────────────────────────────────────────────────

    private static decimal[] ComputeMethod(
        string code,
        string detailJson,
        int years,
        int daysInYear,
        Dictionary<string, decimal[]> crossRef,
        IReadOnlyList<TaxBracketDto>? taxBrackets = null)
    {
        return code switch
        {
            "01" => ComputeMethod01(MethodDetailSerializer.Deserialize<Method01Detail>(code, detailJson), years, daysInYear, crossRef),
            "02" => ComputeMethod02(MethodDetailSerializer.Deserialize<Method02Detail>(code, detailJson), years, daysInYear, crossRef),
            "03" => ComputeMethod03(MethodDetailSerializer.Deserialize<Method03Detail>(code, detailJson), years),
            "04" => ComputeMethod04(MethodDetailSerializer.Deserialize<Method04Detail>(code, detailJson), years),
            "05" => ComputeMethod05(MethodDetailSerializer.Deserialize<Method05Detail>(code, detailJson), years),
            "06" => ComputeMethod06(MethodDetailSerializer.Deserialize<Method06Detail>(code, detailJson), years, crossRef),
            "07" => ComputeMethod07(MethodDetailSerializer.Deserialize<Method07Detail>(code, detailJson), years),
            "08" => ComputeMethod08(MethodDetailSerializer.Deserialize<Method08Detail>(code, detailJson), years, crossRef),
            "09" => ComputeMethod09(MethodDetailSerializer.Deserialize<Method09Detail>(code, detailJson), years),
            "10" => ComputeMethod10(MethodDetailSerializer.Deserialize<Method10Detail>(code, detailJson), years, taxBrackets),
            "11" => ComputeMethod11(MethodDetailSerializer.Deserialize<Method11Detail>(code, detailJson), years, crossRef),
            "12" => ComputeMethod12(MethodDetailSerializer.Deserialize<Method12Detail>(code, detailJson), years),
            "14" => ComputeMethod14(MethodDetailSerializer.Deserialize<Method14Detail>(code, detailJson), years),
            _ => new decimal[years]
        };
    }

    // ── Method calculators ────────────────────────────────────────────────

    /// <summary>
    /// Method 01 — Room Income Per Day.
    /// saleableArea[y] = sumSaleableArea × daysInYear (constant)
    /// occupancyRate[0] = firstYearPct; steps by +occupancyRatePct every occupancyRateYrs, clamp at 100
    /// avgDailyRate[0] = avgRoomRate; compounds by increaseRatePct every increaseRateYrs
    /// roomIncome[y] = saleableArea[y] × (occupancyRate[y]/100) × avgDailyRate[y]
    /// totalMethodValues[y] = roomIncome[y]
    /// note: occupancyRate can adjust year by year, so we compute it by using occupancyRate from frontend if provided (>= 0), otherwise use the step-compounded one. 
    /// </summary>
    private static decimal[] ComputeMethod01(
        Method01Detail d,
        int years,
        int daysInYear,
        Dictionary<string, decimal[]> crossRef)
    {
      var saleableAreaConst = d.SumSaleableArea * daysInYear;
      var occupancyRate = ComputeOccupancyRate(d.OccupancyRateFirstYearPct, d.OccupancyRatePct, d.OccupancyRateYrs, years);
      var avgDailyRate = ComputeStepCompoundingArray(d.AvgRoomRate, d.IncreaseRatePct, d.IncreaseRateYrs, years);

      var result = new decimal[years];
      var totalSaleableAreaDeductByOccRate = new decimal[years];

      for (int y = 0; y < years; y++)
      {
        // If year less than `startIn`, result will be 0;
        if (d.StartIn > 1 && y < d.StartIn - 1)
        {
          result[y] = 0;
          totalSaleableAreaDeductByOccRate[y] = 0;
          continue;
        }

        var curOccupancyRate = d.OccupancyRate[y] >= 0 ? d.OccupancyRate[y] : occupancyRate[y];
        var saleableAreaDeductByOcc = saleableAreaConst * (curOccupancyRate / 100m);
        totalSaleableAreaDeductByOccRate[y] = saleableAreaDeductByOcc;
        result[y] = Math.Round(saleableAreaDeductByOcc * avgDailyRate[y], 2);
      }

      // Store for method 08 cross-reference.
      crossRef.TryAdd("01", totalSaleableAreaDeductByOccRate);

      // Store for method 11 cross-reference.
      var saleableAreaOccRate = totalSaleableAreaDeductByOccRate;
      crossRef.TryAdd("06", saleableAreaOccRate);

      return result;
    }

    /// <summary>
    /// Method 02 — Room Income By Seasonal Rates.
    /// The TS code is identical to method 01 in its derived-rules — avgRoomRate drives avgDailyRate.
    /// The seasonal detail is pre-computed into avgRoomRate in the frontend; we honour that here.
    /// saleableArea uses totalSaleableArea (not sumSaleableArea).
    /// note: occupancyRate can adjust year by year, so we compute it by using occupancyRate from frontend if provided (>= 0), otherwise use the step-compounded one. 
    /// </summary>
    private static decimal[] ComputeMethod02(
        Method02Detail d,
        int years,
        int daysInYear,
        Dictionary<string, decimal[]> crossRef)
    {
        var saleableAreaConst = d.TotalSaleableArea * daysInYear;
        var occupancyRate = ComputeOccupancyRate(d.OccupancyRateFirstYearPct, d.OccupancyRatePct, d.OccupancyRateYrs, years);
        var avgDailyRate = ComputeStepCompoundingArray(d.AvgRoomRate, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        var result = new decimal[years];
        var totalSaleableAreaDeductByOccRate = new decimal[years];

        for (int y = 0; y < years; y++)
        {
            // If year less than `startIn`, result will be 0;
            if (d.StartIn > 1 && y < d.StartIn - 1)
            {
                result[y] = 0;
                totalSaleableAreaDeductByOccRate[y] = 0;
                continue;
            }

            var curOccupancyRate = d.OccupancyRate[y] >= 0 ? d.OccupancyRate[y] : occupancyRate[y];

            var saleableAreaDeductByOcc = saleableAreaConst * (curOccupancyRate / 100m);
            totalSaleableAreaDeductByOccRate[y] = saleableAreaDeductByOcc;
            result[y] = Math.Round(saleableAreaDeductByOcc * avgDailyRate[y], 2);
        }

        // Also register as "01" for method-08 cross-reference if no method-01 exists.
        crossRef.TryAdd("01", totalSaleableAreaDeductByOccRate);

        // Store for method 11 cross-reference.
        var saleableAreaOccRate = totalSaleableAreaDeductByOccRate;
        crossRef.TryAdd("06", saleableAreaOccRate);

        return result;
    }

    /// <summary>
    /// Method 03 — Room Income With Growth.
    /// Iterative step compounding matching TS: prevValue × (1 + growthRate/100) on growth years.
    /// </summary>
    private static decimal[] ComputeMethod03(Method03Detail d, int years)
    {
      var result = ComputeStepCompoundingArray(d.FirstYearAmt, d.IncreaseRatePct, d.IncreaseRateYrs, years);

      if (d.StartIn > 1 && d.StartIn > 1)
      {
        // use 0-base year
        result = ShiftRight(result, d.StartIn - 1);
      }

      return result;
    }

    /// <summary>
    /// Method 04 — Room Income With Growth × Occupancy Rate.
    /// adjusted[y] = step-compounding of firstYearAmt
    /// roomIncome[y] = adjusted[y] × (occupancyRate[y] / 100)
    /// note: occupancyRate can adjust year by year, so we compute it by using occupancyRate from frontend if provided (>= 0), otherwise use the step-compounded one. 
    /// </summary>
    private static decimal[] ComputeMethod04(Method04Detail d, int years)
    {
        var adjusted = ComputeStepCompoundingArray(d.FirstYearAmt, d.IncreaseRatePct, d.IncreaseRateYrs, years);
        var occupancyRate = ComputeOccupancyRate(d.OccupancyRateFirstYearPct, d.OccupancyRatePct, d.OccupancyRateYrs, years);

        var result = new decimal[years];
        for (int y = 0; y < years; y++)
        {
            // If year less than `startIn`, result will be 0;
            if (d.StartIn > 1 && y < d.StartIn - 1)
            {
                result[y] = 0;
                continue;
            }
            var curOccupancyRate = d.OccupancyRate[y] >= 0 ? d.OccupancyRate[y] : occupancyRate[y];

            result[y] = Math.Round(adjusted[y] * (curOccupancyRate / 100m), 2);
        }

        return result;
    }

    /// <summary>
    /// Method 05 — Rental Income Per Month (monthly × 12).
    /// Base = sumRoomIncomePerYear; step-compounding each growth year.
    /// </summary>
    private static decimal[] ComputeMethod05(Method05Detail d, int years)
    {
      var result = ComputeStepCompoundingArray(d.SumRoomIncomePerYear, d.IncreaseRatePct, d.IncreaseRateYrs, years);

      if (d.StartIn > 1)
      {
        // use 0-base year
        result = ShiftRight(result, d.StartIn - 1);
      }

      return result;
    }

    /// <summary>
    /// Method 06 — Rental Income Per Square Meter.
    /// occupancyRate: step addition (same as method 01 occupancy)
    /// avgRentalRate[y]: step compounding starting from avgRentalRatePerMonth
    /// totalRentalIncome[y] = avgRentalRate[y] × (sumSaleableArea × occupancyRate[y]/100) × 12
    /// totalMethodValues[y] = totalRentalIncome[y]
    /// note: occupancyRate can adjust year by year, so we compute it by using occupancyRate from frontend if provided (>= 0), otherwise use the step-compounded one. 
    /// </summary>
    private static decimal[] ComputeMethod06(
        Method06Detail d,
        int years,
        Dictionary<string, decimal[]> crossRef)
    {
        var occupancyRate = ComputeOccupancyRate(d.OccupancyRateFirstYearPct, d.OccupancyRatePct, d.OccupancyRateYrs, years);
        var avgRentalRate = ComputeStepCompoundingArray(d.AvgRentalRatePerMonth, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        var result = new decimal[years];
        var totalSaleableAreaDeductByOccRate = new decimal[years];

        for (int y = 0; y < years; y++)
        {
            // If year less than `startIn`, result will be 0;
            if (d.StartIn > 1 && y < d.StartIn - 1)
            {
                result[y] = 0;
                totalSaleableAreaDeductByOccRate[y] = 0;
                continue;
            }
            var curOccupancyRate = d.OccupancyRate[y] >= 0 ? d.OccupancyRate[y] : occupancyRate[y];

            var saleableAreaDeductByOcc = d.SumSaleableArea * (curOccupancyRate / 100m);
            totalSaleableAreaDeductByOccRate[y] = saleableAreaDeductByOcc;
            result[y] = Math.Round(avgRentalRate[y] * saleableAreaDeductByOcc * 12m, 2);
        }

        // Store for method 11 cross-reference.
        var saleableAreaOccRate = totalSaleableAreaDeductByOccRate;
        crossRef.TryAdd("06", saleableAreaOccRate);

        // Also register as "01" for method-08 cross-reference if no method-01 or method-02 exists.
        crossRef.TryAdd("01", totalSaleableAreaDeductByOccRate);

        return result;
    }

    /// <summary>
    /// Method 07 — Room Cost Per Room Per Day.
    /// Base = sumTotalRoomExpensePerYear; step compounding each growth year.
    /// </summary>
    private static decimal[] ComputeMethod07(Method07Detail d, int years)
    {
        var result = ComputeStepCompoundingArray(d.SumTotalRoomExpensePerYear, d.IncreaseRatePct, d.IncreaseRateYrs, years);
        
        if (d.StartIn > 1)
        {
            // use 0-base year
            result = ShiftRight(result, d.StartIn - 1);
        }

        return result;
    }

    /// <summary>
    /// Method 08 — F&amp;B Expense Per Room Per Day.
    /// totalFoodAndBeveragePerRoomPerDay[y]: step compounding of firstYearAmt
    /// totalMethodValues[y] = totalFoodAndBeveragePerRoomPerDay[y] × totalSaleableAreaDeductByOccRate[y]
    /// where totalSaleableAreaDeductByOccRate comes from the first method-01 (or method-02, method-06) in the analysis.
    /// </summary>
    private static decimal[] ComputeMethod08(
        Method08Detail d,
        int years,
        Dictionary<string, decimal[]> crossRef)
    {
        var perDay = ComputeStepCompoundingArray(d.FirstYearAmt, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        crossRef.TryGetValue("01", out var saleableAreaOccRate);

        var result = new decimal[years];
        for (int y = 0; y < years; y++)
        {
            // If year less than `startIn`, result will be 0;
            if (d.StartIn > 1 && y < d.StartIn - 1)
            {
                result[y] = 0;
                continue;
            }

            var areaOccRate = saleableAreaOccRate != null ? saleableAreaOccRate[y] : 0m;
            result[y] = Math.Round(perDay[y] * areaOccRate, 2);
        }
        return result;
    }

    /// <summary>
    /// Method 09 — Position-Based Salary.
    /// sumTotalSalaryPerYear = Σ(salaryPerPersonPerMonth × numberOfEmployees × 12) — pre-computed in detail.
    /// Step compounding: prevValue × (1 + increaseRate/100) each growth year.
    /// </summary>
    private static decimal[] ComputeMethod09(Method09Detail d, int years)
    {
        var result = ComputeStepCompoundingArray(d.SumTotalSalaryPerYear, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        if (d.StartIn > 1)
        {
            // use 0-base year
            result = ShiftRight(result, d.StartIn - 1);
        }

        return result;
    }

    /// <summary>
    /// Method 10 — Property Tax By Tiered Brackets.
    /// When <paramref name="taxBrackets"/> is provided (non-null, non-empty), the tax is derived
    /// server-side from <c>TotalPropertyPrice[y]</c> using a flat-rate bracket lookup:
    /// find the matching bracket, multiply the whole price by that bracket's rate.
    /// This matches the frontend logic in getPropertyTaxAmount() exactly.
    /// When brackets are absent, falls back to the client-supplied <c>TotalPropertyTax</c> array.
    /// </summary>
    private static decimal[] ComputeMethod10(
        Method10Detail d,
        int years,
        IReadOnlyList<TaxBracketDto>? taxBrackets)
    {
        var result = new decimal[years];

        if (taxBrackets is { Count: > 0 })
        {
            // Server-authoritative path: derive from TotalPropertyPrice using bracket lookup.
            for (int y = 0; y < years; y++)
            {
                var price = d.PropertyTax.TotalPropertyPrice.Length > y
                    ? d.PropertyTax.TotalPropertyPrice[y]
                    : (d.PropertyTax.TotalPropertyPrice.Length > 0
                        ? d.PropertyTax.TotalPropertyPrice[^1]
                        : 0m);

                result[y] = DerivePropertyTax(price, taxBrackets);
            }
        }
        else
        {
            // Fallback: use client-supplied array (backward-compatible, logged at call site).
            for (int y = 0; y < years; y++)
            {
                if (d.PropertyTax.TotalPropertyTax.Length > y)
                    result[y] = d.PropertyTax.TotalPropertyTax[y];
                else if (d.PropertyTax.TotalPropertyTax.Length > 0)
                    result[y] = d.PropertyTax.TotalPropertyTax[^1];
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates property tax using a progressive bracket method: each portion of
    /// <paramref name="totalPropertyPrice"/> that falls within a bracket is taxed at that
    /// bracket's rate, and the results are summed. Brackets are defined by MinValue
    /// (inclusive lower bound) and MaxValue (exclusive upper bound; null means open-ended).
    /// Returns 0 when the price is at or below the lowest bracket's MinValue.
    /// </summary>
    /// <param name="totalPropertyPrice">The total property price to be taxed.</param>
    /// <param name="taxBrackets">The list of tax brackets. Will be sorted by MinValue ascending.</param>
    /// <returns>The total property tax owed.</returns>
    public static decimal DerivePropertyTax(
        decimal totalPropertyPrice,
        IReadOnlyList<TaxBracketDto> taxBrackets)
    {
        // sort by MinValue ascending to ensure correct progressive accumulation
        taxBrackets = taxBrackets.OrderBy(tb => tb.MinValue).ToList();

        var propertyTax = 0m;
        foreach (var bracket in taxBrackets)
        {
            // price is at or below this bracket's floor — nothing more to tax
            if (totalPropertyPrice <= bracket.MinValue)
            {
                return Math.Round(propertyTax, 0);
            }

            var upperBound = bracket.MaxValue ?? totalPropertyPrice;
            var taxableAmount = Math.Min(totalPropertyPrice, upperBound) - bracket.MinValue;
            propertyTax += taxableAmount * bracket.TaxRate;
        }

        return Math.Round(propertyTax, 0);
    }

    /// <summary>
    /// Method 11 — Energy Cost Index.
    /// energyCostIndexIncrease[y]: step compounding of energyCostIndex
    /// totalMethodValues[y] = energyCostIndexIncrease[y] × totalSaleableAreaDeductByOccRate[y] × 12
    /// where totalSaleableAreaDeductByOccRate comes from the first method-06 assumption.
    /// </summary>
    private static decimal[] ComputeMethod11(
        Method11Detail d,
        int years,
        Dictionary<string, decimal[]> crossRef)
    {
        var indexGrowth = ComputeStepCompoundingArray(d.EnergyCostIndex, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        crossRef.TryGetValue("06", out var saleableAreaOccRate);

        var result = new decimal[years];
        for (int y = 0; y < years; y++)
        {
            // If year less than `startIn`, result will be 0;
            if (d.StartIn > 1 && y < d.StartIn - 1)
            {
                result[y] = 0;
                continue;
            }
            
            var areaOccRate = saleableAreaOccRate != null ? saleableAreaOccRate[y] : 0m;
            result[y] = Math.Round(indexGrowth[y] * areaOccRate * 12m, 2);
        }
        return result;
    }

    /// <summary>
    /// Method 12 — Proportion Of New Replacement Cost.
    /// proportionOfNewReplacementCosts[0] = (proportionPct/100) × newReplacementCost
    /// subsequent years: prevValue × (1 + increaseRate/100) each growth year.
    /// </summary>
    private static decimal[] ComputeMethod12(Method12Detail d, int years)
    {
        var firstYearAmt = (d.ProportionPct / 100m) * d.NewReplacementCost;
        var result = ComputeStepCompoundingArray(firstYearAmt, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        if (d.StartIn > 1)
        {
            // use 0-base year
            result = ShiftRight(result, d.StartIn - 1);
        }

        return result;
    }

    /// <summary>
    /// Method 14 — Specified Value With Growth.
    /// Same iterative step compounding as methods 03/08/etc.
    /// </summary>
    private static decimal[] ComputeMethod14(Method14Detail d, int years)
    {
        var result = ComputeStepCompoundingArray(d.FirstYearAmt, d.IncreaseRatePct, d.IncreaseRateYrs, years);

        if (d.StartIn > 1)
        {
            // use 0-base year
            result = ShiftRight(result, d.StartIn - 1);
        }

        return result;
    }

    // ── Aggregation helpers ───────────────────────────────────────────────

    private static Dictionary<Guid, decimal[]> AggregateCategoryValues(
        IncomeAnalysis analysis,
        Dictionary<Guid, decimal[]> assumptionValues,
        int years)
    {
        var result = new Dictionary<Guid, decimal[]>();

        // We also need income section total for gop calculation.
        // Find the income section total first (simple sum of its categories' assumptions).
        decimal[]? incomeSectionTotal = null;
        var incomeSection = analysis.Sections.FirstOrDefault(s => s.SectionType == "income");
        if (incomeSection != null)
        {
            incomeSectionTotal = new decimal[years];
            foreach (var category in incomeSection.Categories)
            {
                foreach (var assumption in category.Assumptions)
                {
                    if (assumptionValues.TryGetValue(assumption.Id, out var av))
                        for (int y = 0; y < years; y++) incomeSectionTotal[y] += av[y];
                }
            }
        }

        foreach (var section in analysis.Sections)
        {
            if (section.SectionType == "summaryDCF" || section.SectionType == "summaryDirect")
                continue;

            // Pass 1: non-GOP categories (sum of assumptions). This must complete before
            // GOP computation so GOP's Σ(sibling expenses) sees every populated sibling,
            // regardless of the section.Categories iteration order (EF DisplaySeq-ordered
            // load can place GOP before its sibling expense categories).
            foreach (var category in section.Categories)
            {
                if (category.CategoryType == "gop")
                    continue;

                var catTotal = new decimal[years];
                foreach (var assumption in category.Assumptions)
                {
                    if (assumptionValues.TryGetValue(assumption.Id, out var av))
                        for (int y = 0; y < years; y++) catTotal[y] += av[y];
                }
                result[category.Id] = catTotal;
            }

            // Pass 2: GOP categories — now safe to read every sibling from `result`.
            foreach (var category in section.Categories)
            {
                if (category.CategoryType != "gop")
                    continue;

                var catTotal = new decimal[years];

                // GOP = totalIncome − Σ(expenses-type categories, excluding gop + fixedExps)
                if (section.SectionType == "expenses")
                {
                    for (int y = 0; y < years; y++)
                    {
                        var income = incomeSectionTotal != null ? incomeSectionTotal[y] : 0m;
                        decimal exps = 0m;
                        foreach (var sibling in section.Categories)
                        {
                            if (sibling.CategoryType == "gop" || sibling.CategoryType == "fixedExps")
                                continue;
                            if (result.TryGetValue(sibling.Id, out var sibVals))
                                exps += sibVals[y];
                        }
                        catTotal[y] = income - exps;
                    }
                }
                // gop outside expenses section: leave as zeros.

                result[category.Id] = catTotal;
            }
        }

        return result;
    }

    private static Dictionary<Guid, decimal[]> AggregateSectionValues(
        IncomeAnalysis analysis,
        Dictionary<Guid, decimal[]> categoryValues,
        Dictionary<Guid, decimal[]> assumptionValues,
        int years)
    {
        var result = new Dictionary<Guid, decimal[]>();

        foreach (var section in analysis.Sections)
        {
            if (section.SectionType == "summaryDCF" || section.SectionType == "summaryDirect")
                continue;

            var sectionTotal = new decimal[years];

            foreach (var category in section.Categories)
            {
                // Exclude gop categories from section total (TS: if gop continue)
                if (category.CategoryType == "gop")
                    continue;

                if (categoryValues.TryGetValue(category.Id, out var cv))
                    for (int y = 0; y < years; y++) sectionTotal[y] += cv[y];
            }

            result[section.Id] = sectionTotal;
        }

        return result;
    }

    // ── Summary (DCF pipeline) ─────────────────────────────────────────────

    private static (decimal[] grossRevenue, decimal[] grossRevenueProportional) ComputeGrossRevenue(
        IncomeAnalysis analysis,
        Dictionary<Guid, decimal[]> sectionValues,
        decimal[] contractRentalFee,
        int years)
    {
        var grossRevenue = new decimal[years];
        var grossRevenueProportional = new decimal[years];

        // Σ positive section values − Σ negative section values − contractRentalFee
        var totalIncome = new decimal[years];

        foreach (var section in analysis.Sections)
        {
            if (!sectionValues.TryGetValue(section.Id, out var sv)) continue;
            for (int y = 0; y < years; y++)
            {
                if (section.Identifier == "positive")
                {
                    grossRevenue[y] += sv[y];
                    totalIncome[y] += sv[y];
                }
                else if (section.Identifier == "negative")
                {
                    grossRevenue[y] -= sv[y];
                }
            }
        }

        for (int y = 0; y < years; y++)
        {
            grossRevenue[y] -= contractRentalFee[y];
            grossRevenueProportional[y] = totalIncome[y] != 0m
                ? (grossRevenue[y] / totalIncome[y]) * 100m
                : 0m;
        }

        return (grossRevenue, grossRevenueProportional);
    }

    private static (decimal[] terminalRevenue, decimal[] totalNet, decimal[] discount, decimal[] presentValue, decimal finalValue)
        ComputeDcfSummary(IncomeAnalysis analysis, decimal[] grossRevenue)
    {
        var years = analysis.TotalNumberOfYears;

        // The TS iterates (totalNumberOfYears - 1) for terminalRevenue/totalNet/discount/presentValue.
        // terminalRevenue is placed only at index [N-2], all others are 0.
        var dcfLen = Math.Max(years - 1, 0);

        var terminalRevenue = new decimal[dcfLen];
        var totalNet = new decimal[dcfLen];
        var discount = new decimal[dcfLen];
        var presentValue = new decimal[dcfLen];

        // summaryDirect has a special 1-year path in TS.
        var hasSummaryDirect = analysis.Sections.Any(s => s.SectionType == "summaryDirect");

        if (hasSummaryDirect)
        {
            // Direct capitalization: pv = grossRevenue[0] / (capRate / 100)
            // Return single-element arrays so the FE can render all columns uniformly.
            var totalNet0 = grossRevenue.Length > 0 ? grossRevenue[0] : 0m;
            var capRate = analysis.CapitalizeRate;
            var pv = capRate != 0m ? totalNet0 / (capRate / 100m) : 0m;

            return (
                terminalRevenue: [0m],
                totalNet: [totalNet0],
                discount: [1m],
                presentValue: [pv],
                finalValue: pv);
        }

        // DCF path
        var capRateDcf = analysis.CapitalizeRate;
        var discountedRate = analysis.DiscountedRate;

        var lastGrossRevenue = grossRevenue.Length > 0 ? grossRevenue[^1] : 0m;
        var terminalValue = capRateDcf != 0m ? lastGrossRevenue / (capRateDcf / 100m) : 0m;

        for (int i = 0; i < dcfLen; i++)
        {
            // TS: terminal revenue placed only at index totalNumberOfYears - 2
            terminalRevenue[i] = (i == years - 2) ? terminalValue : 0m;

            totalNet[i] = grossRevenue[i] + terminalRevenue[i];

            // TS: 1 / Math.pow(1 + discountedRate/100, idx+1)
            discount[i] = discountedRate != 0m
                ? 1m / (decimal)Math.Pow((double)(1m + discountedRate / 100m), i + 1)
                : 1m;

            presentValue[i] = totalNet[i] * discount[i];
        }

        var finalValue = presentValue.Sum();

        return (terminalRevenue, totalNet, discount, presentValue, finalValue);
    }

    // ── Method 13 reference resolution ───────────────────────────────────

    private static bool ArraysEqual(decimal[] a, decimal[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static decimal[]? ResolveRefTarget(
        Method13Detail detail,
        Dictionary<Guid, decimal[]> sectionValues,
        Dictionary<Guid, decimal[]> categoryValues,
        Dictionary<Guid, decimal[]> assumptionValues,
        int years)
    {
        var target = detail.RefTarget;
        if (string.IsNullOrWhiteSpace(target.DbId))
            return null;

        if (!Guid.TryParse(target.DbId, out var refGuid))
            return null;

        return target.Kind switch
        {
            "section" => sectionValues.TryGetValue(refGuid, out var sv) ? sv : null,
            "category" => categoryValues.TryGetValue(refGuid, out var cv) ? cv : null,
            "assumption" => assumptionValues.TryGetValue(refGuid, out var av) ? av : null,
            _ => null
        };
    }

    // ── Shared growth helpers ─────────────────────────────────────────────

    /// <summary>
    /// Iterative step-compounding matching the TS derived-rules pattern:
    /// - year 0: return firstYearAmt
    /// - year y: if (y % increaseRateYrs == 0) apply increaseRatePct, else carry forward
    /// Safe when increaseRateYrs == 0 (treated as no growth).
    /// </summary>
    public static decimal[] ComputeStepCompoundingArray(
        decimal firstYearAmt,
        decimal increaseRatePct,
        int increaseRateYrs,
        int years)
    {
        var result = new decimal[years];
        if (years == 0) return result;

        result[0] = firstYearAmt;

        for (int y = 1; y < years; y++)
        {
            var growthRate = (increaseRateYrs > 0 && y % increaseRateYrs == 0)
                ? increaseRatePct
                : 0m;
            result[y] = Math.Round(result[y - 1] * (1m + growthRate / 100m), 2);
        }

        return result;
    }

    /// <summary>
    /// Occupancy rate step-addition matching the TS pattern:
    /// - year 0: firstYearPct
    /// - year y: if prevOccupancyRate >= 100, return 100; else if (y % occupancyRateYrs == 0) add occupancyRatePct
    /// Safe when occupancyRateYrs == 0 (treated as no growth).
    /// </summary>
    public static decimal[] ComputeOccupancyRate(
        decimal firstYearPct,
        decimal occupancyRatePct,
        int occupancyRateYrs,
        int years)
    {
        var result = new decimal[years];
        if (years == 0) return result;

        result[0] = firstYearPct;

        for (int y = 1; y < years; y++)
        {
            var prev = result[y - 1];
            if (prev >= 100m)
            {
                result[y] = 100m;
            }
            else if (occupancyRateYrs > 0 && y % occupancyRateYrs == 0)
            {
                result[y] = prev + occupancyRatePct;
            }
            else
            {
                result[y] = prev;
            }
        }

        return result;
    }

    /// <summary>
    /// Normalizes a source decimal array to exactly <paramref name="expectedLength"/>.
    /// Truncates when longer, zero-pads when shorter, returns a defensive copy otherwise.
    /// </summary>
    private static decimal[] NormalizeLength(decimal[]? source, int expectedLength)
    {
        // Guard: if the domain ever passes a weird length, don't throw — return empty.
        if (expectedLength <= 0)
        return Array.Empty<decimal>();

        // Case 1: null or empty source → all-zero array
        if (source is null || source.Length == 0)
        return new decimal[expectedLength];

        // Case 2: source is longer → truncate (range slice creates a new array)
        if (source.Length >= expectedLength)
        return source[..expectedLength];

        // Case 3: source is shorter → pad with zeros at the tail
        var padded = new decimal[expectedLength];
        source.CopyTo(padded, 0);
        return padded;
    }

    /// <summary>
    /// Shifts all elements in the source array to the right by the specified number of positions,
    /// filling the vacated positions on the left with zeros and truncating elements that overflow
    /// beyond the array's original length.
    /// </summary>
    static decimal[] ShiftRight(decimal[] source, int shift)
    {
        // Case 1: shift is zero or negative, return source as-is
        if (shift <= 0)
            return source;

        // Case 2: source is null or empty, return an empty array
        if (source is null || source.Length == 0)
            return Array.Empty<decimal>();

        int length = source.Length;

        // Case 3: shift exceeds or equals the source length, return an all-zero array
        if (shift >= length)
            return new decimal[length];

        // Case 4: shift is within the source length range, perform the shift
        decimal[] result = new decimal[length];

        // Copy source elements into result, offset by shift
        source[..^shift].CopyTo(result.AsSpan(shift));

        return result;
    }

    // ── JSON helpers ──────────────────────────────────────────────────────

    private static decimal[] ParseJsonArray(string json, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new decimal[expectedLength];

        try
        {
            var parsed = JsonSerializer.Deserialize<decimal[]>(json);
            if (parsed == null || parsed.Length == 0)
                return new decimal[expectedLength];

            if (parsed.Length >= expectedLength)
                return parsed[..expectedLength];

            // Pad with zeros if shorter.
            var padded = new decimal[expectedLength];
            parsed.CopyTo(padded, 0);
            return padded;
        }
        catch
        {
            return new decimal[expectedLength];
        }
    }
}
