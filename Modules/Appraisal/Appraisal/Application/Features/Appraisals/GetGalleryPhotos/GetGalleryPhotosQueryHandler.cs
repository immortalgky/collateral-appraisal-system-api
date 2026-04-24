using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetGalleryPhotos;

public class GetGalleryPhotosQueryHandler(
    IAppraisalGalleryRepository galleryRepository,
    AppraisalDbContext dbContext,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetGalleryPhotosQuery, GetGalleryPhotosResult>
{
    public async Task<GetGalleryPhotosResult> Handle(
        GetGalleryPhotosQuery query,
        CancellationToken cancellationToken)
    {
        // ── Authorization ─────────────────────────────────────────────────────
        var sharedDocumentIds = await EnforceDocumentAccessAsync(query.AppraisalId, cancellationToken);

        var allPhotos = (await galleryRepository.GetByAppraisalIdAsync(
            query.AppraisalId, cancellationToken)).ToList();

        // v4: ExtAdmin callers see only admin-shared documents
        var photos = sharedDocumentIds is not null
            ? allPhotos.Where(p => sharedDocumentIds.Contains(p.DocumentId)).ToList()
            : allPhotos;

        // Batch-load topic mappings for all photos
        var photoIds = photos.Select(p => p.Id).ToList();
        var topicMappings = await dbContext.GalleryPhotoTopicMappings
            .Where(m => photoIds.Contains(m.GalleryPhotoId))
            .ToListAsync(cancellationToken);

        var topicsByPhoto = topicMappings
            .GroupBy(m => m.GalleryPhotoId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.PhotoTopicId).ToList());

        var dtos = photos.Select(p => new GalleryPhotoDto(
            p.Id,
            p.DocumentId,
            p.PhotoNumber,
            p.PhotoType,
            p.PhotoCategory,
            p.Caption,
            p.Latitude,
            p.Longitude,
            p.CapturedAt,
            p.UploadedAt,
            p.IsInUse,
            topicsByPhoto.GetValueOrDefault(p.Id, []),
            p.FileName,
            p.FilePath,
            p.FileExtension,
            p.MimeType,
            p.FileSizeBytes,
            p.UploadedByName
        )).ToList();

        return new GetGalleryPhotosResult(dtos);
    }

    /// <summary>
    /// Enforces <see cref="DocumentAccessPolicy"/> for the current caller.
    /// Loads the RM requestor and any active invitation context from the database.
    /// Admin/IntAdmin roles skip the database lookup.
    /// Returns the shared DocumentId set (non-null only for ExtAdmin; null = no filtering needed).
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> EnforceDocumentAccessAsync(Guid appraisalId, CancellationToken cancellationToken)
    {
        // Admins always pass — skip expensive lookups
        if (currentUser.IsInRole("Admin") || currentUser.IsInRole("IntAdmin"))
            return null;

        using var connection = connectionFactory.GetOpenConnection();

        // Load the RM requestor for the appraisal's linked request (C7: RM must own the request)
        var rmRequestorId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT r.Requestor
            FROM   request.Requests r
            INNER JOIN appraisal.Appraisals a ON a.RequestId = r.Id
            WHERE  a.Id = @AppraisalId
            """,
            new { AppraisalId = appraisalId });

        // Load active invitations that cover this specific appraisal (C6: per-appraisal scope for ExtAdmin)
        var invitations = await connection.QueryAsync<(Guid CompanyId, string InvitationStatus, string QuotationStatus)>(
            """
            SELECT qi.CompanyId, qi.Status AS InvitationStatus, qr.Status AS QuotationStatus
            FROM   appraisal.QuotationInvitations qi
            INNER JOIN appraisal.QuotationRequests qr ON qr.Id = qi.QuotationRequestId
            INNER JOIN appraisal.QuotationRequestAppraisals qra ON qra.QuotationRequestId = qr.Id
            WHERE  qra.AppraisalId = @AppraisalId
            """,
            new { AppraisalId = appraisalId });

        // M6: post-finalize, the winning company still needs doc access to execute the work.
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

        // v4: for ExtAdmin callers, load the admin-shared document IDs so the caller
        // can filter the gallery to only shared documents.
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
