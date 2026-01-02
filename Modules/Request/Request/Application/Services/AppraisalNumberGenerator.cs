namespace Request.Application.Services;

public class AppraisalNumberGenerator(RequestDbContext context) : IAppraisalNumberGenerator
{
    private const string TypeName = "APPRAISAL";
    private const string Prefix = "A";

    public async Task<AppraisalNumber> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var thaiYear = DateTime.Now.Year + 543;
        var yearShort = (thaiYear % 100).ToString("D2"); // "68" from 2568

        var nextNumber = await GetNextRunningNumberAsync(thaiYear, cancellationToken);

        // Format: {YY}A{000001} e.g., "68A000001"
        return AppraisalNumber.Create($"{yearShort}{Prefix}{nextNumber:D6}");
    }

    private async Task<int> GetNextRunningNumberAsync(int year, CancellationToken cancellationToken)
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

        var result = await context.Database
            .SqlQueryRaw<int>(sql, TypeName, year, Prefix)
            .ToListAsync(cancellationToken);

        return result.First();
    }
}
