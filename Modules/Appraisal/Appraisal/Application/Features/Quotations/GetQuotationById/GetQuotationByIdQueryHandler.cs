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
        var filteredQuotations = QuotationAccessPolicy
            .FilterCompanyQuotationsForView(quotation.Quotations, currentUser)
            .ToList();

        // Resolve company names once for the visible quotations so the FE can render
        // the company column without a second round-trip per row.
        var visibleCompanyNames = await ResolveCompanyNamesAsync(
            filteredQuotations.Select(cq => cq.CompanyId).Distinct().ToArray());

        // Resolve negotiation initiator display names so the FE can label rows with the
        // person's name instead of just the role ("Admin" / "Company").
        var negotiationUserIds = filteredQuotations
            .SelectMany(cq => cq.Negotiations)
            .Where(n => n.InitiatedByUserId.HasValue)
            .Select(n => n.InitiatedByUserId!.Value)
            .Distinct()
            .ToArray();
        var negotiationUserNames = await ResolveUserNamesAsync(negotiationUserIds);

        var visibleQuotations = filteredQuotations
            .Select(cq => new CompanyQuotationResult(
                Id: cq.Id,
                CompanyId: cq.CompanyId,
                CompanyName: visibleCompanyNames.GetValueOrDefault(cq.CompanyId),
                QuotationNumber: cq.QuotationNumber,
                Status: cq.Status,
                // CompanyQuotation.CreateDraft initialises SubmittedAt = default(DateTime), which serialises
                // as "0001-01-01T00:00:00" and renders as junk in the UI. Only emit a real value once the
                // company has actually submitted (or progressed past Draft/PendingCheckerReview).
                SubmittedAt: cq.Status is "Draft" or "PendingCheckerReview" ? null : cq.SubmittedAt,
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
                    EstimatedDays: i.EstimatedDays,
                    FeeAmount: i.FeeAmount,
                    Discount: i.Discount,
                    NegotiatedDiscount: i.NegotiatedDiscount,
                    VatPercent: i.VatPercent,
                    ItemNotes: i.ItemNotes
                )).ToList(),
                Negotiations: cq.Negotiations.Select(n => new CompanyQuotationNegotiationResult(
                    Id: n.Id,
                    NegotiationRound: n.NegotiationRound,
                    InitiatedBy: n.InitiatedBy,
                    InitiatedByUserName: n.InitiatedByUserId.HasValue
                        ? negotiationUserNames.GetValueOrDefault(n.InitiatedByUserId.Value)
                        : null,
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
        // Also resolve the current AppraisalNumber — the denormalized value on QuotationRequestItem
        // can be blank if the appraisal hadn't been numbered yet at quotation-creation time.
        var appraisalIds = quotation.Appraisals.Select(a => a.AppraisalId).ToArray();
        var appraisalMetaMap = await ResolveAppraisalMetaAsync(appraisalIds);

        // Resolve customer names keyed by appraisalId (first customer per request, ordered by Id).
        var appraisalCustomerNamesMap = await ResolveAppraisalCustomerNamesAsync(appraisalIds);

        var appraisalResults = quotation.Appraisals
            .Select(a =>
            {
                itemsByAppraisalId.TryGetValue(a.AppraisalId, out var item);
                appraisalMetaMap.TryGetValue(a.AppraisalId, out var meta);
                appraisalCustomerNamesMap.TryGetValue(a.AppraisalId, out var customerName);
                // Prefer the live AppraisalNumber; fall back to the denormalized item value.
                var appraisalNumber = !string.IsNullOrWhiteSpace(meta.AppraisalNumber)
                    ? meta.AppraisalNumber
                    : item?.AppraisalNumber;
                // Prefer the denormalized PropertyType; fall back to the live CollateralType from RequestTitles.
                var propertyType = !string.IsNullOrWhiteSpace(item?.PropertyType)
                    ? item.PropertyType
                    : TranslateCollateralType(meta.CollateralType);
                return new QuotationAppraisalResult(
                    AppraisalId: a.AppraisalId,
                    AddedAt: a.AddedAt,
                    AddedBy: a.AddedBy,
                    AppraisalNumber: appraisalNumber,
                    PropertyType: propertyType,
                    Address: item?.PropertyLocation,
                    LoanType: quotation.BankingSegment,
                    RequestId: meta.RequestId == Guid.Empty ? null : meta.RequestId,
                    CustomerName: customerName,
                    MaxAppraisalDays: item?.MaxAppraisalDays);
            })
            .ToList();

        // v7: enrich SharedDocuments with display metadata pulled from request documents.
        var sharedDocumentsEnriched = await EnrichSharedDocumentsAsync(quotation.SharedDocuments);

        // Resolve invited companies — skip Expired invitations, enrich with CompanyName.
        // External company users do not see the list of rival invitees; they get an empty list.
        List<InvitedCompanyResult> invitedCompanies;
        if (QuotationAccessPolicy.CanViewInvitedCompanies(currentUser))
        {
            var activeInvitationCompanyIds = quotation.Invitations
                .Where(i => i.Status != "Expired")
                .Select(i => i.CompanyId)
                .ToArray();
            var companyNamesMap = await ResolveCompanyNamesAsync(activeInvitationCompanyIds);
            invitedCompanies = activeInvitationCompanyIds
                .Where(id => companyNamesMap.ContainsKey(id))
                .Select(id => new InvitedCompanyResult(id, companyNamesMap[id]))
                .OrderBy(r => r.CompanyName)
                .ToList();
        }
        else
        {
            invitedCompanies = new List<InvitedCompanyResult>();
        }

        return new GetQuotationByIdResult(
            Id: quotation.Id,
            QuotationNumber: quotation.QuotationNumber,
            RequestDate: quotation.RequestDate,
            DueDate: quotation.DueDate,
            Status: quotation.Status,
            RequestedBy: quotation.RequestedBy,
            Description: quotation.RequestDescription,
            SpecialRequirements: quotation.SpecialRequirements,
            TotalAppraisals: quotation.TotalAppraisals,
            TotalCompaniesInvited: quotation.TotalCompaniesInvited,
            TotalQuotationsReceived: quotation.TotalQuotationsReceived,
            SelectedCompanyId: quotation.SelectedCompanyId,
            SelectedQuotationId: quotation.SelectedQuotationId,
            SelectedAt: quotation.SelectedAt,
            SelectionReason: quotation.SelectionReason,
            CancellationReason: quotation.CancellationReason,
            Appraisals: appraisalResults.AsReadOnly(),
            FirstAppraisalId: quotation.FirstAppraisalId,
            RequestId: quotation.RequestId,
            WorkflowInstanceId: quotation.WorkflowInstanceId,
            TaskExecutionId: quotation.TaskExecutionId,
            BankingSegment: quotation.BankingSegment,
            RmUserId: quotation.RmUserId,
            RmUserName: quotation.RmUsername,
            SubmissionsClosedAt: quotation.SubmissionsClosedAt,
            ShortlistSentToRmAt: quotation.ShortlistSentToRmAt,
            ShortlistSentByAdminId: quotation.ShortlistSentByAdminId,
            TotalShortlisted: quotation.TotalShortlisted,
            TentativeWinnerQuotationId: quotation.TentativeWinnerQuotationId,
            TentativelySelectedAt: quotation.TentativelySelectedAt,
            TentativelySelectedBy: quotation.TentativelySelectedBy,
            TentativelySelectedByRole: quotation.TentativelySelectedByRole,
            RmRequestsNegotiation: quotation.RmRequestsNegotiation,
            RmNegotiationNote: quotation.RmNegotiationNote,
            SharedDocuments: sharedDocumentsEnriched,
            CompanyQuotations: visibleQuotations,
            InvitedCompanies: invitedCompanies
        );
    }

    private static string? TranslateCollateralType(string? code) => code switch
    {
        "L"   => "Land",
        "B"   => "Building",
        "LB"  => "Land and Building",
        "U"   => "Condo",
        "LSL" => "Lease Agreement Land",
        "LSB" => "Lease Agreement Building",
        "LS"  => "Lease Agreement Land and Building",
        "LSU" => "Lease Agreement Condo",
        "MAC" => "Machine",
        "VEH" => "Vehicle",
        "VES" => "Vessel",
        _ => code,
    };

    private async Task<Dictionary<Guid, (Guid RequestId, string? AppraisalNumber, string? CollateralType)>> ResolveAppraisalMetaAsync(Guid[] appraisalIds)
    {
        if (appraisalIds.Length == 0)
            return new Dictionary<Guid, (Guid, string?, string?)>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid AppraisalId, Guid RequestId, string? AppraisalNumber, string? CollateralType)>(
            """
            SELECT a.Id AS AppraisalId,
                   a.RequestId,
                   a.AppraisalNumber,
                   (SELECT TOP 1 rp.PropertyType
                    FROM [request].[RequestProperties] rp
                    WHERE rp.RequestId = a.RequestId
                    ORDER BY rp.Id) AS CollateralType
            FROM [appraisal].[Appraisals] a
            WHERE a.Id IN @AppraisalIds
            """,
            new { AppraisalIds = appraisalIds });

        return rows.ToDictionary(
            r => r.AppraisalId,
            r => (r.RequestId, r.AppraisalNumber, r.CollateralType));
    }

    private async Task<Dictionary<Guid, string?>> ResolveAppraisalCustomerNamesAsync(Guid[] appraisalIds)
    {
        if (appraisalIds.Length == 0)
            return new Dictionary<Guid, string?>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid AppraisalId, string? CustomerName)>(
            """
            SELECT a.Id AS AppraisalId,
                   (SELECT TOP 1 rc.Name
                    FROM [request].[RequestCustomers] rc
                    WHERE rc.RequestId = a.RequestId
                    ORDER BY rc.Id) AS CustomerName
            FROM [appraisal].[Appraisals] a
            WHERE a.Id IN @AppraisalIds
            """,
            new { AppraisalIds = appraisalIds });

        return rows.ToDictionary(r => r.AppraisalId, r => r.CustomerName);
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(Guid[] userIds)
    {
        if (userIds.Length == 0)
            return new Dictionary<Guid, string>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid Id, string? FirstName, string? LastName, string? UserName)>(
            """
            SELECT u.Id, u.FirstName, u.LastName, u.UserName
            FROM [auth].[AspNetUsers] u
            WHERE u.Id IN @UserIds
            """,
            new { UserIds = userIds });

        return rows.ToDictionary(
            r => r.Id,
            r =>
            {
                var fullName = $"{r.FirstName} {r.LastName}".Trim();
                return string.IsNullOrWhiteSpace(fullName) ? (r.UserName ?? string.Empty) : fullName;
            });
    }

    private async Task<Dictionary<Guid, string>> ResolveCompanyNamesAsync(Guid[] companyIds)
    {
        if (companyIds.Length == 0)
            return new Dictionary<Guid, string>();

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<(Guid Id, string Name)>(
            """
            SELECT c.Id, c.Name
            FROM auth.Companies c
            WHERE c.Id IN @CompanyIds
            """,
            new { CompanyIds = companyIds });

        return rows.ToDictionary(r => r.Id, r => r.Name);
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
                SELECT rd.DocumentId,
                       rd.FileName,
                       rd.DocumentType AS DocumentTypeCode,
                       dt.Name AS DocumentTypeName,
                       'Application Documents' AS SectionLabel,
                       NULL AS TitleNumber,
                       rd.UploadedAt,
                       rd.UploadedByName,
                       rd.Notes
                FROM [request].[RequestDocuments] rd
                LEFT JOIN [parameter].[DocumentTypes] dt ON dt.[Code] = rd.[DocumentType]
                WHERE rd.DocumentId IN @DocumentIds
                UNION ALL
                SELECT td.DocumentId,
                       td.FileName,
                       td.DocumentType AS DocumentTypeCode,
                       dt.Name AS DocumentTypeName,
                       CASE rt.CollateralType
                           WHEN 'L'   THEN 'Land'
                           WHEN 'B'   THEN 'Building'
                           WHEN 'LB'  THEN 'Land and Building'
                           WHEN 'U'   THEN 'Condo'
                           WHEN 'LSL' THEN 'Lease Agreement Land'
                           WHEN 'LSB' THEN 'Lease Agreement Building'
                           WHEN 'LS'  THEN 'Lease Agreement Land and Building'
                           WHEN 'LSU' THEN 'Lease Agreement Condo'
                           WHEN 'MAC' THEN 'Machine'
                           WHEN 'VEH' THEN 'Vehicle'
                           WHEN 'VES' THEN 'Vessel'
                           ELSE rt.CollateralType
                       END + ' · Title No. ' + ISNULL(rt.TitleNumber, '') AS SectionLabel,
                       rt.TitleNumber AS TitleNumber,
                       td.UploadedAt,
                       td.UploadedByName,
                       td.Notes
                FROM [request].[RequestTitleDocuments] td
                INNER JOIN [request].[RequestTitles] rt ON rt.Id = td.TitleId
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
                    DocumentTypeName: meta?.DocumentTypeName,
                    SectionLabel: meta?.SectionLabel,
                    TitleNumber: meta?.TitleNumber,
                    UploadedAt: meta?.UploadedAt,
                    UploadedByName: meta?.UploadedByName,
                    Notes: meta?.Notes);
            })
            .ToList();
    }

    private sealed record SharedDocumentMetaRow(
        Guid? DocumentId,
        string? FileName,
        string? DocumentTypeCode,
        string? DocumentTypeName,
        string? SectionLabel,
        string? TitleNumber,
        DateTime? UploadedAt,
        string? UploadedByName,
        string? Notes);
}

public class QuotationNotFoundException(Guid id)
    : NotFoundException($"Quotation with ID '{id}' was not found.");
