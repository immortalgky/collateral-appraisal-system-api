namespace Shared.Messaging.OutboxPatterns.Repository;

public class InboxRepository<TDbContext> : BaseRepository<InboxMessage, Guid>, IInboxRepository 
    where TDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IConfiguration _configuration;
    private readonly string _schema;
    public InboxRepository(
        TDbContext dbContext,
        ISqlConnectionFactory sqlConnectionFactory,
        IConfiguration configuration,
        string schema) : base(dbContext)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _configuration = configuration;
        _schema = schema;
    }

    public async Task<IDbContextTransaction> BeginTransaction(CancellationToken cancellationToken = default)
    {
        return await Context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task<bool> DeleteMessageTimeout(CancellationToken cancellationToken)
    {
        await using var connection = (SqlConnection)_sqlConnectionFactory.GetOpenConnection();

        var sql = @$"
        DELETE FROM [{_schema}].[InboxMessages] 
        WHERE ReceiveAt < @cutoffDate";

        var cutoffDate = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("OutboxConfigurations:CutOffDay"));
        var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@cutoffDate", cutoffDate);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    public async Task SaveChangeAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken);
    }
}