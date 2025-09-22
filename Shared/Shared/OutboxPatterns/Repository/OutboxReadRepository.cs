using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Shared.Data;
using Shared.OutboxPatterns.Models;

namespace Shared.OutboxPatterns.Repository;

public class OutboxReadRepository<TDbContext> : BaseReadRepository<OutboxMessage, Guid>, IOutboxReadRepository where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private readonly IConfiguration _configuration;
    private readonly int _batchSize;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public OutboxReadRepository(TDbContext dbContext, IConfiguration configuration, ISqlConnectionFactory sqlConnectionFactory) : base(dbContext)
    {
        _configuration = configuration;
        _batchSize = _configuration.GetValue<int>("OutboxConfigurations:BatchSize");
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<List<OutboxMessage>> GetAllAsync(string schema, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var parameters = new { batchSize = _batchSize };

        string sql = $@"
            SELECT [Id], [OccurredOn], [Payload], [EventType], [ExceptionInfo], 
                   [RetryCount], [LastRetryAt], [MaxRetries], [IsInfrastructureFailure] 
            FROM [{schema}].[OutboxMessages] WITH (READCOMMITTED, ROWLOCK, READPAST, UPDLOCK)
            WHERE (
                ([IsInfrastructureFailure] = 0 AND [RetryCount] < [MaxRetries]) OR
                ([IsInfrastructureFailure] = 1 AND [OccurredOn] > DATEADD(hour, -24, GETUTCDATE()))
            )
            AND (
                [LastRetryAt] IS NULL OR
                ([IsInfrastructureFailure] = 1 AND [LastRetryAt] < DATEADD(minute, -CASE WHEN [RetryCount] > 6 THEN 64 ELSE POWER(2, [RetryCount]) END, GETUTCDATE())) OR
                ([IsInfrastructureFailure] = 0 AND [LastRetryAt] < DATEADD(minute, -POWER(2, [RetryCount]), GETUTCDATE()))
            )
            ORDER BY [OccurredOn]
            OFFSET 0 ROWS FETCH NEXT @batchSize ROWS ONLY";

        var messages = (await connection.QueryAsync<OutboxMessage>(
            sql, parameters
        )).ToList();

        return messages ?? [];
    }
}