using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Auth.Contracts.Companies;

namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// Writes the off-system external engagement onto the appraisal: the company that produced the
/// book, the book's appraisal date, and the fee that follows from engaging that company.
///
/// This is the offline mirror of <see cref="EventHandlers.CompanyAssignedIntegrationEventHandler"/>.
/// It deliberately reuses that handler's active-assignment selection rule and its
/// IAssignmentFeeService calls so the two paths cannot drift.
/// </summary>
public class SetOfflineExternalEngagementCommandHandler(
    IAppraisalRepository appraisalRepository,
    ICompanyLookupService companyLookup,
    IAssignmentFeeService feeService,
    AppraisalDbContext db,
    ICurrentUserService currentUser,
    ILogger<SetOfflineExternalEngagementCommandHandler> logger)
    : ICommandHandler<SetOfflineExternalEngagementCommand, SetOfflineExternalEngagementResult>
{
    public async Task<SetOfflineExternalEngagementResult> Handle(
        SetOfflineExternalEngagementCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        // Same selection rule as CompanyAssignedIntegrationEventHandler — AppraisalAssignmentElection. Keep the two in sync; a second rule here would let the
        // offline path write to a different row than the in-system path.
        var assignment = appraisal.Assignments.Current()
            ?? throw new BadRequestException(
                $"No active assignment found for appraisal '{command.AppraisalId}'.");

        // Path guard. Without this, the endpoint could be pointed at ANY appraisal — including one
        // mid-execution on the internal path — and Assign() below would erase its AssigneeUserId,
        // flip it to External, reset the SLA anchor and materialise a fee for a company that never
        // worked the case.
        //
        // AssignmentMethod == "Offline" is the module's proxy for "the workflow is on
        // int-offline-book-keyin": WorkflowService.PublishInternalAssignedEvent stamps it on landing
        // at that activity (and only there), so InternalAssignedIntegrationEventHandler has written
        // it before the keyer can ever open the task. It therefore covers BOTH the first key-in and
        // a later correction — there is no window where a genuine EXTO case is not yet "Offline".
        //
        // An earlier version also accepted AssignmentStatus == Pending as "first arrival". That was
        // wrong and exploitable: Pending is the state of every appraisal sitting at
        // appraisal-assignment awaiting an admin decision, so any authenticated caller could point
        // this endpoint at an unrelated appraisal and silently convert it to an off-system external
        // engagement. Status describes lifecycle position, not which path the case is on.
        if (!assignment.IsOfflineEngagement)
            throw new BadRequestException(
                $"Appraisal '{command.AppraisalId}' is not on the off-system external path " +
                $"(assignment is '{assignment.AssignmentStatus.Code}' via '{assignment.AssignmentMethod}'). " +
                "Only an appraisal routed with the EXTO decision can record an offline engagement.");

        // Only a row the workflow has handed to the keyer (InternalAssignedIntegrationEventHandler: Assigned, then
        // InProgress) is on the key-in task. A Pending row is not: a routed-back one is at appraisal-assignment
        // (ReturnToAssignmentQueue keeps method "Offline"), and a never-assigned one is an admin's EXTO draft or the
        // instant before the consumer ran. Recording there would hand the case on behind the admin's back, and let
        // any caller convert a case still awaiting its decision. The keyer saves again once the task is theirs.
        if (assignment.AssignmentStatus == AssignmentStatus.Pending)
            throw new BadRequestException(
                $"Appraisal '{command.AppraisalId}' is not on the key-in task (its assignment is still Pending at " +
                "appraisal assignment, or about to be handed to the keyer). Try again once it is on the key-in task.");

        // Assign() below unconditionally rewinds the row to Assigned. That is correct while the
        // keyer still owns the work, but silently demoting an assignment the bank has already
        // reviewed would reopen the invoicing gate and discard SubmittedAt. Once the book has been
        // handed on, a correction has to come back through a workflow route-back.
        if (assignment.AssignmentStatus != AssignmentStatus.Assigned
            && assignment.AssignmentStatus != AssignmentStatus.InProgress)
            throw new BadRequestException(
                $"The appraisal book has already been submitted for review (assignment is " +
                $"'{assignment.AssignmentStatus.Code}'). Route the case back to the keyin task " +
                "before correcting the external engagement.");

        var companyId = await ResolveCompanyAsync(command, assignment, cancellationToken);

        // Assign() defaults every optional parameter to null, so anything not passed back is wiped.
        // AssigneeUserId matters most: the keyer is stamped onto the row when the workflow lands on
        // int-offline-book-keyin (InternalAssignedIntegrationEventHandler), and omitting it here
        // would erase the very person doing this save. ReassignmentNumber is preserved for the same
        // reason — resetting it to 1 would discard the rework count on a routed-back case.
        assignment.Assign(
            assignmentType: "External",
            assigneeUserId: assignment.AssigneeUserId,
            assigneeCompanyId: companyId,
            assignmentMethod: AppraisalAssignment.OfflineAssignmentMethod,
            internalAppraiserId: assignment.InternalAppraiserId,
            internalFollowupMethod: assignment.InternalFollowupAssignmentMethod,
            reassignmentNumber: assignment.ReassignmentNumber,
            // Keep the admin SetAssigner recorded; a row nobody stamped (empty, or the "System" a fresh queue row
            // carries) falls back to the caller. Never from the body.
            assignedBy: IsUnstamped(assignment.AssignedBy) ? currentUser.UserCode ?? "System" : assignment.AssignedBy);

        if (!string.IsNullOrWhiteSpace(command.ExternalAppraiserName))
            assignment.SetExternalAppraiser(
                appraiserId: string.Empty,
                name: command.ExternalAppraiserName,
                license: null);

        // Assign() resets the row to Assigned. The workflow already moved the case onto
        // int-offline-book-keyin before the company was known, so the transition-time StartWork
        // has been and gone — advance it here or the assignment would sit at Assigned while the
        // keyer is actively working it.
        assignment.StartWork();

        await SetBookDateAsync(command.AppraisalId, command.BookDate, cancellationToken);

        var feeSource = await feeService.ResolveSourceForAppraisalAsync(
            appraisal, new AssignmentFeeSource.TierBased(), cancellationToken);

        await feeService.EnsureAssignmentFeeItemsAsync(
            appraisalId: command.AppraisalId,
            assignmentId: assignment.Id,
            source: feeSource,
            // The row was priced when it was handed to the keyer; a base that may already be billed is left alone.
            repriceSystemBase: false,
            ct: cancellationToken);

        // Kept on purpose: assignment events only touch AppraisalAssignment rows, so this stamps Appraisals.UpdatedAt
        // (read by GetAppraisalStatus and vw_MisCasReport).
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        logger.LogInformation(
            "Recorded offline external engagement for Appraisal {AppraisalId}: CompanyId={CompanyId}, BookDate={BookDate}",
            command.AppraisalId, companyId, command.BookDate);

        return new SetOfflineExternalEngagementResult(assignment.Id);
    }

    /// <summary>
    /// A company already on the row is the single source: kept, never replaced here (a wrong one is corrected by
    /// routing back to appraisal-assignment). Normally the admin recorded it at assignment; on a case that reached
    /// key-in before that, the keyer recorded it with an earlier save. It is not re-checked against its MOU window:
    /// it was checked when recorded, and a window that lapsed since must not strand the keyer. Only a row with no
    /// company takes the keyer's, under the existence + MOU-window rule CompanySelectionActivity applies to a
    /// manual selection.
    /// </summary>
    private async Task<string> ResolveCompanyAsync(
        SetOfflineExternalEngagementCommand command, AppraisalAssignment assignment, CancellationToken ct)
    {
        // "Holds a company" is the same test vw_AppraisalValidationContext.ExternalCompanyRecorded uses: non-empty.
        if (!string.IsNullOrWhiteSpace(assignment.AssigneeCompanyId))
        {
            if (command.CompanyId is { } sent
                && !(Guid.TryParse(assignment.AssigneeCompanyId, out var recorded) && recorded == sent))
                throw new BadRequestException(
                    "The external company is already recorded on this assignment and cannot be changed here. " +
                    "To correct it, route the case back to appraisal assignment.");
            return assignment.AssigneeCompanyId;
        }

        var companyId = command.CompanyId ?? throw new BadRequestException("The external company is required.");

        // IsAssignable is resolved inside Auth (which owns the rule) and surfaced on the DTO.
        var company = await companyLookup.GetByIdAsync(companyId, ct)
                      ?? throw new NotFoundException("Company", companyId);

        if (!company.IsAssignable)
            throw new BadRequestException(
                $"Company '{company.Name}' is not currently assignable (outside its MOU approval window).");

        return companyId.ToString();
    }

    private static bool IsUnstamped(string? assignedBy) =>
        string.IsNullOrWhiteSpace(assignedBy) || string.Equals(assignedBy, "System", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stamps the book's appraisal date onto ValuationAnalyses.ValuationDate — the field every
    /// downstream AppraisalDate derivation already reads. The row normally exists by this point
    /// (AppraisalCreationService seeds it); it is created here only for defensiveness.
    /// AppraisalValuationSummaryService preserves this value on later recomputes because the
    /// assignment now carries AssignmentMethod = "Offline".
    /// </summary>
    private async Task SetBookDateAsync(Guid appraisalId, DateTime bookDate, CancellationToken ct)
    {
        var valuation = db.ValuationAnalyses.Local
                            .FirstOrDefault(v => v.AppraisalId == appraisalId)
                        ?? await db.ValuationAnalyses
                            .FirstOrDefaultAsync(v => v.AppraisalId == appraisalId, ct);

        if (valuation is null)
        {
            valuation = ValuationAnalysis.Create(appraisalId, "Combined", bookDate);
            db.ValuationAnalyses.Add(valuation);
            return;
        }

        valuation.SetValuationDate(bookDate);
    }
}
