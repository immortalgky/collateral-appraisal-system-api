using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.GetQuotationById;

public class GetQuotationByIdQueryHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetQuotationByIdQuery, GetQuotationByIdResult>
{
    public async Task<GetQuotationByIdResult> Handle(GetQuotationByIdQuery query, CancellationToken cancellationToken)
    {
        // Load with negotiations so the result includes full negotiation history
        var quotation = await quotationRepository.GetByIdWithNegotiationsAsync(query.Id, cancellationToken)
                        ?? throw new QuotationNotFoundException(query.Id);

        // ── Access control ────────────────────────────────────────────────────
        QuotationAccessPolicy.EnsureCanViewQuotation(quotation, quotation.RmUserId, currentUser);

        // ── Filter visible company quotations by role ─────────────────────────
        var visibleQuotations = QuotationAccessPolicy
            .FilterCompanyQuotationsForView(quotation.Quotations, currentUser)
            .Select(cq => new CompanyQuotationResult(
                Id: cq.Id,
                CompanyId: cq.CompanyId,
                QuotationNumber: cq.QuotationNumber,
                Status: cq.Status,
                TotalQuotedPrice: cq.TotalQuotedPrice,
                OriginalQuotedPrice: cq.OriginalQuotedPrice,
                CurrentNegotiatedPrice: cq.CurrentNegotiatedPrice,
                NegotiationRounds: cq.NegotiationRounds,
                IsShortlisted: cq.IsShortlisted,
                IsWinner: cq.IsWinner,
                EstimatedDays: cq.EstimatedDays,
                ValidUntil: cq.ValidUntil,
                ProposedStartDate: cq.ProposedStartDate,
                ProposedCompletionDate: cq.ProposedCompletionDate,
                Remarks: cq.Remarks,
                Items: cq.Items.Select(i => new CompanyQuotationItemResult(
                    Id: i.Id,
                    AppraisalId: i.AppraisalId,
                    ItemNumber: i.ItemNumber,
                    QuotedPrice: i.QuotedPrice,
                    OriginalQuotedPrice: i.OriginalQuotedPrice,
                    CurrentNegotiatedPrice: i.CurrentNegotiatedPrice,
                    EstimatedDays: i.EstimatedDays
                )).ToList(),
                Negotiations: cq.Negotiations.Select(n => new CompanyQuotationNegotiationResult(
                    Id: n.Id,
                    NegotiationRound: n.NegotiationRound,
                    InitiatedBy: n.InitiatedBy,
                    InitiatedAt: n.InitiatedAt,
                    CounterPrice: n.CounterPrice,
                    Message: n.Message,
                    Status: n.Status,
                    ResponseMessage: n.ResponseMessage,
                    RespondedAt: n.RespondedAt
                )).ToList()
            ))
            .ToList();

        // C2: build a lookup from the denormalized Items collection so we can enrich
        // each appraisal join row with AppraisalNumber, PropertyType, Address, and LoanType.
        // LoanType maps to BankingSegment (stored on the quotation at creation time).
        var itemsByAppraisalId = quotation.Items
            .GroupBy(i => i.AppraisalId)
            .ToDictionary(g => g.Key, g => g.First());

        // v7: resolve each appraisal's RequestId via Dapper — FE needs it to fetch per-appraisal
        // documents from /requests/{requestId}/documents for the "share documents" picker.
        var appraisalRequestIdsMap = await ResolveAppraisalRequestIdsAsync(
            quotation.Appraisals.Select(a => a.AppraisalId).ToArray());

        var appraisalResults = quotation.Appraisals
            .Select(a =>
            {
                itemsByAppraisalId.TryGetValue(a.AppraisalId, out var item);
                appraisalRequestIdsMap.TryGetValue(a.AppraisalId, out var requestId);
                return new QuotationAppraisalResult(
                    AppraisalId: a.AppraisalId,
                    AddedAt: a.AddedAt,
                    AddedBy: a.AddedBy,
                    AppraisalNumber: item?.AppraisalNumber,
                    PropertyType: item?.PropertyType,
                    Address: item?.PropertyLocation,
                    LoanType: quotation.BankingSegment,
                    RequestId: requestId == Guid.Empty ? null : requestId);
            })
            .ToList();

        // v7: enrich SharedDocuments with display metadata pulled from request documents.
        var sharedDocumentsEnriched = await EnrichSharedDocumentsAsync(quotation.SharedDocuments);

        return new GetQuotationByIdResult(
            Id: quotation.Id,
            QuotationNumber: quotation.QuotationNumber,
            RequestDate: quotation.RequestDate,
            DueDate: quotation.DueDate,
            Status: quotation.Status,
            RequestedBy: quotation.RequestedBy,
            RequestedByName: quotation.RequestedByName,
            Description: quotation.RequestDescription,
            SpecialRequirements: quotation.SpecialRequirements,
            TotalAppraisals: quotation.TotalAppraisals,
            TotalCompaniesInvited: quotation.TotalCompaniesInvited,
            TotalQuotationsReceived: quotation.TotalQuotationsReceived,
            SelectedCompanyId: quotation.SelectedCompanyId,
            SelectedQuotationId: quotation.SelectedQuotationId,
            SelectedAt: quotation.SelectedAt,
            SelectionReason: quotation.SelectionReason,
            Appraisals: appraisalResults.AsReadOnly(),
            FirstAppraisalId: quotation.FirstAppraisalId,
            RequestId: quotation.RequestId,
            WorkflowInstanceId: quotation.WorkflowInstanceId,
            TaskExecutionId: quotation.TaskExecutionId,
            BankingSegment: quotation.BankingSegment,
            RmUserId: quotation.RmUserId,
            SubmissionsClosedAt: quotation.SubmissionsClosedAt,
            ShortlistSentToRmAt: quotation.ShortlistSentToRmAt,
            ShortlistSentByAdminId: quotation.ShortlistSentByAdminId,
            TotalShortlisted: quotation.TotalShortlisted,
            TentativeWinnerQuotationId: quotation.TentativeWinnerQuotationId,
            TentativelySelectedAt: quotation.TentativelySelectedAt,
            TentativelySelectedBy: quotation.TentativelySelectedBy,
            TentativelySelectedByRole: quotation.TentativelySelectedByRole,
            SharedDocuments: sharedDocumentsEnriched,
            CompanyQuotations: visibleQuotations
        );
    }

    private async Task<Dictionary<Guid, Guid>> ResolveAppraisalRequestIdsAsync(Guid[] appraisalIds)
    {
        if (appraisalIds.Length == 0)
            return new Dictionary<Guid, Guid>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid AppraisalId, Guid RequestId)>(
            """
            SELECT Id AS AppraisalId, RequestId
            FROM appraisal.Appraisals
            WHERE Id IN @AppraisalIds
            """,
            new { AppraisalIds = appraisalIds });

        return rows.ToDictionary(r => r.AppraisalId, r => r.RequestId);
    }

    private async Task<IReadOnlyList<QuotationSharedDocumentResult>> EnrichSharedDocumentsAsync(
        IReadOnlyCollection<Domain.Quotations.QuotationSharedDocument> sharedDocuments)
    {
        if (sharedDocuments.Count == 0)
            return Array.Empty<QuotationSharedDocumentResult>();

        var documentIds = sharedDocuments.Select(sd => sd.DocumentId).Distinct().ToArray();

        var connection = connectionFactory.GetOpenConnection();

        // Pull display metadata from both source tables. We match by DocumentId only —
        // the caller already trusts the stored Level (validated at set-time).
        var rows = (await connection.QueryAsync<SharedDocumentMetaRow>(
                """
                SELECT rd.DocumentId, rd.FileName, rd.DocumentType AS DocumentTypeCode, dt.Name AS DocumentTypeName
                FROM [request].[RequestDocuments] rd
                LEFT JOIN [parameter].[DocumentTypes] dt ON dt.[Code] = rd.[DocumentType]
                WHERE rd.DocumentId IN @DocumentIds
                UNION ALL
                SELECT td.DocumentId, td.FileName, td.DocumentType AS DocumentTypeCode, dt.Name AS DocumentTypeName
                FROM [request].[RequestTitleDocuments] td
                LEFT JOIN [parameter].[DocumentTypes] dt ON dt.[Code] = td.[DocumentType]
                WHERE td.DocumentId IN @DocumentIds
                """,
                new { DocumentIds = documentIds }))
            .ToList();

        // A DocumentId should live in exactly one of the two tables; first-wins is defensive.
        var metaByDocumentId = rows
            .Where(r => r.DocumentId.HasValue)
            .GroupBy(r => r.DocumentId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return sharedDocuments
            .Select(sd =>
            {
                metaByDocumentId.TryGetValue(sd.DocumentId, out var meta);
                return new QuotationSharedDocumentResult(
                    AppraisalId: sd.AppraisalId,
                    DocumentId: sd.DocumentId,
                    Level: sd.Level,
                    FileName: meta?.FileName,
                    FileType: meta?.DocumentTypeCode,
                    DocumentTypeName: meta?.DocumentTypeName);
            })
            .ToList();
    }

    private sealed record SharedDocumentMetaRow(
        Guid? DocumentId,
        string? FileName,
        string? DocumentTypeCode,
        string? DocumentTypeName);
}

public class QuotationNotFoundException(Guid id)
    : NotFoundException($"Quotation with ID '{id}' was not found.");
