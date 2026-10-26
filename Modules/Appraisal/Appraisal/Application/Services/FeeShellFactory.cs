using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;

namespace Appraisal.Application.Services;

/// <summary>
/// Builds a fresh <see cref="AppraisalFee"/> shell for an assignment row that has none (a reassignment created
/// a new row), carrying the fee context over from the appraisal's newest existing fee. Shared by assignment-time
/// fee materialisation and the admin's first fee edit so both start from the same context.
/// </summary>
internal static class FeeShellFactory
{
    /// <summary>
    /// The appraisal's newest fee row, optionally leaving out one assignment's own row — the fee a fresh shell copies
    /// its context from. Shared with the fee preview so it reports the context assignment would copy.
    /// </summary>
    public static IOrderedQueryable<AppraisalFee> LatestFee(
        AppraisalDbContext dbContext, Guid appraisalId, Guid? exceptAssignmentId)
    {
        var fees = dbContext.AppraisalFees.AsQueryable();
        if (exceptAssignmentId is { } except)
            fees = fees.Where(f => f.AssignmentId != except);

        return fees
            .Join(
                dbContext.AppraisalAssignments.Where(a => a.AppraisalId == appraisalId),
                f => f.AssignmentId,
                a => a.Id,
                (f, _) => f)
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id); // unique tie-break (SQL order), so two fees stamped alike never swap between calls
    }

    /// <summary>The new, tracked shell and the fee it copied from; null when the appraisal has no fee row at all.</summary>
    public static async Task<(AppraisalFee Shell, Guid SourceFeeId)?> CreateFromLatestAsync(
        AppraisalDbContext dbContext,
        Guid appraisalId,
        Guid assignmentId,
        CancellationToken ct)
    {
        var latestFee = await LatestFee(dbContext, appraisalId, assignmentId).FirstOrDefaultAsync(ct);

        if (latestFee is null)
            return null;

        var fee = AppraisalFee.Create(
            assignmentId: assignmentId,
            feePaymentType: latestFee.FeePaymentType,
            feeNotes: latestFee.FeeNotes,
            totalSellingPrice: latestFee.TotalSellingPrice);

        if (latestFee.BankAbsorbAmount > 0)
            fee.SetBankAbsorb(latestFee.BankAbsorbAmount);

        // The keyed inspection fee is what the NEXT inspection will charge — it must outlive a reassignment.
        fee.SetConstructionInspectionFee(latestFee.ConstructionInspectionFeeAmount);

        dbContext.AppraisalFees.Add(fee);

        return (fee, latestFee.Id);
    }
}
