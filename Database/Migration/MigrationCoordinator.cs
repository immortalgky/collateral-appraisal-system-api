using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Database.Migration;

public class MigrationCoordinator
{
    private readonly ILogger<MigrationCoordinator> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MigrationCoordinator(ILogger<MigrationCoordinator> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> MigrateAllAsync(string environment = "Development")
    {
        return await MigrateAllAsync(null, environment);
    }

    public async Task<bool> MigrateAllAsync(string? connectionString, string environment = "Development")
    {
        using var scope = _serviceProvider.CreateScope();
        var migrationService = (IMigrationService)scope.ServiceProvider.GetRequiredService(typeof(IMigrationService));
        var efCoreMigrationService =
            (IEfCoreMigrationService)scope.ServiceProvider.GetRequiredService(typeof(IEfCoreMigrationService));

        try
        {
            _logger.LogInformation("Starting coordinated migration for environment: {Environment}", environment);

            // Step 1: Run EF Core migrations for all modules
            var efMigrationResult = !string.IsNullOrEmpty(connectionString)
                ? await efCoreMigrationService.MigrateAsync(connectionString, environment)
                : await efCoreMigrationService.MigrateAsync(environment);

            if (!efMigrationResult)
            {
                _logger.LogError("EF Core migrations failed - aborting database object deployment");
                return false;
            }

            // Step 2: Run database object migrations
            var dbObjectMigrationResult = await migrationService.MigrateAsync(environment);
            if (!dbObjectMigrationResult)
            {
                _logger.LogError("Database object migrations failed");
                return false;
            }

            _logger.LogInformation("Coordinated migration completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordinated migration failed");
            return false;
        }
    }
}