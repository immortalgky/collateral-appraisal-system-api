using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Workflow.Data;
using Workflow.Data.Entities;
using Workflow.Services.Configuration.Models;

namespace Workflow.Services.Configuration;

public class CompanyRoundRobinConfigService(
    WorkflowDbContext context,
    ILogger<CompanyRoundRobinConfigService> logger) : ICompanyRoundRobinConfigService
{
    public async Task<CompanyRoundRobinConfigurationDto?> ResolveAsync(
        string? loanType,
        CancellationToken cancellationToken = default)
    {
        var scope = string.IsNullOrWhiteSpace(loanType) ? null : loanType;

        // At most one active pool per scope (enforced by the filtered-unique index). The active-config
        // table is tiny, so this stays a cheap per-call query — no cache, matching the sibling
        // TaskConfigurationService and avoiding cross-server stale reads (there is no distributed cache).
        // Fetch all active pools and match in memory so loan-type matching is case-insensitive and
        // collation-independent (e.g. incoming "RETAIL" still resolves the seeded "Retail" pool).
        var candidates = await context.CompanyRoundRobinConfigurations
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        // Loan-type-specific pool wins over the global (null) pool.
        var best = candidates.FirstOrDefault(c => string.Equals(c.LoanType, scope, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(c => c.LoanType == null);

        return best is null ? null : MapToDto(best);
    }

    public async Task<CompanyRoundRobinConfigurationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.CompanyRoundRobinConfigurations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<List<CompanyRoundRobinConfigurationDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context.CompanyRoundRobinConfigurations
            .OrderBy(c => c.LoanType)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<CompanyRoundRobinConfigurationDto> CreateAsync(
        CreateCompanyRoundRobinConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = CompanyRoundRobinConfiguration.Create(
            JsonSerializer.Serialize(CompanyPoolWeights.Normalize(request.Entries)),
            request.CreatedBy,
            string.IsNullOrWhiteSpace(request.LoanType) ? null : request.LoanType,
            request.IsActive);

        context.CompanyRoundRobinConfigurations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created company round-robin configuration {ConfigId} (loanType={LoanType})",
            entity.Id, entity.LoanType);

        return MapToDto(entity);
    }

    public async Task<CompanyRoundRobinConfigurationDto> UpdateAsync(
        Guid id,
        UpdateCompanyRoundRobinConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.CompanyRoundRobinConfigurations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Company round-robin configuration with ID {id} not found");

        entity.Update(
            JsonSerializer.Serialize(CompanyPoolWeights.Normalize(request.Entries)),
            request.UpdatedBy,
            string.IsNullOrWhiteSpace(request.LoanType) ? null : request.LoanType,
            request.IsActive);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated company round-robin configuration {ConfigId}", id);

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.CompanyRoundRobinConfigurations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Company round-robin configuration with ID {id} not found");

        context.CompanyRoundRobinConfigurations.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted company round-robin configuration {ConfigId}", id);
    }

    private CompanyRoundRobinConfigurationDto MapToDto(CompanyRoundRobinConfiguration entity)
    {
        return new CompanyRoundRobinConfigurationDto
        {
            Id = entity.Id,
            LoanType = entity.LoanType,
            Entries = DeserializeEntries(entity.Entries),
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy
        };
    }

    private List<CompanyWeightDto> DeserializeEntries(string entriesJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CompanyWeightDto>>(entriesJson) ?? new();
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to deserialize company round-robin entries JSON: {Json}", entriesJson);
            return new();
        }
    }
}
