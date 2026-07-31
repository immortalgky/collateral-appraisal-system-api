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
    ILogger<SetOfflineExternalEngagementCommandHandler> logger)
    : ICommandHandler<SetOfflineExternalEngagementCommand, SetOfflineExternalEngagementResult>
{
    public async Task<SetOfflineExternalEngagementResult> Handle(
        SetOfflineExternalEngagementCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(command.AppraisalId, cancellationToken)
                        ?? throw new NotFoundException("Appraisal", command.AppraisalId);

        // Same selection rule as CompanyAssignedIntegrationEventHandler — latest assignment that is
        // neither Rejected nor Cancelled. Keep the two in sync; a second rule here would let the
        // offline path write to a different row than the in-system path.
        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus != AssignmentStatus.Rejected
                        && a.AssignmentStatus != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault()
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

        // Assign() below unconditionally rewinds the row to Assigned. That is correct while the
        // keyer still owns the work, but silently demoting an assignment the bank has already
        // reviewed would reopen the invoicing gate and discard SubmittedAt. Once the book has been
        // handed on, a correction has to come back through a workflow route-back.
        if (assignment.AssignmentStatus != AssignmentStatus.Pending
            && assignment.AssignmentStatus != AssignmentStatus.Assigned
            && assignment.AssignmentStatus != AssignmentStatus.InProgress)
            throw new BadRequestException(
                $"The appraisal book has already been submitted for review (assignment is " +
                $"'{assignment.AssignmentStatus.Code}'). Route the case back to the keyin task " +
                "before correcting the external engagement.");

        // Enforce the same MOU-window rule CompanySelectionActivity applies to a manual selection,
        // so an off-system engagement cannot record a company the bank may not currently use.
        // IsAssignable is resolved inside Auth (which owns the rule) and surfaced on the DTO.
        var company = await companyLookup.GetByIdAsync(command.CompanyId, cancellationToken)
                      ?? throw new NotFoundException("Company", command.CompanyId);

        if (!company.IsAssignable)
            throw new BadRequestException(
                $"Company '{company.Name}' is not currently assignable (outside its MOU approval window).");

        // Assign() defaults every optional parameter to null, so anything not passed back is wiped.
        // AssigneeUserId matters most: the keyer is stamped onto the row when the workflow lands on
        // int-offline-book-keyin (InternalAssignedIntegrationEventHandler), and omitting it here
        // would erase the very person doing this save. ReassignmentNumber is preserved for the same
        // reason — resetting it to 1 would discard the rework count on a routed-back case.
        assignment.Assign(
            assignmentType: "External",
            assigneeUserId: assignment.AssigneeUserId,
            assigneeCompanyId: command.CompanyId.ToString(),
            assignmentMethod: AppraisalAssignment.OfflineAssignmentMethod,
            internalAppraiserId: assignment.InternalAppraiserId,
            internalFollowupMethod: assignment.InternalFollowupAssignmentMethod,
            reassignmentNumber: assignment.ReassignmentNumber,
            assignedBy: string.IsNullOrEmpty(command.AssignedBy) ? "System" : command.AssignedBy);

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
            ct: cancellationToken);

        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        logger.LogInformation(
            "Recorded offline external engagement for Appraisal {AppraisalId}: CompanyId={CompanyId}, BookDate={BookDate}",
            command.AppraisalId, command.CompanyId, command.BookDate);

        return new SetOfflineExternalEngagementResult(assignment.Id);
    }

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
