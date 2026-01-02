using DbUp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Database.Migration;

public class DatabaseMigrator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IConfiguration configuration, ILogger<DatabaseMigrator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> MigrateAsync(string environment = "Development")
    {
        try
        {
            _logger.LogInformation("Starting database migration for environment: {Environment}", environment);

            var connectionString = GetConnectionString();

            // First, handle repeatable scripts (views, stored procedures, functions)
            await ExecuteRepeatableScripts(connectionString, environment);

            // Then handle one-time migration scripts
            var upgrader = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly,
                    script => FilterMigrationScripts(script, environment))
                .LogToAutodetectedLog()
                .WithTransaction()
                .WithExecutionTimeout(TimeSpan.FromSeconds(300))
                .JournalToSqlTable("dbo", "DatabaseMigrationHistory")
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                _logger.LogError(result.Error, "Database migration failed");
                return false;
            }

            _logger.LogInformation("Database migration completed successfully. Scripts executed: {Count}",
                result.Scripts.Count());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database migration failed with exception");
            return false;
        }
    }

    public async Task<bool> ValidateAsync()
    {
        try
        {
            var connectionString = GetConnectionString();
            var upgrader = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
                .LogToAutodetectedLog()
                .WithTransaction()
                .JournalToSqlTable("dbo", "DatabaseMigrationHistory")
                .Build();

            var scripts = upgrader.GetScriptsToExecute();

            _logger.LogInformation("Found {Count} pending migration scripts", scripts.Count());

            foreach (var script in scripts)
            {
                _logger.LogInformation("Pending script: {ScriptName}", script.Name);
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration validation failed");
            return false;
        }
    }

    public async Task<bool> RollbackAsync(string targetVersion)
    {
        try
        {
            _logger.LogInformation("Starting rollback to version: {TargetVersion}", targetVersion);

            // For now, just log that rollback is not implemented
            _logger.LogWarning("Rollback functionality is not yet implemented");

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed");
            return false;
        }
    }

    private async Task ExecuteRepeatableScripts(string connectionString, string environment)
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var repeatableScripts = assembly.GetManifestResourceNames()
            .Where(name => IsRepeatableScript(name) && FilterScriptsByEnvironment(name, environment))
            .OrderBy(name => name);

        foreach (var scriptName in repeatableScripts)
        {
            var scriptContent = GetEmbeddedScript(assembly, scriptName);
            var currentChecksum = CalculateChecksum(scriptContent);

            if (await ShouldExecuteRepeatableScript(connectionString, scriptName, currentChecksum))
            {
                _logger.LogInformation("Executing repeatable script: {ScriptName}", scriptName);
                await ExecuteScript(connectionString, scriptName, scriptContent, currentChecksum);
            }
            else
            {
                _logger.LogDebug("Skipping unchanged repeatable script: {ScriptName}", scriptName);
            }
        }
    }

    private static bool IsRepeatableScript(string scriptName)
    {
        return scriptName.Contains(".Scripts.Views.") ||
               scriptName.Contains(".Scripts.StoredProcedures.") ||
               scriptName.Contains(".Scripts.Functions.");
    }

    private static bool FilterMigrationScripts(string scriptName, string environment)
    {
        // Only include migration scripts, not repeatable ones
        return scriptName.Contains(".Migration.Scripts.") && FilterScriptsByEnvironment(scriptName, environment);
    }

    private static string GetEmbeddedScript(System.Reflection.Assembly assembly, string scriptName)
    {
        using var stream = assembly.GetManifestResourceStream(scriptName);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static string CalculateChecksum(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    private static async Task<bool> ShouldExecuteRepeatableScript(string connectionString, string scriptName,
        string currentChecksum)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT ScriptChecksum 
            FROM dbo.DatabaseMigrationHistory 
            WHERE ScriptName = @ScriptName";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ScriptName", scriptName);

        var storedChecksum = await command.ExecuteScalarAsync() as string;
        return storedChecksum != currentChecksum;
    }

    private async Task ExecuteScript(string connectionString, string scriptName, string scriptContent, string checksum)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            // Execute the script
            using var scriptCommand = new SqlCommand(scriptContent, connection, transaction);
            scriptCommand.CommandTimeout = 300;
            await scriptCommand.ExecuteNonQueryAsync();

            // Extract version from script name
            var version = VersionExtractor.ExtractVersion(scriptName);
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var executedBy = Environment.UserName ?? "System";

            // Update or insert the journal record with full details
            var upsertSql = @"
                MERGE dbo.DatabaseMigrationHistory AS target
                USING (SELECT 
                    @ScriptName AS ScriptName, 
                    @Checksum AS ScriptChecksum, 
                    GETDATE() AS Applied,
                    @Version AS Version,
                    @Environment AS Environment,
                    @ExecutedBy AS ExecutedBy,
                    @ExecutionTimeMs AS ExecutionTimeMs,
                    1 AS Success
                ) AS source
                ON target.ScriptName = source.ScriptName
                WHEN MATCHED THEN
                    UPDATE SET 
                        ScriptChecksum = source.ScriptChecksum, 
                        Applied = source.Applied,
                        Version = source.Version,
                        Environment = source.Environment,
                        ExecutedBy = source.ExecutedBy,
                        ExecutionTimeMs = source.ExecutionTimeMs,
                        Success = source.Success
                WHEN NOT MATCHED THEN
                    INSERT (ScriptName, Applied, ScriptChecksum, Version, Environment, ExecutedBy, ExecutionTimeMs, Success)
                    VALUES (source.ScriptName, source.Applied, source.ScriptChecksum, source.Version, source.Environment, source.ExecutedBy, source.ExecutionTimeMs, source.Success);";

            using var journalCommand = new SqlCommand(upsertSql, connection, transaction);
            journalCommand.Parameters.AddWithValue("@ScriptName", scriptName);
            journalCommand.Parameters.AddWithValue("@Checksum", checksum);
            journalCommand.Parameters.AddWithValue("@Version", version);
            journalCommand.Parameters.AddWithValue("@Environment", environment);
            journalCommand.Parameters.AddWithValue("@ExecutedBy", executedBy);
            journalCommand.Parameters.AddWithValue("@ExecutionTimeMs", 0); // Could measure actual execution time later
            await journalCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Migration script executed: {ScriptName} (Version: {Version})", scriptName, version);

            await transaction.CommitAsync();
            _logger.LogInformation("Successfully executed script: {ScriptName}", scriptName);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private string GetConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Try config file first (appsettings.Database.json)
        var configConnectionString = _configuration[$"Environments:{environment}:ConnectionString"];
        if (!string.IsNullOrEmpty(configConnectionString))
        {
            return configConnectionString;
        }

        // Try standard ConnectionStrings section
        var standardConnectionString = _configuration.GetConnectionString("DefaultConnection")
                                       ?? _configuration.GetConnectionString($"Database:{environment}");
        if (!string.IsNullOrEmpty(standardConnectionString))
        {
            return standardConnectionString;
        }

        // Try environment variable
        var envConnectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        throw new InvalidOperationException($"No connection string configured for environment '{environment}'. " +
                                            $"Set it in appsettings.Database.json under Environments:{environment}:ConnectionString, " +
                                            $"or in the DATABASE_CONNECTION_STRING environment variable.");
    }

    private static bool FilterScriptsByEnvironment(string scriptName, string environment)
    {
        if (scriptName.Contains(".env."))
        {
            return scriptName.Contains($".env.{environment.ToLower()}.");
        }

        return true;
    }
}