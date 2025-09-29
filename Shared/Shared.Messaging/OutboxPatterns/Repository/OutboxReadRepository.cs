namespace Shared.Messaging.OutboxPatterns.Repository;

public class OutboxReadRepository<TDbContext> : BaseReadRepository<OutboxMessage, Guid>, IOutboxReadRepository 
    where TDbContext : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly int _batchSize;
    private readonly string _schema;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;


    public OutboxReadRepository(
        TDbContext dbContext,
        IConfiguration configuration,
        ISqlConnectionFactory sqlConnectionFactory,
        string schema) : base(dbContext)
    {
        _configuration = configuration;
        _sqlConnectionFactory = sqlConnectionFactory;
        _batchSize = _configuration.GetValue<int>("Jobs:OutboxProcessor:BatchSize");
        _schema = schema;
    }

    public async Task<List<OutboxMessage>> GetMessageAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new { BatchSize = _batchSize, Schema = _schema };

        string sql = @$"
            SELECT TOP (@BatchSize) *
            FROM [@Schema].[OutboxMessages] 
            WITH (ROWLOCK, READPAST, UPDLOCK)
            ORDER BY [Id]";

        var messages = (await connection.QueryAsync<OutboxMessage>(sql, parameters)).ToList();

        return messages.ToList();
    }
}