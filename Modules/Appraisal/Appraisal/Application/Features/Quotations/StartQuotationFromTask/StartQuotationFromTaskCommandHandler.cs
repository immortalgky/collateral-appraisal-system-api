using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Appraisals;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

public class StartQuotationFromTaskCommandHandler(
    IQuotationRepository quotationRepository,
    IAppraisalRepository appraisalRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger,
    IDateTimeProvider dateTimeProvider,
    ILogger<StartQuotationFromTaskCommandHandler> logger)
    : ICommandHandler<StartQuotationFromTaskCommand, StartQuotationFromTaskResult>
{
    public async Task<StartQuotationFromTaskResult> Handle(
        StartQuotationFromTaskCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var requestedBy = currentUser.Username
            ?? throw new UnauthorizedAccessException("Cannot resolve current user username from token");
        var adminUserId = currentUser.UserId;

        // ── Path A: add appraisal to an existing Draft ────────────────────────
        if (command.ExistingQuotationRequestId.HasValue)
        {
            return await AddToExistingDraftAsync(command, requestedBy, adminUserId, cancellationToken);
        }

        // ── Path B: create a new Draft ────────────────────────────────────────
        return await CreateNewDraftAsync(command, requestedBy, adminUserId, cancellationToken);
    }

    private async Task<StartQuotationFromTaskResult> AddToExistingDraftAsync(
        StartQuotationFromTaskCommand command,
        string requestedBy,
        Guid? adminUserId,
        CancellationToken cancellationToken)
    {
        var existingId = command.ExistingQuotationRequestId!.Value;

        var quotation = await quotationRepository.GetByIdAsync(existingId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{existingId}' not found.");

        if (quotation.RequestedBy != requestedBy)
            throw new UnauthorizedAccessException("You can only add appraisals to your own Draft quotation.");

        if (quotation.Status != "Draft")
            throw new BadRequestException($"Cannot add appraisal to quotation in status '{quotation.Status}'. Only Draft quotations accept new appraisals.");

        // Active-quotation uniqueness check (domain guard + transaction-scoped read)
        var alreadyActive = await quotationRepository.HasActiveQuotationForAppraisalAsync(
            command.AppraisalId,
            excludeQuotationRequestId: existingId,
            cancellationToken);

        if (alreadyActive)
            throw new ConflictException(
                $"Appraisal '{command.AppraisalId}' is already part of another non-terminal quotation request.");

        // Deferred until after the guards so rejection paths don't pay for the read. See CreateNewDraftAsync for why this isn't merged with the EF aggregate load.
        var summary = await GetAppraisalSummaryAsync(command.AppraisalId, cancellationToken);

        quotation.AddAppraisal(command.AppraisalId, requestedBy, dateTimeProvider.ApplicationNow);

        // Also add a display item for the new appraisal (used by admin review panel)
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: summary.AppraisalNumber ?? string.Empty,
            propertyType: summary.PropertyType ?? "Unknown",
            propertyLocation: summary.PropertyLocation,
            estimatedValue: summary.EstimatedValue,
            maxAppraisalDays: command.MaxAppraisalDays);

        quotationRepository.Update(quotation);

        outbox.Publish(new AppraisalAddedToQuotationIntegrationEvent
        {
            QuotationRequestId = existingId,
            AppraisalId = command.AppraisalId,
            AdminUserId = adminUserId ?? Guid.Empty
        }, correlationId: existingId.ToString());

        return new StartQuotationFromTaskResult(existingId);
    }

    private async Task<StartQuotationFromTaskResult> CreateNewDraftAsync(
        StartQuotationFromTaskCommand command,
        string requestedBy,
        Guid? adminUserId,
        CancellationToken cancellationToken)
    {
        if (command.InvitedCompanyIds == null || command.InvitedCompanyIds.Count == 0)
            throw new BadRequestException("At least one company must be invited");

        if (command.ExcludedCompanyIds is { Count: > 0 })
        {
            var excluded = command.ExcludedCompanyIds.Intersect(command.InvitedCompanyIds).ToList();
            if (excluded.Count > 0)
                throw new ConflictException(
                    $"Cannot invite companies that previously appraised this collateral (appeal exclusion): {string.Join(", ", excluded)}");
        }

        // Active-quotation uniqueness check
        var alreadyActive = await quotationRepository.HasActiveQuotationForAppraisalAsync(
            command.AppraisalId,
            excludeQuotationRequestId: null,
            cancellationToken);

        if (alreadyActive)
            throw new ConflictException(
                $"Appraisal '{command.AppraisalId}' is already part of another non-terminal quotation request.");

        // Same deferred summary lookup as Path A — kept after guards so a duplicate-active rejection
        // doesn't pay for the read. Path B also loads the EF aggregate below (when assignment fields
        // are set) for CreatePendingAssignment; that aggregate can't supply PropertyType because it
        // lives in the request module, so the two reads serve different purposes.
        var summary = await GetAppraisalSummaryAsync(command.AppraisalId, cancellationToken);

        var rmUsername = await ResolveRmAsync(command.RequestId, cancellationToken);

        var quotation = QuotationRequest.CreateFromTask(
            cutOffTime: command.CutOffTime,
            requestedBy: requestedBy,
            initialAppraisalId: command.AppraisalId,
            requestId: command.RequestId,
            workflowInstanceId: command.WorkflowInstanceId,
            taskExecutionId: command.TaskExecutionId,
            bankingSegment: command.BankingSegment,
            addedBy: requestedBy,
            now: dateTimeProvider.ApplicationNow,
            rmUsername: rmUsername,
            description: null,
            specialRequirements: command.SpecialRequirements);

        // Add the appraisal as a display item
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: summary.AppraisalNumber ?? string.Empty,
            propertyType: summary.PropertyType ?? "Unknown",
            propertyLocation: summary.PropertyLocation,
            estimatedValue: summary.EstimatedValue,
            maxAppraisalDays: command.MaxAppraisalDays);

        // Invite each company
        var distinctCompanyIds = command.InvitedCompanyIds.Distinct().ToList();
        foreach (var companyId in distinctCompanyIds)
            quotation.InviteCompany(companyId);

        // C8: Do NOT call Send() here. The quotation stays in Draft so admin can add
        // more appraisals or adjust invitations before explicitly sending via POST /quotations/{id}/send.
        // QuotationStartedIntegrationEvent is emitted by SendQuotationCommandHandler on explicit send.

        await quotationRepository.AddAsync(quotation, cancellationToken);

        // No pre-Quotation Pending assignment is created here. The workflow's
        // appraisal-assignment task remains the source of truth: it stays open until the
        // admin clicks Assign on the administration screen post-finalize, which advances
        // the workflow and CompanySelectionActivity publishes CompanyAssignedIntegrationEvent.

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.QuotationCreatedFromTask, actionByRole: adminRole);

        return new StartQuotationFromTaskResult(quotation.Id);
    }

    private async Task<AppraisalSummary> GetAppraisalSummaryAsync(
        Guid appraisalId,
        CancellationToken cancellationToken)
    {
        var summaries = await appraisalRepository.GetSummariesAsync(new[] { appraisalId }, cancellationToken);
        return summaries.FirstOrDefault()
            ?? throw new NotFoundException($"Appraisal '{appraisalId}' not found.");
    }

    /// <summary>
    /// Resolves the RM's username from the linked request's Requestor column (employee ID / login string).
    /// Returns null on failure — quotation is still created without RM linkage.
    /// </summary>
    private async Task<string?> ResolveRmAsync(Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = connectionFactory.GetOpenConnection();
            var rmUsername = await connection.QuerySingleOrDefaultAsync<string?>(
                "SELECT Requestor FROM request.Requests WHERE Id = @RequestId",
                new { RequestId = requestId });

            return string.IsNullOrWhiteSpace(rmUsername) ? null : rmUsername;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to resolve RM identity for RequestId={RequestId}. Quotation will be created without RM linkage.",
                requestId);
            return null;
        }
    }
}
