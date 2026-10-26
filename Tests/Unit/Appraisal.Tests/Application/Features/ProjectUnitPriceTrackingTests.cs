using Appraisal.Application.Features.Project;
using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Tests.Application.Features;

public class ProjectUnitPriceTrackingTests
{
    [Fact]
    public async Task DetachAddedUnitPrices_DropsOnlyTheRowsAnEarlierAttemptAdded()
    {
        var options = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppraisalDbContext(options);

        var saved = ProjectUnitPrice.Create(Guid.NewGuid());
        db.ProjectUnitPrices.Add(saved);
        await db.SaveChangesAsync();
        saved.ApplyImportedValues(1m, 2m, 3m, 4m);                         // Modified: must stay tracked
        var leftOver = ProjectUnitPrice.Create(Guid.NewGuid());
        db.ProjectUnitPrices.Add(leftOver);                                // Added by the failed attempt

        db.DetachAddedUnitPrices();

        Assert.Equal(EntityState.Detached, db.Entry(leftOver).State);
        Assert.Equal(EntityState.Modified, db.Entry(saved).State);
    }
}
