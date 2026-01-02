using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Database.Migration;
using Database.Extensions;

namespace Database;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        using var scope = host.Services.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        var coordinator = scope.ServiceProvider.GetRequiredService<MigrationCoordinator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            return await ExecuteCommand(args, coordinator, logger, scope);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database CLI failed");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ExecuteCommand(
        string[] args,
        //IMigrationService migrationService, 
        MigrationCoordinator coordinator,
        ILogger logger,
        IServiceScope? scope = null)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();

        switch (command)
        {
            case "migrate":
                var environment = args.Length > 1 ? args[1] : "Development";
                Console.WriteLine($"Running migrations for {environment} environment...");
                var result = await coordinator.MigrateAllAsync(environment);
                Console.WriteLine(result ? "Migration completed successfully" : "Migration failed");
                return result ? 0 : 1;

            // case "validate":
            //     Console.WriteLine("Validating pending migrations...");
            //     var validateResult = await migrationService.ValidateAsync();
            //     Console.WriteLine(validateResult ? "Validation passed" : "Validation failed");
            //     return validateResult ? 0 : 1;
            //     
            // case "rollback":
            //     if (args.Length < 2)
            //     {
            //         Console.WriteLine("Error: Rollback command requires target version");
            //         Console.WriteLine("Usage: rollback <version>");
            //         return 1;
            //     }
            //     Console.WriteLine($"Rolling back to version: {args[1]}");
            //     var rollbackResult = await migrationService.RollbackAsync(args[1]);
            //     Console.WriteLine(rollbackResult ? "Rollback completed successfully" : "Rollback failed");
            //     return rollbackResult ? 0 : 1;
            //     
            // case "history":
            //     Console.WriteLine("Migration History:");
            //     Console.WriteLine("==================");
            //     var history = await migrationService.GetMigrationHistoryAsync();
            //     foreach (var item in history.Take(20))
            //     {
            //         var status = item.Success ? "SUCCESS" : "FAILED";
            //         var version = string.IsNullOrEmpty(item.Version) ? "N/A" : item.Version;
            //         Console.WriteLine($"{item.ExecutedOn:yyyy-MM-dd HH:mm:ss} - v{version} - {item.ScriptName} - {status}");
            //     }
            //     return 0;
            //     
            // case "generate-rollback":
            //     if (args.Length < 3)
            //     {
            //         Console.WriteLine("Error: Generate-rollback command requires version and output path");
            //         Console.WriteLine("Usage: generate-rollback <version> <output-path>");
            //         return 1;
            //     }
            //     Console.WriteLine($"Generating rollback script for version {args[1]}...");
            //     var generateResult = await migrationService.GenerateRollbackScriptAsync(args[1], args[2]);
            //     Console.WriteLine(generateResult ? $"Rollback script generated: {args[2]}" : "Failed to generate rollback script");
            //     return generateResult ? 0 : 1;


            case "help":
            case "--help":
            case "-h":
                ShowHelp();
                return 0;

            default:
                Console.WriteLine($"Unknown command: {command}");
                ShowHelp();
                return 1;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Database Migration CLI (Views, Stored Procedures, Functions)");
        Console.WriteLine("============================================================");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet run --project Database/Database.csproj <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  migrate [environment]              Run DbUp migrations (default: Development)");
        Console.WriteLine("  validate                           Validate pending DbUp migrations");
        Console.WriteLine("  rollback <version>                 Rollback to specific version");
        Console.WriteLine("  history                            Show DbUp migration history");
        Console.WriteLine("  generate-rollback <version> <path> Generate rollback script");
        Console.WriteLine("  help                               Show this help message");
        Console.WriteLine();
        Console.WriteLine("Note: This tool only handles database objects (views, stored procedures, functions).");
        Console.WriteLine("      Table migrations are handled by EF Core in individual modules.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project Database/Database.csproj migrate");
        Console.WriteLine("  dotnet run --project Database/Database.csproj migrate Production");
        Console.WriteLine("  dotnet run --project Database/Database.csproj validate");
        Console.WriteLine("  dotnet run --project Database/Database.csproj history");
        Console.WriteLine("  dotnet run --project Database/Database.csproj rollback 1.0.0");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  DATABASE_CONNECTION_STRING         Database connection string");
        Console.WriteLine("  ASPNETCORE_ENVIRONMENT             Environment name (Development/Production/etc.)");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var basePath = context.HostingEnvironment.ContentRootPath;

                // Handle both scenarios: running from Database folder or from solution root with --project
                var configPath = Path.Combine(basePath, "Configuration", "appsettings.Database.json");
                if (!File.Exists(configPath))
                {
                    // Try from solution root (when using --project Database/Database.csproj)
                    configPath = Path.Combine(basePath, "Database", "Configuration", "appsettings.Database.json");
                }

                config.AddJsonFile(configPath, optional: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) => { services.AddDatabaseMigration(context.Configuration); })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}