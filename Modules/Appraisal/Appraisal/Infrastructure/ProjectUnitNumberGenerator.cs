using Appraisal.Application.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Data.RunningNumbers;

namespace Appraisal.Infrastructure;

/// <summary>
/// Issues block-project unit numbers in the format {YY}U{00000} — e.g. "69U00042".
///
/// Shape and why:
///   * 8 characters, so the number drops into every field that already accepts an appraisal number,
///     including the 10-character AS400 CCSURV field (8 chars plus two trailing spaces, exactly how
///     an ordinary appraisal number is written today). No interface widening is needed.
///   * digits only, so nothing depends on letter case — the database collation is case-insensitive
///     and the host's handling of case is unconfirmed.
///   * the literal 'U' at position 3 marks it as a unit rather than an appraisal. Appraisal numbers
///     are {YY}{D6}, all digits, so the marker can never collide. Length alone would not be enough:
///     legacy appraisal numbers such as "2560100004" are also ten characters.
///   * the counter resets yearly, like every other RunningNumbers series.
///
/// Numbers are reserved in one round trip rather than one per unit: a block project with 800 units
/// would otherwise open 800 round trips inside a single transaction.
/// </summary>
public class ProjectUnitNumberGenerator(AppraisalDbContext context) : IProjectUnitNumberGenerator
{
    private const string TypeName = nameof(RunningNumberType.PROJECT_UNIT);
    private const string Prefix = "U";

    /// <summary>Widest running number the 5-digit slot holds.</summary>
    public const int MaxRunningNumberPerYear = 99_999;

    /// <summary>Sentinel returned by the reservation when the block would pass the ceiling.</summary>
    private const int CeilingExceeded = -1;

    public async Task<IReadOnlyList<string>> GenerateAsync(
        int thaiYear,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive.");

        if (count > MaxRunningNumberPerYear)
            throw new InvalidOperationException(
                $"Cannot issue {count} unit numbers at once: the {{YY}}U{{00000}} format holds only " +
                $"{MaxRunningNumberPerYear} per year.");

        var start = await ReserveBlockAsync(thaiYear, count, cancellationToken);

        // The reservation refuses rather than overshoots, so the counter is never left past the
        // ceiling. Advancing it first and checking afterwards would poison the rest of the year:
        // the failed block stays spent and every later request — however small — fails the same way.
        if (start == CeilingExceeded)
            throw new InvalidOperationException(
                $"Unit numbering for Buddhist year {thaiYear} cannot fit {count} more number(s) " +
                $"below {MaxRunningNumberPerYear}, the limit the {{YY}}U{{00000}} format holds. " +
                "Widen the format before issuing more. The counter has not been advanced.");

        var yearShort = (thaiYear % 100).ToString("D2");

        return Enumerable.Range(start, count)
            .Select(n => $"{yearShort}{Prefix}{n:D5}")
            .ToList();
    }

    /// <summary>
    /// Advances the counter by <paramref name="count"/> and returns the first number of the block.
    /// Same UPDLOCK/ROWLOCK/HOLDLOCK row locking as every other RunningNumbers consumer.
    ///
    /// A block that is reserved and then not used — the surrounding save fails — leaves a gap in the
    /// sequence. That is the same trade every RunningNumbers consumer here already makes: the
    /// counter is advanced before the rows it numbers are written, because holding the row lock for
    /// the whole save would serialise every approval in the system.
    /// </summary>
    private async Task<int> ReserveBlockAsync(int year, int count, CancellationToken cancellationToken)
    {
        // @Found separates "no counter row for this year yet" from "the row exists but the block
        // would not fit". Keying that off @@ROWCOUNT alone cannot tell them apart, and a WHERE that
        // filters on the ceiling would send an over-limit request down the INSERT branch.
        const string sql = """
            DECLARE @Start INT = NULL;
            DECLARE @Found BIT = 0;

            UPDATE dbo.RunningNumbers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET @Found = 1,
                @Start = CASE WHEN CurrentNumber + {3} <= {4} THEN CurrentNumber + 1 END,
                CurrentNumber = CASE WHEN CurrentNumber + {3} <= {4}
                                     THEN CurrentNumber + {3}
                                     ELSE CurrentNumber END,
                UpdatedOn = GETUTCDATE()
            WHERE Type = {0} AND Year = {1};

            IF @Found = 0
            BEGIN
                INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
                VALUES ({0}, {2}, {3}, {1}, GETUTCDATE());
                SET @Start = 1;
            END

            SELECT ISNULL(@Start, -1);
            """;

        var result = await context.Database
            .SqlQueryRaw<int>(sql, TypeName, year, Prefix, count, MaxRunningNumberPerYear)
            .ToListAsync(cancellationToken);

        return result.First();
    }
}
