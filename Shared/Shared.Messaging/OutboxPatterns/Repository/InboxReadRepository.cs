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

    public async Task<InboxMessage> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new { Schema = _schema };

        string sql = @$"
            SELECT TOP (1) *
            FROM [@Schema].[InboxMessages]
            WITH (ROWLOCK, UPDLOCK)
            WHERE EventId = @id";

        var messages = await connection.QueryAsync<InboxMessage>(sql, parameters);

        return messages.FirstOrDefault()!;
    }
}