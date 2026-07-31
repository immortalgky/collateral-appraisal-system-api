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
    bool IsActive,
    string? AppraisalType);

internal static class FeeStructureMapping
{
    public static FeeStructureDto ToDto(this FeeStructure f) =>
        new(f.Id, f.FeeCode, f.BaseAmount, f.MinSellingPrice, f.MaxSellingPrice, f.IsActive, f.AppraisalType);

    /// <summary>
    /// Rejects a tier whose selling-price range overlaps an existing active tier in the same ladder
    /// — same FeeCode *and* same AppraisalType scope. A PreAppraisal 0→∞ tier therefore does not
    /// conflict with the generic (null-scoped) bands. A null max means open-ended (+∞). Inactive
    /// tiers are ignored (not used for fee matching) and an inactive incoming row is never checked.
    /// The predicate is evaluated in the database so only the existence of a conflict is fetched,
    /// never the rows themselves.
    /// </summary>
    public static async Task EnsureNoActiveOverlapAsync(
        AppraisalDbContext db,
        string feeCode,
        string? appraisalType,
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
        var query = db.FeeStructures
            .Where(f => f.FeeCode == feeCode
                        && f.IsActive
                        && (excludeId == null || f.Id != excludeId)
                        && (f.MaxSellingPrice == null || minSellingPrice <= f.MaxSellingPrice)
                        && (maxSellingPrice == null || f.MinSellingPrice <= maxSellingPrice));

        // Scope to the same ladder. Written as an explicit branch rather than a single
        // `f.AppraisalType == appraisalType` so the null case provably translates to IS NULL.
        query = appraisalType is null
            ? query.Where(f => f.AppraisalType == null)
            : query.Where(f => f.AppraisalType == appraisalType);

        if (await query.AnyAsync(ct))
        {
            var scope = appraisalType is null ? "any appraisal type" : $"appraisal type '{appraisalType}'";
            throw new ConflictException(
                $"The selling-price range overlaps an existing active tier for fee code '{feeCode}' ({scope}).");
        }
    }
}
