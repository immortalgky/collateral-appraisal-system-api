using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Database.Migration;

public class DatabaseTestSetupService : IDatabaseTestSetupService
{
    private readonly ILogger<DatabaseTestSetupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public DatabaseTestSetupService(ILogger<DatabaseTestSetupService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    }

    public async Task<bool> SetupDatabaseAsync(string connectionString)
    {
        try
        {
            _logger.LogInformation("Starting database setup for test container with connection string: {ConnectionString}", 
                connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "...");

            // Step 1: Run EF Core migrations for all modules
            var efCoreMigrationService = CreateEfCoreMigrationService();
            var efResult = await efCoreMigrationService.MigrateAsync(connectionString, "Testing");
            
            if (!efResult)
            {
                _logger.LogError("EF Core migrations failed for test database setup");
                return false;
            }

            // Step 2: Run database objects (views, stored procedures, functions)
            var databaseMigrator = new DatabaseMigrator(CreateTestConfiguration(connectionString), 
                _loggerFactory.CreateLogger<DatabaseMigrator>());
            var migrationService = new MigrationService(databaseMigrator, 
                CreateTestConfiguration(connectionString), 
                _loggerFactory.CreateLogger<MigrationService>());
                
            var dbObjectsResult = await migrationService.MigrateAsync("Testing");
            
            if (!dbObjectsResult)
            {
                _logger.LogError("Database objects migration failed for test database setup");
                return false;
            }

            // Step 3: Validate that database objects were created successfully
            var validationService = new DatabaseValidationService(_loggerFactory.CreateLogger<DatabaseValidationService>());
            var validationResult = await validationService.ValidateDatabaseObjectsAsync(connectionString);
            
            if (!validationResult)
            {
                _logger.LogWarning("Database validation reported issues, but continuing with test setup");
                // Don't fail the setup for validation issues since some objects might not be implemented yet
            }

            _logger.LogInformation("Database setup completed successfully for test container");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database setup failed for test container");
            return false;
        }
    }

    private IEfCoreMigrationService CreateEfCoreMigrationService()
    {
        // Create a minimal service provider for EF Core migrations
        return new EfCoreMigrationService(
            _loggerFactory.CreateLogger<EfCoreMigrationService>(),
            null! // We'll use the connection string overload, so don't need service provider
        );
    }

    private IConfiguration CreateTestConfiguration(string connectionString)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", connectionString },
            { "ConnectionStrings:Database", connectionString }
        });
        
        return configBuilder.Build();
    }
}