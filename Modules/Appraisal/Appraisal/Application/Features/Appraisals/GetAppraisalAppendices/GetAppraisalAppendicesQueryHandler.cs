using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public class GetAppraisalAppendicesQueryHandler(
    AppraisalDbContext dbContext,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalAppendicesQuery, GetAppraisalAppendicesResult>
{
    public async Task<GetAppraisalAppendicesResult> Handle(
        GetAppraisalAppendicesQuery query,
        CancellationToken cancellationToken)
    {
        // ── Authorization ─────────────────────────────────────────────────────
        var sharedDocumentIds = await EnforceDocumentAccessAsync(query.AppraisalId, cancellationToken);

        // Load appendices with documents (Include only works without Join)
        var appendices = await dbContext.AppraisalAppendices
            .Include(a => a.Documents)
            .Where(a => a.AppraisalId == query.AppraisalId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);

        // Load appendix types as a lookup dictionary
        var typeIds = appendices.Select(a => a.AppendixTypeId).Distinct().ToList();
        var typesById = await dbContext.AppendixTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        // Batch-load gallery photos to resolve DocumentId for each GalleryPhotoId
        var allGalleryPhotoIds = appendices
            .SelectMany(a => a.Documents)
            .Select(d => d.GalleryPhotoId)
            .Distinct()
            .ToList();

        var galleryLookup = await dbContext.AppraisalGallery
            .Where(g => allGalleryPhotoIds.Contains(g.Id))
            .Select(g => new { g.Id, g.DocumentId, g.FileName, g.FilePath, g.FileExtension, g.MimeType, g.FileSizeBytes })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var dtos = appendices
            .Where(a => typesById.ContainsKey(a.AppendixTypeId))
            .Select(a =>
            {
                var type = typesById[a.AppendixTypeId];
                return new AppraisalAppendixDto(
                    a.Id,
                    a.AppendixTypeId,
                    type.Code,
                    type.Name,
                    a.SortOrder,
                    a.LayoutColumns,
                    a.Documents
                        .OrderBy(d => d.DisplaySequence)
                        .Select(d =>
                        {
                            var photo = galleryLookup.GetValueOrDefault(d.GalleryPhotoId);
                            return new AppendixDocumentDto(
                                d.Id,
                                d.GalleryPhotoId,
                                photo?.DocumentId ?? Guid.Empty,
                                d.DisplaySequence,
                                photo?.FileName,
                                photo?.FilePath,
                                photo?.FileExtension,
                                photo?.MimeType,
                                photo?.FileSizeBytes
                            );
                        })
                        // v4: ExtAdmin sees only admin-shared documents
                        .Where(d => sharedDocumentIds is null || sharedDocumentIds.Contains(d.DocumentId))
                        .ToList()
                );
            })
            // v4: remove appendices that end up with 0 visible documents for ExtAdmin
            .Where(a => sharedDocumentIds is null || a.Documents.Count > 0)
            .ToList();

        return new GetAppraisalAppendicesResult(dtos);
    }

    /// <summary>
    /// Enforces <see cref="DocumentAccessPolicy"/> for the current caller.
    /// Admin/IntAdmin roles skip the database lookup.
    /// Returns the shared DocumentId set (non-null only for ExtAdmin; null = no filtering needed).
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> EnforceDocumentAccessAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
            return null;

        using var connection = connectionFactory.GetOpenConnection();

        // RM must own the linked request (C7)
        var rmRequestorId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT r.Requestor
            FROM   request.Requests r
            INNER JOIN appraisal.Appraisals a ON a.RequestId = r.Id
            WHERE  a.Id = @AppraisalId
            """,
            new { AppraisalId = appraisalId });

        // ExtAdmin: scoped to invitations on quotations that contain this specific appraisal (C6)
        var invitations = await connection.QueryAsync<(Guid CompanyId, string InvitationStatus, string QuotationStatus)>(
            """
            SELECT qi.CompanyId, qi.Status AS InvitationStatus, qr.Status AS QuotationStatus
            FROM   appraisal.QuotationInvitations qi
            INNER JOIN appraisal.QuotationRequests qr ON qr.Id = qi.QuotationRequestId
            INNER JOIN appraisal.QuotationRequestAppraisals qra ON qra.QuotationRequestId = qr.Id
            WHERE  qra.AppraisalId = @AppraisalId
            """,
            new { AppraisalId = appraisalId });

        // M6: winning company retains access post-finalize via active AppraisalAssignment
        var assignedCompanyId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT TOP 1 aa.AssigneeCompanyId
            FROM   appraisal.AppraisalAssignments aa
            WHERE  aa.AppraisalId = @AppraisalId
              AND  aa.AssignmentStatus NOT IN ('Rejected','Cancelled')
              AND  aa.AssigneeCompanyId IS NOT NULL
            ORDER BY aa.CreatedOn DESC
            """,
            new { AppraisalId = appraisalId });

        DocumentAccessPolicy.EnsureCanViewAppraisalDocuments(
            appraisalId,
            invitations,
            currentUser,
            rmRequestorId,
            assignedCompanyId);

        // v4: for ExtAdmin callers, load admin-shared document IDs for filtering
        if (!currentUser.IsInRole("ExtAdmin")) return null;

        var callerCompanyId = currentUser.CompanyId;

        var sharedDocumentIds = await connection.QueryAsync<Guid>(
            """
            SELECT qsd.DocumentId
            FROM   appraisal.QuotationSharedDocuments qsd
            INNER JOIN appraisal.QuotationInvitations qi
                ON   qi.QuotationRequestId = qsd.QuotationRequestId
            INNER JOIN appraisal.QuotationRequests qr
                ON   qr.Id = qsd.QuotationRequestId
            WHERE  qsd.AppraisalId = @AppraisalId
              AND  qi.CompanyId = @CallerCompanyId
              AND  qi.Status <> 'Withdrawn'
              AND  qr.Status NOT IN ('Cancelled')
            """,
            new { AppraisalId = appraisalId, CallerCompanyId = callerCompanyId });

        return sharedDocumentIds.ToHashSet();
    }
}
