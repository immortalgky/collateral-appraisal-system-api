using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Database.Migration;

/// <summary>
/// Simplified migration service that only handles DbUp migrations for views, stored procedures, and functions
/// </summary>
public class SimplifiedMigrationService : IMigrationService
{
    private readonly IMigrationService _dbUpMigrationService;
    private readonly ILogger<SimplifiedMigrationService> _logger;

    public SimplifiedMigrationService(
        IMigrationService dbUpMigrationService,
        IConfiguration configuration,
        ILogger<SimplifiedMigrationService> logger)
    {
        _dbUpMigrationService = dbUpMigrationService;
        _logger = logger;
    }

    public async Task<bool> MigrateAsync(string environment = "Development")
    {
        try
        {
            _logger.LogInformation("Starting DbUp migration process for environment: {Environment}", environment);
            _logger.LogInformation("Handling views, stored procedures, and functions only");

            var success = await _dbUpMigrationService.MigrateAsync(environment);

            if (success)
            {
                _logger.LogInformation("DbUp migration completed successfully");
            }
            else
            {
                _logger.LogError("DbUp migration failed");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbUp migration process failed");
            return false;
        }
    }

    public async Task<bool> ValidateAsync()
    {
        try
        {
            _logger.LogInformation("Validating DbUp migrations");
            return await _dbUpMigrationService.ValidateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbUp migration validation failed");
            return false;
        }
    }

    public async Task<bool> RollbackAsync(string targetVersion)
    {
        try
        {
            _logger.LogInformation("Rolling back DbUp migrations to version: {TargetVersion}", targetVersion);
            return await _dbUpMigrationService.RollbackAsync(targetVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbUp migration rollback failed");
            return false;
        }
    }

    public async Task<IEnumerable<MigrationHistory>> GetMigrationHistoryAsync()
    {
        return await _dbUpMigrationService.GetMigrationHistoryAsync();
    }

    public async Task<bool> GenerateRollbackScriptAsync(string version, string outputPath)
    {
        try
        {
            _logger.LogInformation("Generating DbUp rollback script for version: {Version}", version);
            return await _dbUpMigrationService.GenerateRollbackScriptAsync(version, outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate DbUp rollback script");
            return false;
        }
    }
}