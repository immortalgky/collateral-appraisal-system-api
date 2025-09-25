using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Shared.Data;
using Shared.Data.Models;

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
        _batchSize = _configuration.GetValue<int>("OutboxConfigurations:BatchSize");
    }

    public async Task<List<OutboxMessage>> GetMessageAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("Schema cannot be null or empty.", nameof(schema));

        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new { batchSize = _batchSize };

        string sql = $@"
            SELECT *
            FROM [{schema}].[OutboxMessages] 
            WITH (ROWLOCK, READPAST, UPDLOCK)
            WHERE (
                ([IsInfrastructureFailure] = 0 AND [RetryCount] < [MaxRetries]) OR
                ([IsInfrastructureFailure] = 1 AND [OccurredOn] > DATEADD(hour, -24, GETUTCDATE()))
            )
            AND (
                [LastRetryAt] IS NULL OR
                ([IsInfrastructureFailure] = 1 AND [LastRetryAt] < DATEADD(minute, -CASE WHEN [RetryCount] > 6 THEN 64 ELSE POWER(2, [RetryCount]) END, GETUTCDATE())) OR
                ([IsInfrastructureFailure] = 0 AND [LastRetryAt] < DATEADD(minute, -POWER(2, [RetryCount]), GETUTCDATE()))
            )
            OFFSET 0 ROWS FETCH NEXT @batchSizeROWS ONLY
            ORDER BY [Id]";

        var messages = (await connection.QueryAsync<OutboxMessage>(sql, parameters)).ToList();

        return messages ?? new List<OutboxMessage>();
    }
}