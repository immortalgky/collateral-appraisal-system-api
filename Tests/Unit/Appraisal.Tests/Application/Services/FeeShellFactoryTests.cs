using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Microsoft.EntityFrameworkCore;
using static Appraisal.Tests.Application.Services.FeeTestSeed;

namespace Appraisal.Tests.Application.Services;

public class FeeShellFactoryTests
{
    [Fact]
    public async Task LatestFee_breaks_a_created_at_tie_on_id_so_it_always_picks_the_same_row()
    {
        await using var db = NewDb();
        var appraisal = await AddAppraisalAsync(db, AppraisalTypes.New);
        var stamp = new DateTime(2026, 3, 1);
        var fees = new List<AppraisalFee>();
        for (var i = 0; i < 4; i++)
        {
            var assignment = await AddAssignmentAsync(db, appraisal.Id);
            var fee = await AddFeeAsync(db, assignment.Id, 1_000m + i);
            db.Entry(fee).Property("CreatedAt").CurrentValue = stamp;
            fees.Add(fee);
        }
        await db.SaveChangesAsync();

        var latest = await FeeShellFactory.LatestFee(db, appraisal.Id, null).FirstAsync();

        Assert.Equal(fees.Max(f => f.Id), latest.Id);
    }
}
