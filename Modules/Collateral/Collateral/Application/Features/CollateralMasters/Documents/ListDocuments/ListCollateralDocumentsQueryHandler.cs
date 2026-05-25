using Dapper;

namespace Collateral.Application.Features.CollateralMasters.Documents.ListDocuments;

public class ListCollateralDocumentsQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<ListCollateralDocumentsQuery, ListCollateralDocumentsResult>
{
    public async Task<ListCollateralDocumentsResult> Handle(
        ListCollateralDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        // Default: active documents only when filter is not specified.
        var isActiveFilter = query.IsActive ?? true;

        var sql = """
            SELECT
                Id,
                DocumentType,
                DocumentId,
                FileName,
                Description,
                IsActive,
                CreatedAt,
                CreatedBy
            FROM collateral.CollateralDocuments
            WHERE CollateralMasterId = @CollateralMasterId
              AND IsActive = @IsActive
            """;

        var p = new DynamicParameters();
        p.Add("CollateralMasterId", query.CollateralMasterId);
        p.Add("IsActive", isActiveFilter);

        if (!string.IsNullOrWhiteSpace(query.DocumentType))
        {
            sql += " AND DocumentType = @DocumentType";
            p.Add("DocumentType", query.DocumentType);
        }

        sql += " ORDER BY CreatedAt DESC";

        var connection = connectionFactory.GetOpenConnection();
        var items = await connection.QueryAsync<CollateralDocumentDto>(sql, p);

        return new ListCollateralDocumentsResult(items.AsList());
    }
}
