using Reporting.Application.Services;

namespace Reporting.Infrastructure.PdfAssembly;

/// <summary>
/// Resolves a <see cref="Guid"/> DocumentId to its physical <c>StoragePath</c>
/// by querying <c>document.Documents</c> directly via Dapper.
///
/// This avoids a hard ProjectReference to the Document module while still being
/// able to retrieve uploaded PDFs for slot insertion.
///
/// NOTE: Only returns paths for active, non-deleted documents.
/// </summary>
internal sealed class DapperAttachmentSource(ISqlConnectionFactory connectionFactory)
    : IReportAttachmentSource
{
    public async Task<string?> GetFilePathAsync(Guid documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT StoragePath
            FROM document.Documents
            WHERE Id = @DocumentId
              AND IsDeleted = 0
              AND IsActive  = 1
            """;

        var parameters = new DynamicParameters();
        parameters.Add("DocumentId", documentId);

        using var connection = connectionFactory.CreateNewConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(sql, parameters);
    }
}
