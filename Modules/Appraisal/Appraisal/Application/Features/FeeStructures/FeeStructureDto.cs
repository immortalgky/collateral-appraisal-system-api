namespace Appraisal.Application.Features.FeeStructures;

/// <summary>
/// Read shape for the master <c>FeeStructures</c> table. FeeName is intentionally absent — the
/// screen resolves the display name from <see cref="FeeCode"/> via the TypeOfFee parameter group.
/// </summary>
public record FeeStructureDto(
    Guid Id,
    string FeeCode,
    decimal BaseAmount,
    decimal MinSellingPrice,
    decimal? MaxSellingPrice,
    bool IsActive);

internal static class FeeStructureMapping
{
    public static FeeStructureDto ToDto(this FeeStructure f) =>
        new(f.Id, f.FeeCode, f.BaseAmount, f.MinSellingPrice, f.MaxSellingPrice, f.IsActive);

    /// <summary>
    /// Rejects a tier whose selling-price range overlaps an existing active tier of the same
    /// FeeCode. A null max means open-ended (+∞). Inactive tiers are ignored (not used for fee
    /// matching) and an inactive incoming row is never checked. The predicate is evaluated in the
    /// database so only the existence of a conflict is fetched, never the rows themselves.
    /// </summary>
    public static async Task EnsureNoActiveOverlapAsync(
        AppraisalDbContext db,
        string feeCode,
        decimal minSellingPrice,
        decimal? maxSellingPrice,
        bool isActive,
        Guid? excludeId,
        CancellationToken ct)
    {
        if (!isActive)
            return;

        // Overlap of [min,max] with an existing [f.Min,f.Max], treating null max as +∞:
        //   min <= f.Max  AND  f.Min <= max
        var overlaps = await db.FeeStructures
            .Where(f => f.FeeCode == feeCode
                        && f.IsActive
                        && (excludeId == null || f.Id != excludeId)
                        && (f.MaxSellingPrice == null || minSellingPrice <= f.MaxSellingPrice)
                        && (maxSellingPrice == null || f.MinSellingPrice <= maxSellingPrice))
            .AnyAsync(ct);

        if (overlaps)
            throw new ConflictException(
                $"The selling-price range overlaps an existing active tier for fee code '{feeCode}'.");
    }
}
