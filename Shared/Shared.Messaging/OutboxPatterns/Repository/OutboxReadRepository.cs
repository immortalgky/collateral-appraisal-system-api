namespace Shared.Messaging.OutboxPatterns.Repository;

public class OutboxReadRepository<TDbContext> : BaseReadRepository<OutboxMessage, Guid>, IOutboxReadRepository 
    where TDbContext : DbContext
{
    private readonly IConfiguration _configuration;
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
        _schema = schema;
    }

    public async Task<List<OutboxMessage>> GetMessageAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new
        {
            BatchSize = _configuration.GetValue<int>("Jobs:OutboxProcessor:BatchSize"),
        };

        string sql = @$"
            SELECT TOP (@BatchSize) 
            EventId AS Id,
            OccurredOn,
            Payload,
            EventType,
            ExceptionInfo,
            RetryCount,
            ProcessingFailed
            FROM [{_schema}].[OutboxMessages]
            ORDER BY OccurredOn";

        var messages = await connection.QueryAsync<OutboxMessage>(sql, parameters);

        return messages.ToList();
    }
}