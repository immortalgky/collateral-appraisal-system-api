using Appraisal.Application.Features.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public class GetAppraisalDocumentsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetAppraisalDocumentsQuery, GetAppraisalDocumentsResult>
{
    public async Task<GetAppraisalDocumentsResult> Handle(
        GetAppraisalDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
                            SELECT
                                dt.[Code]      AS [TypeCode],
                                dt.[Name]      AS [TypeName],
                                dt.[NameTh]    AS [TypeNameTh],
                                dt.[Category]  AS [TypeCategory],
                                dt.[SortOrder] AS [TypeSortOrder],
                                ad.[Id]                                          AS [Id],
                                ad.[DocumentId]                                  AS [DocumentId],
                                COALESCE(d.[FileName], ad.[FileName])            AS [FileName],
                                COALESCE(d.[MimeType], ad.[MimeType])            AS [MimeType],
                                COALESCE(d.[FileSizeBytes], ad.[FileSizeBytes]) AS [FileSizeBytes],
                                ad.[Notes]                                       AS [Notes],
                                ad.[SortOrder]                                   AS [FileSortOrder],
                                ad.[CreatedAt]                                   AS [UploadedAt],
                                ad.[CreatedBy]                                   AS [UploadedBy],
                                ad.[UploadedByName]                             AS [UploadedByName]
                            FROM [parameter].[DocumentTypes] dt
                            LEFT JOIN [appraisal].[AppraisalDocuments] ad
                                ON ad.[DocumentTypeCode] = dt.[Code] AND ad.[AppraisalId] = @AppraisalId
                            LEFT JOIN [document].[Documents] d ON d.[Id] = ad.[DocumentId]
                            WHERE dt.[Category] IN ('VAL_DOC', 'VAL_REPORT') AND dt.[IsActive] = 1
                            ORDER BY dt.[SortOrder], dt.[Code], ad.[SortOrder], ad.[Id];

                            SELECT [Status] FROM [appraisal].[Appraisals] WHERE [Id] = @AppraisalId;
                            """;

        var connection = connectionFactory.GetOpenConnection();
        await using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, new { query.AppraisalId }, cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<DocumentRow>()).ToList();
        var status = await grid.ReadFirstOrDefaultAsync<string>();

        /*
         * The same release rule the brief endpoint applies, enforced here too.
         *
         * The brief withholds document ids until the committee approves the price, because a
         * DocumentId is a working download link on its own (/documents/{id}/download). This
         * checklist returns the very same ids for the very same appraisal, on login alone — so
         * without this a credit user could read the brief, see an empty document list, call this
         * endpoint, and download the files anyway. One extra request was all the rule cost.
         *
         * Only tracking-only callers are affected: RequestMaker and every appraisal role open
         * this checklist while the work is in progress, which is the whole point of it.
         */
        if (AppraisalFieldScope.IsTrackingOnly(currentUser) && !AppraisalFieldScope.IsReleased(status))
        {
            rows = rows.Select(r => r with { Id = null, DocumentId = null }).ToList();
        }

        var types = rows
            .GroupBy(r => new { r.TypeCode, r.TypeName, r.TypeNameTh, r.TypeCategory, r.TypeSortOrder })
            .OrderBy(g => g.Key.TypeSortOrder)
            .ThenBy(g => g.Key.TypeCode)
            .Select(g =>
            {
                var files = g
                    .Where(r => r.Id is not null)
                    .Select(r => new AppraisalDocumentFileDto(
                        r.Id!.Value,
                        r.DocumentId,
                        r.FileName,
                        r.MimeType,
                        r.FileSizeBytes,
                        r.Notes,
                        r.FileSortOrder ?? 0,
                        r.UploadedAt,
                        r.UploadedBy,
                        r.UploadedByName))
                    .ToList();

                return new AppraisalDocumentTypeDto(
                    g.Key.TypeCode,
                    g.Key.TypeName,
                    g.Key.TypeNameTh,
                    g.Key.TypeCategory,
                    files.Count,
                    files);
            })
            .ToList();

        var typesWithFiles = types.Count(t => t.TotalFiles > 0);

        return new GetAppraisalDocumentsResult(types.Count, typesWithFiles, types);
    }

    /// <summary>Flat row from the DocumentTypes LEFT JOIN AppraisalDocuments/Documents query.</summary>
    private sealed record DocumentRow(
        string TypeCode,
        string TypeName,
        string? TypeNameTh,
        string? TypeCategory,
        int TypeSortOrder,
        Guid? Id,
        Guid? DocumentId,
        string? FileName,
        string? MimeType,
        long? FileSizeBytes,
        string? Notes,
        int? FileSortOrder,
        DateTime? UploadedAt,
        string? UploadedBy,
        string? UploadedByName);
}
