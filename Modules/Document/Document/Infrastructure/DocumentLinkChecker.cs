using Dapper;
using Document.Domain.Documents;
using Shared.Data;

namespace Document.Infrastructure;

internal sealed class DocumentLinkChecker(ISqlConnectionFactory connectionFactory) : IDocumentLinkChecker
{
    private const string Sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM document.vw_DocumentLinks WHERE DocumentId = @DocumentId) THEN 1 ELSE 0 END";

    public Task<bool> HasLiveLinksAsync(Guid documentId, CancellationToken cancellationToken = default) =>
        connectionFactory.GetOpenConnection().ExecuteScalarAsync<bool>(
            new CommandDefinition(Sql, new { DocumentId = documentId }, cancellationToken: cancellationToken));
}
