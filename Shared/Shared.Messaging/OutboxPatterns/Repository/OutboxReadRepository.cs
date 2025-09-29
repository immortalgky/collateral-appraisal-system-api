namespace Shared.Messaging.OutboxPatterns.Repository;

public class OutboxReadRepository<TDbContext> : BaseReadRepository<OutboxMessage, Guid>, IOutboxReadRepository 
    where TDbContext : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly int _batchSize;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public OutboxReadRepository(
        TDbContext dbContext, 
        IConfiguration configuration, 
        ISqlConnectionFactory sqlConnectionFactory) : base(dbContext)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _sqlConnectionFactory = sqlConnectionFactory ?? throw new ArgumentNullException(nameof(sqlConnectionFactory));
        _batchSize = _configuration.GetValue<int>("Jobs:OutboxProcessor:BatchSize");
    }

    public async Task<List<OutboxMessage>> GetMessageAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));

        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new { batchSize = _batchSize, schema = schema };

        string sql = @$"
            SELECT TOP (@batchSize) *
            FROM [" + schema + @"].[OutboxMessages] 
            WITH (ROWLOCK, READPAST, UPDLOCK)
            ORDER BY [Id]";

        var messages = (await connection.QueryAsync<OutboxMessage>(sql, parameters)).ToList();

        return messages ?? new List<OutboxMessage>();
    }
}