using Common.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Seed;

namespace Common.Infrastructure.Seed;

/// <summary>
/// Seeds initial SystemConfiguration rows.
/// Guarded per-key — only inserts if the key does not yet exist.
/// </summary>
public class SystemConfigurationDataSeed(
    CommonDbContext ctx,
    ILogger<SystemConfigurationDataSeed> logger)
    : IDataSeeder<CommonDbContext>
{
    public async Task SeedAllAsync()
    {
        var rows = new[]
        {
            SystemConfiguration.Create(
                key: "BlockReappraisalIntervalYears",
                value: "5",
                valueType: "int",
                description: "Years after last appraisal before a block project is due for reappraisal.",
                category: "Reappraisal"),
            SystemConfiguration.Create(
                key: "ForceSaleRateDefaultPct",
                value: "70",
                valueType: "decimal",
                description: "Default force-sale percentage applied to total appraisal price when an appraisal has no override.",
                category: "Valuation")
        };

        foreach (var row in rows)
        {
            if (await ctx.SystemConfigurations.AnyAsync(c => c.Key == row.Key))
            {
                logger.LogInformation("SystemConfiguration '{Key}' already exists, skipping.", row.Key);
                continue;
            }

            ctx.SystemConfigurations.Add(row);
            logger.LogInformation("Seeding SystemConfiguration '{Key}'.", row.Key);
        }

        await ctx.SaveChangesAsync();
    }
}
