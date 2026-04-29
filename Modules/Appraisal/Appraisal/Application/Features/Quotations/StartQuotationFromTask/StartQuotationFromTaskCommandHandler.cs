using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Appraisals;
using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.StartQuotationFromTask;

public class StartQuotationFromTaskCommandHandler(
    IQuotationRepository quotationRepository,
    IAppraisalRepository appraisalRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger,
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

        quotation.AddAppraisal(command.AppraisalId, requestedBy);

        // Also add a display item for the new appraisal (used by admin review panel)
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: command.AppraisalNumber,
            propertyType: command.PropertyType,
            propertyLocation: command.PropertyLocation,
            estimatedValue: command.EstimatedValue,
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

        // Active-quotation uniqueness check
        var alreadyActive = await quotationRepository.HasActiveQuotationForAppraisalAsync(
            command.AppraisalId,
            excludeQuotationRequestId: null,
            cancellationToken);

        if (alreadyActive)
            throw new ConflictException(
                $"Appraisal '{command.AppraisalId}' is already part of another non-terminal quotation request.");

        var (rmUserId, rmUsername) = await ResolveRmAsync(command.RequestId, cancellationToken);

        var quotation = QuotationRequest.CreateFromTask(
            dueDate: command.DueDate,
            requestedBy: requestedBy,
            initialAppraisalId: command.AppraisalId,
            requestId: command.RequestId,
            workflowInstanceId: command.WorkflowInstanceId,
            taskExecutionId: command.TaskExecutionId,
            bankingSegment: command.BankingSegment,
            addedBy: requestedBy,
            rmUserId: rmUserId,
            rmUsername: rmUsername,
            description: null,
            specialRequirements: command.SpecialRequirements);

        // Add the appraisal as a display item
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: command.AppraisalNumber,
            propertyType: command.PropertyType,
            propertyLocation: command.PropertyLocation,
            estimatedValue: command.EstimatedValue,
            maxAppraisalDays: command.MaxAppraisalDays);

        // Invite each company
        var distinctCompanyIds = command.InvitedCompanyIds.Distinct().ToList();
        foreach (var companyId in distinctCompanyIds)
            quotation.InviteCompany(companyId);

        // C8: Do NOT call Send() here. The quotation stays in Draft so admin can add
        // more appraisals or adjust invitations before explicitly sending via POST /quotations/{id}/send.
        // QuotationStartedIntegrationEvent is emitted by SendQuotationCommandHandler on explicit send.

        await quotationRepository.AddAsync(quotation, cancellationToken);

        // Pre-register a Pending assignment so we capture the admin's selection (type + method)
        // before the quotation winner is known. Promoted to Assigned when quotation is finalized.
        if (!string.IsNullOrWhiteSpace(command.AssignmentType) && !string.IsNullOrWhiteSpace(command.AssignmentMethod))
        {
            var appraisal = await appraisalRepository.GetByIdAsync(command.AppraisalId, cancellationToken)
                            ?? throw new NotFoundException($"Appraisal '{command.AppraisalId}' not found.");
            appraisal.CreatePendingAssignment(
                command.AssignmentType,
                command.AssignmentMethod,
                command.InternalFollowupAssignmentMethod,
                quotationRequestId: quotation.Id,
                registeredBy: requestedBy);
            await appraisalRepository.UpdateAsync(appraisal, cancellationToken);
        }

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.QuotationCreatedFromTask, actionByRole: adminRole);

        return new StartQuotationFromTaskResult(quotation.Id);
    }

    /// <summary>
    /// Resolves the RM's identity from the linked request.
    /// The 'Requestor' column stores the employee ID / username (a short string, e.g. "EMP001").
    /// Returns (null, username) — RmUserId is kept null since username is the authoritative identifier
    /// used by workflow task assignment.
    /// </summary>
    private async Task<(Guid? RmUserId, string? RmUsername)> ResolveRmAsync(
        Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = connectionFactory.GetOpenConnection();
            var rmUsername = await connection.QuerySingleOrDefaultAsync<string?>(
                "SELECT Requestor FROM request.Requests WHERE Id = @RequestId",
                new { RequestId = requestId });

            // Requestor column holds username (employee ID), not a Guid.
            // We retain RmUserId as null — username is what the workflow assignee resolver needs.
            return (null, string.IsNullOrWhiteSpace(rmUsername) ? null : rmUsername);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to resolve RM identity for RequestId={RequestId}. Quotation will be created without RM linkage.",
                requestId);
            return (null, null);
        }
    }
}
