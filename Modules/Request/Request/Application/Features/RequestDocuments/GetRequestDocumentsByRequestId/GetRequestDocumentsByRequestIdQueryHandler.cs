using Dapper;

namespace Request.Application.Features.RequestDocuments.GetRequestDocumentsByRequestId;

internal class GetRequestDocumentsByRequestIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetRequestDocumentsByRequestIdQuery, GetRequestDocumentsByRequestIdResult>
{
    public async Task<GetRequestDocumentsByRequestIdResult> Handle(
        GetRequestDocumentsByRequestIdQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           -- Result set 1: Request-level documents
                           SELECT [Id], [DocumentId], [DocumentType], [FileName], [FilePath],
                                  [Notes], [IsRequired], [UploadedBy], [UploadedByName], [UploadedAt]
                           FROM [request].[RequestDocuments]
                           WHERE [RequestId] = @RequestId
                           ORDER BY [Id] ASC;

                           -- Result set 2: Title info + title documents
                           SELECT
                               t.[Id] AS [TitleId],
                               CASE
                                   WHEN t.[CollateralType] IN ('L', 'LB', 'U', 'LSL', 'LS', 'LSU') THEN t.[TitleNumber]
                                   WHEN t.[CollateralType] = 'VEH' THEN t.[LicensePlateNumber]
                                   WHEN t.[CollateralType] = 'VES' THEN t.[VesselRegistrationNumber]
                                   WHEN t.[CollateralType] = 'MAC' THEN t.[RegistrationNumber]
                                   ELSE t.[CollateralType]
                               END AS [TitleIdentifier],
                               t.[CollateralType],
                               td.[Id], td.[DocumentId], td.[DocumentType], td.[FileName], td.[FilePath],
                               td.[Notes], td.[IsRequired], td.[UploadedBy], td.[UploadedByName], td.[UploadedAt]
                           FROM [request].[RequestTitles] t
                           LEFT JOIN [request].[RequestTitleDocuments] td ON t.[Id] = td.[TitleId]
                           WHERE t.[RequestId] = @RequestId
                           ORDER BY t.[CreatedAt] ASC, td.[Id] ASC;
                           """;

        var connection = connectionFactory.GetOpenConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new { query.RequestId });

        // Result set 1: request-level documents
        var requestDocs = (await multi.ReadAsync<DocumentItemDto>()).ToList();

        // Result set 2: title rows (flat join result)
        var titleRows = (await multi.ReadAsync<TitleDocumentRow>()).ToList();

        var sections = new List<DocumentSectionDto>();

        // Add request-level section if there are request documents
        if (requestDocs.Count > 0)
        {
            var uploadedCount = requestDocs.Count(d => d.DocumentId is not null);
            sections.Add(new DocumentSectionDto(
                null, null, null,
                requestDocs.Count, uploadedCount,
                requestDocs));
        }

        // Group title rows by TitleId to build per-title sections
        var titleGroups = titleRows.GroupBy(r => new
        {
            r.TitleId,
            r.TitleIdentifier,
            r.CollateralType
        });

        foreach (var group in titleGroups)
        {
            // When LEFT JOIN returns null td.Id, the title has no documents
            var docs = group
                .Where(r => r.Id is not null)
                .Select(r => new DocumentItemDto(
                    r.Id!.Value,
                    r.DocumentId,
                    r.DocumentType,
                    r.FileName,
                    r.FilePath,
                    r.Notes,
                    r.IsRequired ?? false,
                    r.UploadedBy,
                    r.UploadedByName,
                    r.UploadedAt))
                .ToList();

            var uploaded = docs.Count(d => d.DocumentId is not null);
            sections.Add(new DocumentSectionDto(
                group.Key.TitleId,
                group.Key.TitleIdentifier,
                group.Key.CollateralType,
                docs.Count, uploaded,
                docs));
        }

        var totalDocuments = sections.Sum(s => s.TotalDocuments);
        var totalUploaded = sections.Sum(s => s.UploadedDocuments);

        return new GetRequestDocumentsByRequestIdResult(totalDocuments, totalUploaded, sections);
    }
}

/// <summary>
/// Flat row from the title LEFT JOIN documents query — used only inside the handler.
/// </summary>
internal record TitleDocumentRow(
    Guid TitleId,
    string? TitleIdentifier,
    string? CollateralType,
    Guid? Id,
    Guid? DocumentId,
    string? DocumentType,
    string? FileName,
    string? FilePath,
    string? Notes,
    bool? IsRequired,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt);