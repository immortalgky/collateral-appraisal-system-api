using System.Data;
using Dapper;

namespace Appraisal.Application.Features.Appraisals.AddAppraisalDocument;

/// <summary>
/// The two lookups every "attach a valuation document" path shares, so AddAppraisalDocument and
/// CorrectAppraisalDocuments cannot drift on what counts as a valid type or where a new file sorts.
/// </summary>
public static class AppraisalDocumentQueries
{
    public static async Task<bool> IsValuationDocumentTypeAsync(IDbConnection connection, string typeCode)
        => await connection.ExecuteScalarAsync<bool>(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [parameter].[DocumentTypes]
                WHERE [Code] = @Code AND [Category] IN ('VAL_DOC', 'VAL_REPORT') AND [IsActive] = 1
            ) THEN 1 ELSE 0 END
            """,
            new { Code = typeCode });

    public static async Task<int> NextSortOrderAsync(IDbConnection connection, Guid appraisalId, string typeCode)
    {
        var maxSortOrder = await connection.ExecuteScalarAsync<int?>(
            """
            SELECT MAX([SortOrder]) FROM [appraisal].[AppraisalDocuments]
            WHERE [AppraisalId] = @AppraisalId AND [DocumentTypeCode] = @Code
            """,
            new { AppraisalId = appraisalId, Code = typeCode });
        return (maxSortOrder ?? -1) + 1;
    }

    /// <summary>
    /// The uploaded file as the Document module stored it, or null when it does not exist or was deleted.
    /// Audit paths read name/mime/size from here instead of trusting what the client sent.
    /// </summary>
    public static Task<UploadedFile?> GetUploadedFileAsync(IDbConnection connection, Guid documentId)
        => connection.QuerySingleOrDefaultAsync<UploadedFile?>(
            """
            SELECT [FileName], [MimeType], [FileSizeBytes]
            FROM [document].[Documents]
            WHERE [Id] = @DocumentId AND [IsActive] = 1 AND [IsDeleted] = 0
            """,
            new { DocumentId = documentId });
}

public sealed record UploadedFile(string FileName, string MimeType, long FileSizeBytes);
