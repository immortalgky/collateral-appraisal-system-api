using System.Text.Json;
using Auth.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Shared.Data.Seed;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Services.Configuration;
using Workflow.Services.Configuration.Models;

namespace Workflow.AssigneeSelection.Seed;

/// <summary>
/// Seeds the default external-company round-robin pools from the bank's parameter listing: the
/// companies with an MOU with the bank (embedded JSON), split into a <b>Retail</b> pool and an
/// <b>IBG</b> pool by each company's Appraisal Type (its <see cref="Company.LoanTypes"/> — "All" /
/// "IBG , Retail" companies go in both). Resolves each company by Name against the Auth company table,
/// so the Auth company seed must run first; any name not found is skipped.
///
/// Idempotent: skipped if any pool configuration already exists.
/// </summary>
public class CompanyRoundRobinConfigSeeder(
    WorkflowDbContext context,
    ICompanyRepository companyRepository,
    ILogger<CompanyRoundRobinConfigSeeder> logger) : IDataSeeder<WorkflowDbContext>
{
    private const string ResourceFileName = "company-roundrobin-mou-pool.json";

    public async Task SeedAllAsync()
    {
        if (await context.CompanyRoundRobinConfigurations.AnyAsync())
        {
            logger.LogInformation("CompanyRoundRobinConfigurations already seeded, skipping");
            return;
        }

        var pool = LoadPool();
        if (pool is null || pool.Count == 0)
        {
            logger.LogWarning("Company round-robin MOU pool resource not found or empty, skipping seed");
            return;
        }

        // Resolve companies by Name in a single query (carries LoanTypes, used to split by segment).
        var companiesByName = (await companyRepository.GetAllAsync())
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First());

        var retail = new List<CompanyWeightDto>();
        var ibg = new List<CompanyWeightDto>();
        foreach (var item in pool)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            if (!companiesByName.TryGetValue(item.Name, out var company))
            {
                logger.LogWarning("MOU pool company '{Name}' not found in company table, skipping", item.Name);
                continue;
            }

            var entry = new CompanyWeightDto { CompanyId = company.Id, Weight = item.Weight };
            if (company.LoanTypes.Contains("Retail", StringComparer.OrdinalIgnoreCase)) retail.Add(entry);
            if (company.LoanTypes.Contains("IBG", StringComparer.OrdinalIgnoreCase)) ibg.Add(entry);
        }

        // One active pool per loan-type scope. CompanyPoolWeights.Normalize matches the admin write path:
        // dedup, clamp >= 1, and GCD-reduce the raw "% Round Robin" values (all 100 → all 1).
        var seeded = 0;
        seeded += AddPool("Retail", retail);
        seeded += AddPool("IBG", ibg);

        if (seeded == 0)
        {
            logger.LogWarning("No MOU pool companies resolved, skipping company round-robin config seed");
            return;
        }

        await context.SaveChangesAsync();
    }

    private int AddPool(string loanType, List<CompanyWeightDto> entries)
    {
        var normalized = CompanyPoolWeights.Normalize(entries);
        if (normalized.Count == 0) return 0;

        context.CompanyRoundRobinConfigurations.Add(
            CompanyRoundRobinConfiguration.Create(
                JsonSerializer.Serialize(normalized),
                createdBy: "system",
                loanType: loanType,
                isActive: true));

        logger.LogInformation("Seeded active {LoanType} round-robin pool with {Count} companies",
            loanType, normalized.Count);
        return 1;
    }

    private static List<MouPoolItem>? LoadPool()
    {
        var assembly = typeof(CompanyRoundRobinConfigSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ResourceFileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;

        return JsonSerializer.Deserialize<List<MouPoolItem>>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private sealed record MouPoolItem
    {
        public string Name { get; init; } = default!;
        public int Weight { get; init; } = 1;
    }
}
