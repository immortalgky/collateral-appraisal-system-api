using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.Fees;

/// <summary>
/// The "admin stage" of an appraisal's fee: its assignment row is still <see cref="AssignmentStatus.Pending"/>,
/// i.e. the case has not been handed to a company or to staff yet (Assign() is what leaves Pending). Until then the
/// bank admin owns the fee; afterwards whatever the admin set is read-only for the assignee.
/// Also the single home of the item-ownership rules the Add / Update / Remove handlers share, so they cannot drift.
/// </summary>
internal static class FeeAdminStage
{
    public static async Task<bool> IsAdminStageAsync(AppraisalDbContext dbContext, Guid assignmentId, CancellationToken ct)
    {
        // The global filter hides Rejected/Cancelled rows, which yields null here: not the admin stage.
        var status = await dbContext.AppraisalAssignments
            .AsNoTracking()
            .Where(a => a.Id == assignmentId)
            .Select(a => a.AssignmentStatus)
            .FirstOrDefaultAsync(ct);

        return status == AssignmentStatus.Pending;
    }

    /// <summary>
    /// The appraisal's current assignment row, by <see cref="AppraisalAssignmentElection"/> — the same rule the
    /// assignment consumers use. Shared by the admin-stage writes and the fee preview.
    /// </summary>
    public static IQueryable<AppraisalAssignment> CurrentAssignment(AppraisalDbContext dbContext, Guid appraisalId) =>
        dbContext.AppraisalAssignments
            .AsNoTracking()
            .NewestFirstFor(appraisalId);

    /// <summary>
    /// The fee of the appraisal's current assignment row, created on first use while it awaits assignment. A request submitted without fee
    /// data has no fee row at all, and the admin must still be able to set one. A row created after a reassignment
    /// copies its context from the appraisal's newest fee, exactly as assignment-time materialisation would.
    /// </summary>
    public static async Task<AppraisalFee> FindOrCreateAdminStageFeeAsync(
        AppraisalDbContext dbContext,
        Guid appraisalId,
        CancellationToken ct)
    {
        var current = await CurrentAssignment(dbContext, appraisalId)
            .Select(a => new { a.Id, a.AssignmentStatus })
            .FirstOrDefaultAsync(ct);

        if (current is null)
            throw new BadRequestException("A fee can only be created for an appraisal that has an assignment.");

        // An existing fee row is used as it is, whatever the stage: the caller's normal rules then apply.
        var fee = await LoadFeeAsync(dbContext, current.Id, ct);
        if (fee is not null)
            return fee;

        if (current.AssignmentStatus != AssignmentStatus.Pending)
            throw new BadRequestException(
                "A fee can only be created while the appraisal is awaiting assignment.");

        var copied = await FeeShellFactory.CreateFromLatestAsync(dbContext, appraisalId, current.Id, ct);
        if (copied is { } c)
            fee = c.Shell;
        else
        {
            fee = AppraisalFee.Create(current.Id);
            dbContext.AppraisalFees.Add(fee);
        }

        // Write the row now rather than at commit: two first adds racing on the unique AssignmentId index would
        // otherwise surface as a 500 after the handler is done. The loser reloads the winner's row (once) and
        // carries on with it.
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(fee).State = EntityState.Detached;
            var winner = await LoadFeeAsync(dbContext, current.Id, ct);
            if (winner is null)
                throw; // not a lost race: there is no other row, so this is a real failure
            return winner;
        }

        return fee;
    }

    private static Task<AppraisalFee?> LoadFeeAsync(AppraisalDbContext dbContext, Guid assignmentId, CancellationToken ct) =>
        dbContext.AppraisalFees
            .Include(f => f.Items)
            .Where(f => f.AssignmentId == assignmentId)
            .OrderBy(f => f.Id) // unique, so a split query cannot pair the row with another row's items
            .FirstOrDefaultAsync(ct);

    // ── Item rules shared by Add / Update / Remove ──

    /// <summary>
    /// The edit lock after handover: while any item awaits approval (submitted, not resolved) the fee cannot be edited.
    /// At the admin stage the admin wins and the lock does not apply — after a route-back the previous assignee's
    /// pending item would otherwise block the admin, who can only touch their own items anyway. A Conflict (409), not
    /// an unhandled exception, so the client gets a clear reason instead of a 500.
    /// </summary>
    public static void EnsureNoApprovalAwaitingReview(AppraisalFee fee, bool isAdminStage, string action)
    {
        if (!isAdminStage && fee.Items.Any(i => i.ApprovalSubmittedAt.HasValue && i.ApprovalStatus == "Pending"))
            throw new ConflictException(
                $"Cannot {action} a fee item: an approval is currently awaiting review. " +
                "Wait for the approval to be resolved before making further changes.",
                "FeeApprovalAwaitingReview");
    }

    /// <summary>The appraisal has one base appraisal-fee line; a second would be double-billed.</summary>
    public static void EnsureNoOtherBaseItem(AppraisalFee fee, string feeCode, Guid? exceptItemId)
    {
        if (feeCode == AppraisalFee.BaseFeeCode && fee.Items.Any(i => i.FeeCode == AppraisalFee.BaseFeeCode && i.Id != exceptItemId))
            throw new BadRequestException(
                "The appraisal fee (01) already exists on this fee. Update it instead of adding another.");
    }

    /// <summary>
    /// The system's base line when it is the ONLY 01 on the fee: an admin adding a 01 at the admin stage overrides it
    /// instead of being refused. Needed after a route-back, where the fee preview hides this stale line (the new source
    /// may price nothing) so the admin sees no 01 to update, yet it still blocks a second one and cannot be removed.
    /// An Admin/User 01 (or several) keeps <see cref="EnsureNoOtherBaseItem"/>'s refusal.
    /// </summary>
    public static AppraisalFeeItem? SystemBaseToOverride(AppraisalFee fee)
    {
        var bases = fee.Items.Where(i => i.FeeCode == AppraisalFee.BaseFeeCode).Take(2).ToList();
        return bases is [{ Source: FeeItemSource.System } only] ? only : null;
    }

    /// <summary>
    /// Re-checks after the new base item has been written, because two admins adding 01 at once both pass
    /// <see cref="EnsureNoOtherBaseItem"/> on their own snapshot. Throwing rolls the command's transaction back.
    /// ponytail: narrows the race to two uncommitted inserts; a filtered unique index on (AppraisalFeeId) WHERE
    /// FeeCode = '01' closes it and needs a migration.
    /// </summary>
    public static async Task EnsureSingleBaseItemAsync(AppraisalDbContext dbContext, AppraisalFee fee, CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);

        var count = await dbContext.AppraisalFeeItems
            .AsNoTracking()
            .CountAsync(i => i.AppraisalFeeId == fee.Id && i.FeeCode == AppraisalFee.BaseFeeCode, ct);
        if (count > 1)
            throw new ConflictException(
                "The appraisal fee (01) was added by someone else at the same moment. Reload and update it instead.",
                "FeeBaseItemDuplicated");
    }

    /// <summary>After handover a user item cannot pose as the base line, even when the fee has none (as on the update path).</summary>
    public static void EnsureUserItemIsNotBase(string feeCode)
    {
        if (feeCode == AppraisalFee.BaseFeeCode)
            throw new ConflictException(
                "The appraisal fee (01) cannot be added after assignment; only an admin can set it before.",
                "FeeBaseSetBySystem");
    }

    public static void EnsureCanUpdate(AppraisalFee fee, AppraisalFeeItem item, string newFeeCode, bool isAdminStage)
    {
        // 01 is the base line: it cannot be re-coded away (that would dodge the remove guard) and no other line
        // can be re-coded into it (that would let a user item pose as the base).
        if (item.FeeCode != newFeeCode && (item.FeeCode == AppraisalFee.BaseFeeCode || newFeeCode == AppraisalFee.BaseFeeCode))
            throw new BadRequestException(
                "The appraisal fee (01) cannot be changed into another fee code, nor another fee into 01. " +
                "Remove the item and add a new one instead.");

        EnsureNoOtherBaseItem(fee, newFeeCode, item.Id);
        EnsureOwnedForEdit(item, isAdminStage, "changed");
    }

    public static void EnsureCanRemove(AppraisalFeeItem item, bool isAdminStage)
    {
        // The system's base line is the figure the case is billed on; only an admin override of it can go
        // (which hands the base back to the system calculation).
        if (item.FeeCode == AppraisalFee.BaseFeeCode && item.Source == FeeItemSource.System)
            throw new BadRequestException("The system-calculated appraisal fee (01) cannot be removed.");

        EnsureOwnedForEdit(item, isAdminStage, "removed");
    }

    private static void EnsureOwnedForEdit(AppraisalFeeItem item, bool isAdminStage, string verb)
    {
        if (!isAdminStage)
        {
            // What the admin set before handover is read-only for whoever received the case.
            if (item.Source == FeeItemSource.Admin)
                throw new ConflictException(
                    $"This fee item was set by an admin and can no longer be {verb}.", "FeeItemSetByAdmin");

            // The system base is the billed figure; after handover only an admin's override, made at the admin
            // stage, can change it. The assignee cannot.
            if (item.Source == FeeItemSource.System && item.FeeCode == AppraisalFee.BaseFeeCode)
                throw new ConflictException(
                    $"The system-calculated appraisal fee (01) cannot be {verb} after assignment; only an admin can override it before.",
                    "FeeBaseSetBySystem");
            return;
        }

        // At the admin stage the admin edits only their own items and the system base (overriding it makes it
        // theirs). Anyone else's item keeps its own approval flow: touching it here would turn it into an admin
        // item and skip that approval.
        if (item.Source == FeeItemSource.Admin || (item.Source == FeeItemSource.System && item.FeeCode == AppraisalFee.BaseFeeCode))
            return;

        throw new ConflictException(
            $"This fee item was entered by the assignee and goes through its own approval; it cannot be {verb} before assignment.",
            "FeeItemNotAdminOwned");
    }
}
