using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.SendQuotation;

/// <summary>
/// Handles the explicit Send action (POST /quotations/{id}/send).
/// Admin-only. Transitions Draft → Sent and emits QuotationStartedIntegrationEvent.
/// C8: This is the explicit send step; CreateNewDraft stays in Draft.
/// </summary>
public class SendQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<SendQuotationCommand, SendQuotationResult>
{
    public async Task<SendQuotationResult> Handle(
        SendQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdWithSharedDocumentsAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        // m8: only the admin who owns the Draft may send it (parity with add-to-existing on StartQuotationFromTask)
        if (quotation.RequestedBy != currentUser.Username)
            throw new UnauthorizedAccessException(
                "Only the admin who created this draft can send it.");

        if (quotation.Status != "Draft")
            throw new BadRequestException(
                $"Cannot send quotation in status '{quotation.Status}'. Only Draft quotations can be sent.");

        // v7: every appraisal that has any uploadable documents must have at least one shared document picked.
        // Appraisals whose request has no documents at all are excluded — nothing to pick.
        var appraisalsWithShared = quotation.SharedDocuments
            .Select(sd => sd.AppraisalId)
            .ToHashSet();

        var uncoveredAppraisalIds = quotation.Appraisals
            .Select(a => a.AppraisalId)
            .Where(id => !appraisalsWithShared.Contains(id))
            .ToArray();

        if (uncoveredAppraisalIds.Length > 0)
        {
            var appraisalsWithAnyDocs = await ResolveAppraisalsWithAnyDocumentsAsync(uncoveredAppraisalIds);
            var missing = uncoveredAppraisalIds
                .Where(id => appraisalsWithAnyDocs.Contains(id))
                .ToArray();

            if (missing.Length > 0)
            {
                throw new BadRequestException(
                    $"Every appraisal with uploaded documents must have at least one shared document before sending. " +
                    $"Missing for appraisal(s): {string.Join(", ", missing)}.");
            }
        }

        // Domain method enforces: at least one appraisal + at least one invitation
        quotation.Send();

        quotationRepository.Update(quotation);

        var invitedCompanyIds = quotation.Invitations
            .Select(i => i.CompanyId)
            .ToArray();

        var appraisalIds = quotation.Appraisals
            .Select(a => a.AppraisalId)
            .ToArray();

        // C8: Emit QuotationStartedIntegrationEvent here (moved from StartQuotationFromTask)
        outbox.Publish(new QuotationStartedIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            // v2: multi-appraisal; use first appraisal for backward compat field
            AppraisalId = quotation.FirstAppraisalId ?? Guid.Empty,
            AppraisalIds = appraisalIds,
            RequestId = quotation.RequestId ?? Guid.Empty,
            WorkflowInstanceId = quotation.WorkflowInstanceId ?? Guid.Empty,
            TaskExecutionId = quotation.TaskExecutionId ?? Guid.Empty,
            DueDate = quotation.DueDate,
            InvitedCompanyIds = invitedCompanyIds,
            RmUserId = quotation.RmUserId,
            RmUsername = quotation.RmUsername,
            StartedByUsername = currentUser.Username
        }, correlationId: quotation.Id.ToString());

        return new SendQuotationResult(
            quotation.Id,
            quotation.Status,
            quotation.TotalAppraisals,
            quotation.TotalCompaniesInvited);
    }

    /// <summary>
    /// Returns the subset of <paramref name="appraisalIds"/> whose owning request has at least one
    /// uploaded document (either RequestLevel or TitleLevel). Appraisals not in the returned set
    /// have nothing the admin could share, so the "at least one shared document" rule is skipped.
    /// </summary>
    private async Task<HashSet<Guid>> ResolveAppraisalsWithAnyDocumentsAsync(Guid[] appraisalIds)
    {
        if (appraisalIds.Length == 0)
            return new HashSet<Guid>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<Guid>(
            """
            SELECT DISTINCT a.Id
            FROM appraisal.Appraisals a
            WHERE a.Id IN @AppraisalIds
              AND (
                EXISTS (
                    SELECT 1 FROM [request].[RequestDocuments] rd
                    WHERE rd.RequestId = a.RequestId AND rd.DocumentId IS NOT NULL
                )
                OR EXISTS (
                    SELECT 1
                    FROM [request].[RequestTitles] t
                    INNER JOIN [request].[RequestTitleDocuments] td ON td.TitleId = t.Id
                    WHERE t.RequestId = a.RequestId AND td.DocumentId IS NOT NULL
                )
              )
            """,
            new { AppraisalIds = appraisalIds });

        return rows.ToHashSet();
    }
}
