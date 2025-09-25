using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Data.Models;

namespace Shared.Messaging.OutboxPatterns.Repository;

public class InboxReadRepository<TDbContext> : BaseReadRepository<InboxMessage, Guid>, IInboxReadRepository
    where TDbContext : DbContext
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly string _schema;

    public InboxReadRepository(
        TDbContext dbContext,
        ISqlConnectionFactory sqlConnectionFactory,
        string schema
        ) : base(dbContext)
    {
        _sqlConnectionFactory = sqlConnectionFactory ?? throw new ArgumentNullException(nameof(sqlConnectionFactory));
        _schema = schema;
    }

    public async Task<InboxMessage> GetMessageByIdAsync(Guid id,CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_schema))
            throw new ArgumentException("Schema cannot be null or empty.", nameof(_schema));

        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        string sql = @$"
            SELECT TOP (1) *
            FROM [{_schema}].[InboxMessages]
            WITH (ROWLOCK, UPDLOCK)
            WHERE Id = '{id}'";

        var message = await connection.QueryAsync<InboxMessage>(sql, cancellationToken);

        return (InboxMessage)message;
    }
}