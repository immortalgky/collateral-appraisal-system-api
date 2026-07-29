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
                category: "Valuation"),
            // Phase-1 go-live: the bank engages appraisal companies OUTSIDE the system, so NO case
            // may be assigned to a company inside it — not by round-robin, not by an admin's manual
            // pick, not via a quotation winner, not by Construction-Inspection carry-over.
            // CompanySelectionActivity escalates all of them to admin review, where the case is
            // routed internally or recorded as an off-system engagement (EXTO). A case that already
            // holds a company keeps it. Set to "true" to resume normal assignment — no redeploy and
            // no new workflow definition version required.
            SystemConfiguration.Create(
                key: "ExternalCompanyAssignmentEnabled",
                value: "false",
                valueType: "bool",
                description: "Whether an appraisal may be assigned to an external company inside the system (by round-robin, manual selection, quotation winner or CI carry-over). When false, such cases stop at admin review to be routed internally or recorded as an off-system engagement.",
                category: "Assignment")
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
