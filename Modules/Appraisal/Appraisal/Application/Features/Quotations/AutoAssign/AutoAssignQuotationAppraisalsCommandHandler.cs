using Appraisal.Application.Features.Assignments.AssignAppraisal;
using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Contracts.Services;
using Auth.Contracts.Companies;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.AutoAssign;

public class AutoAssignQuotationAppraisalsCommandHandler(
    IQuotationRepository quotationRepository,
    IAppraisalUnitOfWork unitOfWork,
    IQuotationAutoAssignStateReader stateReader,
    ICompanyLookupService companyLookup,
    IUserGroupService userGroupService,
    IServiceScopeFactory scopeFactory,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IDateTimeProvider dateTimeProvider,
    ILogger<AutoAssignQuotationAppraisalsCommandHandler> logger)
    : ICommandHandler<AutoAssignQuotationAppraisalsCommand, IReadOnlyList<AutoAssignItemResult>>
{
    private const int MaxMessageLength = 500;
    public const string StoppedMessage = "Auto-assign stopped before this appraisal was processed";
    // Used when the command threw before its loop (the loop and the saves catch their own errors),
    // so nothing was assigned.
    public const string UnexpectedStopMessage =
        "Auto-assign did not run. Assign this appraisal on the Administration page.";
    private const string UnverifiedMessage = "Assigned; final state could not be verified";
    private const string FollowupStaffGroup = "IntAppraisalStaff";
    private const string GenericFailureMessage =
        "Assignment failed. Check the appraisal and assign it on the Administration page.";
    private const string NoMatchMessage =
        "Company selection returned no match (external company assignment switched off, or the company is outside its MOU window)";

    public async Task<IReadOnlyList<AutoAssignItemResult>> Handle(
        AutoAssignQuotationAppraisalsCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        // Runs after the finalize commit: a client disconnect or timeout must not stop the loop halfway.
        var ct = CancellationToken.None;

        var quotation = await quotationRepository.GetWithAppraisalsAsync(command.QuotationRequestId, ct)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        if (quotation.Status != "Finalized" || quotation.SelectedCompanyId is not { } winnerId)
            throw new BadRequestException("Auto-assign is only available on a finalized quotation");

        // The actor always comes from the server, never from the request body.
        var assignedBy = currentUser.UserCode
                         ?? throw new UnauthorizedAccessException("Cannot determine the current user");

        var results = new List<AutoAssignItemResult>();
        try
        {
            var winner = await companyLookup.GetByIdAsync(winnerId, ct);
            var staffByAppraisal = command.Staff
                .Where(s => !string.IsNullOrWhiteSpace(s.StaffId))
                .GroupBy(s => s.AppraisalId)
                .ToDictionary(g => g.Key, g => g.First().StaffId.Trim());
            var pricedAppraisals = (await stateReader.GetPricedAppraisalIdsAsync(
                quotation.SelectedQuotationId ?? Guid.Empty, ct)).ToHashSet();

            var staffEligibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // ponytail: synchronous and linear in the number of appraisals (one workflow resume each).
            // If quotations grow large, move this loop into a QuotationFinalized consumer.
            foreach (var link in quotation.Appraisals)
            {
                staffByAppraisal.TryGetValue(link.AppraisalId, out var staffId);

                var (result, message, reasonCode) = await AssignOneAsync(
                    link.AppraisalId, winner, pricedAppraisals, staffEligibility, staffId, assignedBy, ct);

                link.RecordAutoAssign(result, message, reasonCode, dateTimeProvider.ApplicationNow);
                results.Add(new AutoAssignItemResult(link.AppraisalId, result, message, reasonCode));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-assign stopped early for quotation {QuotationId}", quotation.Id);
        }

        // Every appraisal ends with a recorded outcome, even if the loop above stopped early.
        foreach (var link in quotation.Appraisals.Where(l => results.All(r => r.AppraisalId != l.AppraisalId)))
        {
            link.RecordAutoAssign(AutoAssignOutcome.Failed, StoppedMessage, AutoAssignReason.Stopped, dateTimeProvider.ApplicationNow);
            results.Add(new AutoAssignItemResult(
                link.AppraisalId, AutoAssignOutcome.Failed, StoppedMessage, AutoAssignReason.Stopped));
        }

        // Skipped (deleted, workflow ended, already assigned) needs no action. Failed and Parked, including
        // construction inspections, need a manual assign.
        var incomplete = results.Count(r => r.Result is AutoAssignOutcome.Failed or AutoAssignOutcome.Parked);
        if (incomplete > 0)
        {
            try
            {
                outbox.Publish(new QuotationAutoAssignNotificationIntegrationEvent
                {
                    QuotationRequestId = quotation.Id,
                    Recipient = assignedBy,
                    Title = "Quotation auto-assign incomplete",
                    Message = $"Quotation {quotation.QuotationNumber ?? quotation.Id.ToString()}: {results.Count(r => r.Result == AutoAssignOutcome.Assigned)} of " +
                              $"{results.Count} appraisals assigned, {incomplete} need your attention.",
                    ActionUrl = $"/quotations/{quotation.Id}"
                }, correlationId: quotation.Id.ToString());
            }
            catch (Exception ex)
            {
                // Losing the notification must not lose the recorded results below.
                logger.LogError(ex, "Auto-assign: could not queue the incomplete notification for quotation {QuotationId}",
                    quotation.Id);
            }
        }

        // One save for all results and the notification: a failed save must not leave a poisoned entry
        // that breaks later saves. The assignments themselves are already committed.
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-assign: could not persist results for quotation {QuotationId}", quotation.Id);
        }

        return results;
    }

    private async Task<(string Result, string? Message, string? ReasonCode)> AssignOneAsync(
        Guid appraisalId,
        CompanyLookupDto? winner,
        HashSet<Guid> pricedAppraisals,
        Dictionary<string, bool> staffEligibility,
        string? staffId,
        string assignedBy,
        CancellationToken cancellationToken)
    {
        // Read just before assigning: the loop can run for a while and the appraisal may have moved on.
        AppraisalAssignState? state;
        try
        {
            state = (await stateReader.GetAsync([appraisalId], cancellationToken)).FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-assign: could not read the state of appraisal {AppraisalId}", appraisalId);
            return (AutoAssignOutcome.Failed, "Could not read the appraisal state", AutoAssignReason.StateUnreadable);
        }

        var decision = AutoAssignClassifier.Classify(
            state, winner is { IsAssignable: true }, pricedAppraisals.Contains(appraisalId), assignedBy);
        if (decision.Outcome is not null)
            return (decision.Outcome, decision.Message, decision.Reason);

        // PMA follow-up is decided by the workflow (int-pma-input completer), so a posted staff id is ignored
        // and must not be able to block the assignment.
        if (state!.IsPma)
            staffId = null;

        if (staffId is not null)
        {
            try
            {
                if (!staffEligibility.TryGetValue(staffId, out var eligible))
                {
                    var groups = await userGroupService.GetGroupsForUserAsync(staffId, cancellationToken);
                    eligible = staffEligibility[staffId] = groups.Contains(FollowupStaffGroup, StringComparer.OrdinalIgnoreCase);
                }

                if (!eligible)
                    return (AutoAssignOutcome.Failed, "Selected follow-up staff is not eligible", AutoAssignReason.StaffIneligible);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auto-assign: could not validate follow-up staff for appraisal {AppraisalId}",
                    appraisalId);
                return (AutoAssignOutcome.Failed, "Could not validate the selected follow-up staff", AutoAssignReason.StaffValidationFailed);
            }
        }

        try
        {
            // Fresh scope per appraisal: a rolled-back resume leaves stale tracked entities and
            // outbox messages in the scoped DbContext, which the next resume would flush.
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new AssignAppraisalCommand(
                    appraisalId,
                    state!.WorkflowInstanceId!.Value,
                    AssigneeCompanyId: winner!.Id.ToString(),
                    AssigneeCompanyName: winner.Name,
                    AssignmentMethod: "Quotation",
                    InternalAppraiserId: staffId,
                    InternalFollowupAssignmentMethod: staffId is null ? "roundrobin" : "manual",
                    DecisionTaken: "EXT"),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-assign failed for appraisal {AppraisalId}", appraisalId);
            // Only the project's domain exceptions are shown; WorkflowService wraps raw activity text in
            // InvalidOperationException, so that and everything else stays in the log.
            // No base fee to bill (AssignAppraisalCommandHandler): nothing was assigned, the admin has to fix the fee.
            if (ex is BadRequestException { Code: AssignAppraisalCommandHandler.FeeBaseMissingCode })
                return (AutoAssignOutcome.Failed, Truncate(ex.Message), AutoAssignReason.FeeBaseMissing);

            return ex is BadRequestException or NotFoundException or ConflictException
                ? (AutoAssignOutcome.Failed, Truncate(ex.Message), AutoAssignReason.AssignRejected)
                : (AutoAssignOutcome.Failed, GenericFailureMessage, AutoAssignReason.AssignFailed);
        }

        // The command committed. From here on this is never reported as Failed because of a read problem.
        try
        {
            var after = (await stateReader.GetAsync([appraisalId], cancellationToken)).FirstOrDefault();
            return after?.PendingActivityId switch
            {
                AppraisalAssignState.ExtAssignmentActivityId => (AutoAssignOutcome.Assigned, null, null),
                AppraisalAssignState.AssignmentActivityId =>
                    (AutoAssignOutcome.Failed, NoMatchMessage, AutoAssignReason.NoMatch),
                // No row or no active task: nothing says it failed, and the assign committed.
                null => (AutoAssignOutcome.Assigned, UnverifiedMessage, AutoAssignReason.Unverified),
                // The assign committed and the workflow moved on (e.g. already past ext assignment): Assigned, not Failed.
                var other => (AutoAssignOutcome.Assigned, $"Workflow is at '{other}' after assignment",
                    AutoAssignReason.WorkflowElsewhere)
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auto-assign: could not verify the state of appraisal {AppraisalId}", appraisalId);
            return (AutoAssignOutcome.Assigned, UnverifiedMessage, AutoAssignReason.Unverified);
        }
    }

    private static string Truncate(string message) =>
        message.Length <= MaxMessageLength ? message : message[..MaxMessageLength];
}
