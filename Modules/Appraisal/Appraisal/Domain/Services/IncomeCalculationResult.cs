namespace Appraisal.Domain.Services;

/// <summary>
/// Holds all server-computed values produced by <see cref="IncomeCalculationService"/>.
/// Callers mutate the aggregate by passing this to <c>IncomeAnalysis.ApplyCalculationResult</c>.
/// All year-indexed arrays have length == <c>TotalNumberOfYears</c> unless noted.
/// </summary>
public sealed record IncomeCalculationResult
{
    // ── method-level totals ────────────────────────────────────────────────
    /// <summary>
    /// Map: assumptionId → year-indexed method-values array (length = TotalNumberOfYears).
    /// </summary>
    public required IReadOnlyDictionary<Guid, decimal[]> MethodValues { get; init; }

    // ── assumption totals ─────────────────────────────────────────────────
    /// <summary>
    /// Map: assumptionId → year-indexed totals array.
    /// For now equals MethodValues (one method per assumption).
    /// </summary>
    public required IReadOnlyDictionary<Guid, decimal[]> AssumptionValues { get; init; }

    // ── category totals ────────────────────────────────────────────────────
    /// <summary>
    /// Map: categoryId → year-indexed totals array.
    /// </summary>
    public required IReadOnlyDictionary<Guid, decimal[]> CategoryValues { get; init; }

    // ── section totals ─────────────────────────────────────────────────────
    /// <summary>
    /// Map: sectionId → year-indexed totals array.
    /// summaryDCF sections are NOT included here.
    /// </summary>
    public required IReadOnlyDictionary<Guid, decimal[]> SectionValues { get; init; }

    // ── summary arrays (length = TotalNumberOfYears) ───────────────────────
    public required decimal[] ContractRentalFee { get; init; }
    public required decimal[] GrossRevenue { get; init; }
    public required decimal[] GrossRevenueProportional { get; init; }

    /// <summary>
    /// Length = TotalNumberOfYears - 1.  Terminal value sits at index [N-2].
    /// Mirrors the TS array which only has N-1 entries.
    /// </summary>
    public required decimal[] TerminalRevenue { get; init; }

    /// <summary>Length = TotalNumberOfYears - 1 (same as TerminalRevenue).</summary>
    public required decimal[] TotalNet { get; init; }

    /// <summary>Length = TotalNumberOfYears - 1 (same as TerminalRevenue).</summary>
    public required decimal[] Discount { get; init; }

    /// <summary>Length = TotalNumberOfYears - 1 (same as TerminalRevenue).</summary>
    public required decimal[] PresentValue { get; init; }

    // ── scalar finals ──────────────────────────────────────────────────────
    public required decimal FinalValue { get; init; }

    /// <summary>
    /// Preserved from analysis if user has overridden it; otherwise equals FinalValue.
    /// </summary>
    public required decimal FinalValueRounded { get; init; }
}
