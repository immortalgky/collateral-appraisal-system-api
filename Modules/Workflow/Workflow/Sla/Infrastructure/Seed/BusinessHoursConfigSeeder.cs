using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Sla.Models;

namespace Workflow.Sla.Infrastructure.Seed;

public class BusinessHoursConfigSeeder(
    WorkflowDbContext context,
    ILogger<BusinessHoursConfigSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    public async Task SeedAllAsync()
    {
        if (await context.BusinessHoursConfigs.AnyAsync(b => b.IsActive))
        {
            logger.LogInformation("Active BusinessHoursConfig already exists, skipping seed");
            return;
        }

        var config = BusinessHoursConfig.Create(
            startTime:      new TimeOnly(8, 30),
            endTime:        new TimeOnly(17, 30),
            timeZone:       "Asia/Bangkok",
            lunchStartTime: new TimeOnly(12, 0),
            lunchEndTime:   new TimeOnly(13, 0));

        context.BusinessHoursConfigs.Add(config);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded default BusinessHoursConfig: 08:30-17:30 Asia/Bangkok with lunch 12:00-13:00 (8h/day)");
    }
}
