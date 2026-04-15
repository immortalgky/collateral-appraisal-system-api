using Microsoft.EntityFrameworkCore;
using Shared.Data.RunningNumbers;
using Workflow.Data;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Infrastructure;

/// <summary>
/// Generates meeting numbers in "{seq}/{BE-year}" format (BE = Gregorian + 543).
/// Delegates to the shared <c>dbo.RunningNumbers</c> table using the same
/// UPDATE…IF @@ROWCOUNT=0 INSERT pattern as Appraisal and Request modules.
/// Enlists in the ambient EF transaction so the counter increment is atomic with
/// the SendInvitation state change.
/// </summary>
public sealed class MeetingNoGenerator(WorkflowDbContext dbContext) : IMeetingNoGenerator
{
    private const string Sql = """
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

    public async Task<string> NextAsync(DateTime now, CancellationToken ct)
    {
        var beYear = now.Year + 543;

        var result = await dbContext.Database
            .SqlQueryRaw<int>(Sql, nameof(RunningNumberType.MEETING), beYear, "MTG")
            .ToListAsync(ct);

        var nextNumber = result.First();

        return $"{nextNumber}/{beYear}";
    }
}
