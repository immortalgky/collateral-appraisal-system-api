using Dapper;
using Request.Domain.RequestTitles;

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
                           SELECT rd.[Id], rd.[DocumentId], rd.[DocumentType], dt.[Name] AS [DocumentTypeName],
                                  rd.[FileName], rd.[FilePath], rd.[Notes], rd.[IsRequired],
                                  rd.[UploadedBy], rd.[UploadedByName], rd.[UploadedAt]
                           FROM [request].[RequestDocuments] rd
                           LEFT JOIN [parameter].[DocumentTypes] dt ON dt.[Code] = rd.[DocumentType]
                           WHERE rd.[RequestId] = @RequestId
                           ORDER BY rd.[Id] ASC;

                           -- Result set 2: Title info + title documents
                           SELECT
                               t.[Id] AS [TitleId],
                               CASE
                                   WHEN t.[CollateralType] IN ('L', 'B', 'LB', 'U', 'LSL', 'LSB', 'LS', 'LSU') THEN t.[TitleNumber]
                                   WHEN t.[CollateralType] = 'VEH' THEN t.[LicensePlateNumber]
                                   WHEN t.[CollateralType] = 'VES' THEN t.[VesselRegistrationNumber]
                                   WHEN t.[CollateralType] = 'MAC' THEN t.[RegistrationNumber]
                                   ELSE t.[CollateralType]
                               END AS [TitleIdentifier],
                               t.[CollateralType],
                               td.[Id], td.[DocumentId], td.[DocumentType], dt.[Name] AS [DocumentTypeName],
                               td.[FileName], td.[FilePath], td.[Notes], td.[IsRequired],
                               td.[UploadedBy], td.[UploadedByName], td.[UploadedAt]
                           FROM [request].[RequestTitles] t
                           LEFT JOIN [request].[RequestTitleDocuments] td ON t.[Id] = td.[TitleId]
                           LEFT JOIN [parameter].[DocumentTypes] dt ON dt.[Code] = td.[DocumentType]
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
                null, null, null, null, "Application Documents",
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
                    r.DocumentTypeName,
                    r.FileName,
                    r.FilePath,
                    r.Notes,
                    r.IsRequired ?? false,
                    r.UploadedBy,
                    r.UploadedByName,
                    r.UploadedAt))
                .ToList();

            var collateralTypeCode = group.Key.CollateralType;
            string? collateralTypeName = null;
            if (collateralTypeCode is not null && CollateralType.TryFromCode(collateralTypeCode, out var ct))
            {
                collateralTypeName = ct?.DisplayName;
            }

            var sectionLabel = BuildSectionLabel(
                group.Key.TitleIdentifier,
                collateralTypeCode,
                collateralTypeName);

            var uploaded = docs.Count(d => d.DocumentId is not null);
            sections.Add(new DocumentSectionDto(
                group.Key.TitleId,
                group.Key.TitleIdentifier,
                collateralTypeCode,
                collateralTypeName,
                sectionLabel,
                docs.Count, uploaded,
                docs));
        }

        var totalDocuments = sections.Sum(s => s.TotalDocuments);
        var totalUploaded = sections.Sum(s => s.UploadedDocuments);

        return new GetRequestDocumentsByRequestIdResult(totalDocuments, totalUploaded, sections);
    }

    private static string BuildSectionLabel(
        string? titleIdentifier,
        string? collateralType,
        string? collateralTypeName)
    {
        if (string.IsNullOrWhiteSpace(titleIdentifier))
        {
            return collateralTypeName ?? collateralType ?? "Section";
        }

        return collateralType switch
        {
            "VEH" => $"Vehicle · Plate {titleIdentifier}",
            "VES" => $"Vessel · Reg. {titleIdentifier}",
            "MAC" => $"Machine · Reg. {titleIdentifier}",
            "L" or "LB" or "U" or "LSL" or "LS" or "LSU" or "LSB" or "B"
                => $"{collateralTypeName} · Title No. {titleIdentifier}",
            _ => titleIdentifier
        };
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
    string? DocumentTypeName,
    string? FileName,
    string? FilePath,
    string? Notes,
    bool? IsRequired,
    string? UploadedBy,
    string? UploadedByName,
    DateTime? UploadedAt);
