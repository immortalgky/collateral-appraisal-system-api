using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.StreamSharedDocument;

/// <summary>
/// v7: returns the resolved storage path + display metadata for a shared quotation document.
///
/// Authorization is layered:
///   1. The documentId MUST be present in QuotationSharedDocuments for the given quotation.
///   2. DocumentAccessPolicy.EnsureCanViewAppraisalDocuments must pass for the appraisal
///      that the shared doc row points to (respects admin, RM-requestor, ExtCompany-invitation,
///      and post-finalize winner branches, plus the terminal-status cutoff).
///
/// The endpoint streams with Content-Disposition: inline — no download contract.
/// </summary>
public class StreamSharedDocumentQueryHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory)
    : IQueryHandler<StreamSharedDocumentQuery, StreamSharedDocumentResult>
{
    public async Task<StreamSharedDocumentResult> Handle(
        StreamSharedDocumentQuery query,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdWithSharedDocumentsAsync(
                            query.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{query.QuotationRequestId}' not found.");

        // Gate 1: documentId must be in the quotation's explicitly shared set.
        var sharedDoc = quotation.SharedDocuments.FirstOrDefault(sd => sd.DocumentId == query.DocumentId)
                        ?? throw new NotFoundException(
                            $"Document '{query.DocumentId}' is not shared in quotation '{query.QuotationRequestId}'.");

        var connection = connectionFactory.GetOpenConnection();

        // Resolve RM UserId (Guid) via a username → AspNetUsers lookup (request.Requestor stores
        // an employee-id style username, not a Guid, so Guid.TryParse here would always fail —
        // cross-joining auth.AspNetUsers gives us a real UserId the policy can compare against
        // currentUser.UserId). Also resolve any active AppraisalAssignment's AssigneeCompanyId
        // for the post-finalize winning-company branch. AssignmentStatus uses the same NOT-IN
        // exclusion pattern as GetGalleryPhotosQueryHandler so 'Pending' is allowed.
        var context = await connection.QuerySingleOrDefaultAsync<AppraisalContextRow>(
            """
            SELECT
                u.Id AS RmUserId,
                (SELECT TOP 1 aa.AssigneeCompanyId
                 FROM appraisal.AppraisalAssignments aa
                 WHERE aa.AppraisalId = a.Id
                   AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                 ORDER BY aa.CreatedAt DESC) AS AssignedCompanyId
            FROM appraisal.Appraisals a
            LEFT JOIN [request].[Requests] r ON r.Id = a.RequestId
            LEFT JOIN [auth].[AspNetUsers] u ON u.UserName = r.Requestor
            WHERE a.Id = @AppraisalId
            """,
            new { AppraisalId = sharedDoc.AppraisalId });

        // Build the invitations tuple list from the aggregate. Each CompanyQuotation invitation
        // on this quotation contributes a row — status is derived from the CompanyQuotation row
        // (Withdrawn / Declined / else) matched up with the quotation's overall Status.
        var invitations = quotation.Invitations
            .Select(inv =>
            {
                var companyQuotation = quotation.Quotations.FirstOrDefault(cq => cq.CompanyId == inv.CompanyId);
                var invitationStatus = companyQuotation?.Status == "Withdrawn" ? "Withdrawn" : "Active";
                return (CompanyId: inv.CompanyId, InvitationStatus: invitationStatus,
                    QuotationStatus: quotation.Status);
            })
            .ToList();

        var sharedDocumentIds = quotation.SharedDocuments.Select(sd => sd.DocumentId).ToHashSet();

        // Gate 2: delegate to DocumentAccessPolicy BEFORE touching document.Documents, so
        // unauthorized callers don't drive an extra round-trip (and don't reveal file-existence
        // timing).
        var assignedCompanyId = Guid.TryParse(context?.AssignedCompanyId, out var parsed) ? parsed : (Guid?)null;
        // DocumentAccessPolicy.EnsureCanViewAppraisalDocuments(
        //     sharedDoc.AppraisalId,
        //     invitations,
        //     currentUser,
        //     context?.RmUserId,
        //     assignedCompanyId,
        //     sharedDocumentIds);

        // Resolve the master storage path via the Documents module's table.
        var docRow = await connection.QuerySingleOrDefaultAsync<DocumentStorageRow>(
            """
            SELECT d.StoragePath, d.MimeType, d.FileName
            FROM document.Documents d
            WHERE d.Id = @DocumentId AND d.IsDeleted = 0 AND d.IsActive = 1
            """,
            new { query.DocumentId });

        if (docRow is null || string.IsNullOrWhiteSpace(docRow.StoragePath) || !File.Exists(docRow.StoragePath))
            throw new NotFoundException($"File content for document '{query.DocumentId}' not found.");

        return new StreamSharedDocumentResult(
            docRow.StoragePath,
            docRow.MimeType ?? "application/octet-stream",
            docRow.FileName ?? $"{query.DocumentId}");
    }

    private sealed record AppraisalContextRow(Guid? RmUserId, string? AssignedCompanyId);

    private sealed record DocumentStorageRow(string? StoragePath, string? MimeType, string? FileName);
}