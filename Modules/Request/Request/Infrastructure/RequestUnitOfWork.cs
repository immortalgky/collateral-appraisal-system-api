using Shared.Data.RunningNumbers;

namespace Request.Infrastructure;

public class RequestUnitOfWork(RequestDbContext context, IServiceProvider serviceProvider)
    : UnitOfWork<RequestDbContext>(context, serviceProvider), IRequestUnitOfWork
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-generate RequestNumber for new Requests without one
        await GenerateRequestNumbersAsync(cancellationToken);

        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateRequestNumbersAsync(CancellationToken cancellationToken)
    {
        var newRequests = Context.ChangeTracker
            .Entries<Domain.Requests.Request>()
            .Where(e => e.State == EntityState.Added && e.Entity.RequestNumber == null)
            .Select(e => e.Entity)
            .ToList();

        foreach (var request in newRequests)
        {
            var requestNumber = await GenerateRequestNumberAsync(cancellationToken);
            request.SetRequestNumber(requestNumber);
        }
    }

    private async Task<RequestNumber> GenerateRequestNumberAsync(CancellationToken cancellationToken)
    {
        var thaiYear = DateTime.Now.Year + 543;
        var nextNumber = await GetNextRunningNumberAsync(
            RunningNumberType.REQUEST,
            thaiYear,
            cancellationToken);

        // Format: REQ-{000001}-{YYYY} e.g., "REQ-000001-2568"
        return RequestNumber.Create($"REQ-{nextNumber:D6}-{thaiYear}");
    }

    private async Task<int> GetNextRunningNumberAsync(
        RunningNumberType type,
        int year,
        CancellationToken cancellationToken)
    {
        // SQL with UPDLOCK, ROWLOCK, HOLDLOCK for row-level locking
        // - UPDLOCK: Blocks other UPDATE/DELETE on same row
        // - ROWLOCK: Ensures row-level lock (no page/table escalation)
        // - HOLDLOCK: Holds lock until transaction commits/rollbacks
        // Parameters: {0} = Type (string), {1} = Year, {2} = Prefix
        const string sql = """
            DECLARE @NextNumber INT;

            UPDATE dbo.RunningNumbers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET @NextNumber = CurrentNumber = CurrentNumber + 1,
                UpdatedOn = GETUTCDATE()
            WHERE Type = {0} AND Year = {1};

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
                VALUES ({0}, {2}, 1, {1}, GETUTCDATE());
                SET @NextNumber = 1;
            END

            SELECT @NextNumber;
            """;

        var (typeName, prefix) = type switch
        {
            RunningNumberType.REQUEST => ("REQUEST", "REQ"),
            RunningNumberType.APPRAISAL => ("APPRAISAL", "A"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var result = await Context.Database
            .SqlQueryRaw<int>(sql, typeName, year, prefix)
            .ToListAsync(cancellationToken);

        return result.First();
    }
}
