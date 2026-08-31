using Appraisal.Application.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Data.RunningNumbers;

namespace Appraisal.Infrastructure;

/// <summary>
/// Issues unit ticket numbers in the format {YY}U{00000} — e.g. "69U00042".
///
/// Shape and why:
///   * 8 characters, so the number fits every field that already carries an appraisal number,
///     including the 10-character AS400 CCSURV field (8 plus two trailing spaces, exactly how an
///     ordinary appraisal number is written). No interface has to be widened.
///   * digits only either side of the marker, so nothing depends on letter case — the database
///     collation is case-insensitive and the host's handling of case is unconfirmed.
///   * the literal 'U' at position 3 marks it as a ticket rather than an appraisal. Appraisal
///     numbers are {YY}{D6}, all digits, so the marker cannot collide. Length alone would not do:
///     legacy appraisal numbers such as "2560100004" are also ten characters.
///   * the counter resets yearly, like every other RunningNumbers series.
/// </summary>
public class UnitTicketNumberGenerator(AppraisalDbContext context) : IUnitTicketNumberGenerator
{
    private const string TypeName = nameof(RunningNumberType.UNIT_TICKET);
    private const string Prefix = "U";

    /// <summary>Widest running number the 5-digit slot holds.</summary>
    public const int MaxRunningNumberPerYear = 99_999;

    private const int CeilingExceeded = -1;

    public async Task<string> GenerateAsync(int thaiYear, CancellationToken cancellationToken = default)
    {
        var next = await ReserveAsync(thaiYear, cancellationToken);

        // The reservation refuses rather than overshoots, so a year that fills up does not leave the
        // counter parked past its ceiling — which would fail every later request too, however small.
        if (next == CeilingExceeded)
            throw new InvalidOperationException(
                $"Unit ticket numbering for Buddhist year {thaiYear} has reached " +
                $"{MaxRunningNumberPerYear}, the limit the {{YY}}U{{00000}} format holds. " +
                "Widen the format before issuing more. The counter has not been advanced.");

        return $"{thaiYear % 100:D2}{Prefix}{next:D5}";
    }

    /// <summary>
    /// Advances the counter by one and returns it, or <see cref="CeilingExceeded"/> when the year is
    /// full. Same UPDLOCK/ROWLOCK/HOLDLOCK row locking as every other RunningNumbers consumer.
    ///
    /// @Found separates "no counter row for this year yet" from "the row exists but is full";
    /// @@ROWCOUNT alone cannot tell them apart, and a WHERE that filtered on the ceiling would send
    /// a full year down the INSERT branch and start it over at 1.
    /// </summary>
    private async Task<int> ReserveAsync(int year, CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @Next INT = NULL;
            DECLARE @Found BIT = 0;

            UPDATE dbo.RunningNumbers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET @Found = 1,
                @Next = CASE WHEN CurrentNumber + 1 <= {3} THEN CurrentNumber + 1 END,
                CurrentNumber = CASE WHEN CurrentNumber + 1 <= {3}
                                     THEN CurrentNumber + 1
                                     ELSE CurrentNumber END,
                UpdatedOn = GETUTCDATE()
            WHERE Type = {0} AND Year = {1};

            IF @Found = 0
            BEGIN
                INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
                VALUES ({0}, {2}, 1, {1}, GETUTCDATE());
                SET @Next = 1;
            END

            SELECT ISNULL(@Next, -1);
            """;

        var result = await context.Database
            .SqlQueryRaw<int>(sql, TypeName, year, Prefix, MaxRunningNumberPerYear)
            .ToListAsync(cancellationToken);

        return result.First();
    }
}
