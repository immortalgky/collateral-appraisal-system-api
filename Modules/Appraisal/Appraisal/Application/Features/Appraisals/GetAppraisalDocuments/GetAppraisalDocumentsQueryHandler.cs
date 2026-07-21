using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public class GetAppraisalDocumentsQueryHandler(ISqlConnectionFactory connectionFactory)
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
                            """;

        var connection = connectionFactory.GetOpenConnection();
        var rows = (await connection.QueryAsync<DocumentRow>(sql, new { query.AppraisalId })).ToList();

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
