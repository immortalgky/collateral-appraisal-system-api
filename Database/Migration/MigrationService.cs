using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text;

namespace Database.Migration;

public class MigrationService : IMigrationService
{
    private readonly DatabaseMigrator _migrator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(
        DatabaseMigrator migrator,
        IConfiguration configuration,
        ILogger<MigrationService> logger)
    {
        _migrator = migrator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> MigrateAsync(string environment = "Development")
    {
        try
        {
            await EnsureMigrationHistoryTableAsync();

            if (_configuration.GetValue<bool>("DatabaseMigration:BackupDatabase"))
            {
                await BackupDatabaseAsync();
            }

            var result = await _migrator.MigrateAsync(environment);

            if (result && _configuration.GetValue<bool>($"Environments:{environment}:EnableSeeding"))
            {
                await SeedDataAsync(environment);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration service failed");
            return false;
        }
    }

    public async Task<bool> ValidateAsync()
    {
        return await _migrator.ValidateAsync();
    }

    public async Task<bool> RollbackAsync(string targetVersion)
    {
        return await _migrator.RollbackAsync(targetVersion);
    }

    public async Task<IEnumerable<MigrationHistory>> GetMigrationHistoryAsync()
    {
        var history = new List<MigrationHistory>();

        var connectionString = GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT Id, ScriptName, ScriptChecksum, ExecutedOn, ExecutedBy, 
                   ExecutionTimeMs, Environment, Version, Success, ErrorMessage
            FROM dbo.DatabaseMigrationHistory 
            ORDER BY ExecutedOn DESC";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            history.Add(new MigrationHistory
            {
                Id = reader.GetInt32(0),
                ScriptName = reader.GetString(1),
                ScriptChecksum = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ExecutedOn = reader.GetDateTime(3),
                ExecutedBy = reader.GetString(4),
                ExecutionTimeMs = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                Environment = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Version = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Success = reader.IsDBNull(8) || reader.GetBoolean(8),
                ErrorMessage = await reader.IsDBNullAsync(9) ? null : reader.GetString(9)
            });
        }

        return history;
    }

    public async Task<bool> GenerateRollbackScriptAsync(string version, string outputPath)
    {
        try
        {
            _logger.LogInformation("Generating rollback script for version: {Version}", version);

            var rollbackContent = await GenerateRollbackContentAsync(version);
            await File.WriteAllTextAsync(outputPath, rollbackContent);

            _logger.LogInformation("Rollback script generated: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate rollback script");
            return false;
        }
    }

    private async Task EnsureMigrationHistoryTableAsync()
    {
        var sql = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DatabaseMigrationHistory' AND xtype='U')
            BEGIN
                CREATE TABLE dbo.DatabaseMigrationHistory (
                    Id int IDENTITY(1,1) PRIMARY KEY,
                    ScriptName nvarchar(255) NOT NULL,
                    Applied datetime2 NOT NULL DEFAULT GETDATE(),
                    ScriptChecksum nvarchar(64) NULL,
                    ExecutedOn datetime2 NOT NULL DEFAULT GETDATE(),
                    ExecutedBy nvarchar(100) NOT NULL DEFAULT SYSTEM_USER,
                    ExecutionTimeMs int NULL,
                    Environment nvarchar(50) NULL,
                    Version nvarchar(50) NULL,
                    Success bit NULL,
                    ErrorMessage nvarchar(max) NULL
                );
                
                CREATE INDEX IX_DatabaseMigrationHistory_ScriptName ON dbo.DatabaseMigrationHistory(ScriptName);
                CREATE INDEX IX_DatabaseMigrationHistory_Applied ON dbo.DatabaseMigrationHistory(Applied);
            END";

        var connectionString = GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task BackupDatabaseAsync()
    {
        _logger.LogInformation("Creating database backup before migration");

        var configuredBackupPath = _configuration.GetValue<string>("DatabaseMigration:BackupPath");
        var backupDirectory = string.IsNullOrEmpty(configuredBackupPath) 
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DatabaseMigration",
                "Backups")
            : configuredBackupPath;

        var backupPath = Path.Combine(
            backupDirectory,
            $"CollateralAppraisal_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        var sql = $"BACKUP DATABASE [CollateralAppraisal] TO DISK = '{backupPath}'";

        var connectionString = GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Database backup created: {BackupPath}", backupPath);
    }

    private async Task SeedDataAsync(string environment)
    {
        _logger.LogInformation("Seeding data for environment: {Environment}", environment);

        var seedingMode = _configuration.GetValue<string>($"Environments:{environment}:SeedingMode");

        switch (seedingMode)
        {
            case "TestData":
                await ExecuteSeedScriptsAsync("TestData");
                await ExecuteSeedScriptsAsync("MasterData");
                break;
            case "MasterDataOnly":
                await ExecuteSeedScriptsAsync("MasterData");
                break;
            default:
                break;
        }
    }

    private async Task ExecuteSeedScriptsAsync(string seedType)
    {
        var scriptsPath = Path.Combine("Scripts", "Seed", seedType);
        if (!Directory.Exists(scriptsPath)) return;

        var scripts = Directory.GetFiles(scriptsPath, "*.sql")
            .OrderBy(f => f)
            .ToList();

        foreach (var script in scripts)
        {
            var sql = await File.ReadAllTextAsync(script);
            var connectionString = GetConnectionString();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Executed seed script: {Script}", Path.GetFileName(script));
        }
    }

    private async Task<string> GenerateRollbackContentAsync(string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- Rollback script for version {version}");
        sb.AppendLine($"-- Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var history = await GetMigrationHistoryAsync();
        var scriptsToRollback = history
            .Where(h => !string.IsNullOrEmpty(h.Version) && VersionExtractor.CompareVersions(h.Version, version) > 0)
            .OrderByDescending(h => h.ExecutedOn);

        if (scriptsToRollback.Any())
        {
            sb.AppendLine("-- Scripts to rollback (in reverse order):");
            sb.AppendLine("-- ==========================================");
            
            foreach (var script in scriptsToRollback)
            {
                sb.AppendLine($"-- Version {script.Version}: {script.ScriptName} (Applied: {script.ExecutedOn:yyyy-MM-dd HH:mm:ss})");
                
                // Generate rollback instruction based on script type
                if (script.ScriptName.ToLowerInvariant().Contains("view"))
                {
                    var viewName = ExtractObjectName(script.ScriptName, "view");
                    sb.AppendLine($"DROP VIEW IF EXISTS {viewName};");
                }
                else if (script.ScriptName.ToLowerInvariant().Contains("procedure") || script.ScriptName.ToLowerInvariant().Contains("proc"))
                {
                    var procName = ExtractObjectName(script.ScriptName, "procedure");
                    sb.AppendLine($"DROP PROCEDURE IF EXISTS {procName};");
                }
                else if (script.ScriptName.ToLowerInvariant().Contains("function") || script.ScriptName.ToLowerInvariant().Contains("func"))
                {
                    var funcName = ExtractObjectName(script.ScriptName, "function");
                    sb.AppendLine($"DROP FUNCTION IF EXISTS {funcName};");
                }
                else
                {
                    sb.AppendLine($"-- Manual rollback required for: {script.ScriptName}");
                }
                
                sb.AppendLine();
            }
            
            sb.AppendLine("-- Remove migration history entries:");
            sb.AppendLine("-- =================================");
            foreach (var script in scriptsToRollback)
            {
                sb.AppendLine($"DELETE FROM dbo.DatabaseMigrationHistory WHERE ScriptName = '{script.ScriptName}';");
            }
        }
        else
        {
            sb.AppendLine($"-- No migrations found newer than version {version}");
            sb.AppendLine("-- Nothing to rollback");
        }

        return sb.ToString();
    }

    private static string ExtractObjectName(string scriptName, string objectType)
    {
        // Try to extract object name from common naming patterns
        var fileName = Path.GetFileNameWithoutExtension(scriptName);
        
        // Pattern 1: vw_ObjectName, sp_ObjectName, fn_ObjectName
        if (objectType == "view" && fileName.StartsWith("vw_"))
        {
            return $"[dbo].[{fileName}]";
        }
        if (objectType == "procedure" && fileName.StartsWith("sp_"))
        {
            return $"[dbo].[{fileName}]";
        }
        if (objectType == "function" && fileName.StartsWith("fn_"))
        {
            return $"[dbo].[{fileName}]";
        }
        
        // Pattern 2: Extract from path (Scripts/Views/Schema/ObjectName.sql)
        var pathParts = scriptName.Split('/', '\\');
        if (pathParts.Length >= 3)
        {
            var schema = pathParts[^2]; // Second to last part (schema)
            var objName = Path.GetFileNameWithoutExtension(pathParts[^1]); // Last part (filename without extension)
            return $"[{schema}].[{objName}]";
        }
        
        // Fallback: use filename as object name
        return $"[dbo].[{fileName}]";
    }

    private string GetConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Always get connection string from appsettings.Database.json
        var configConnectionString = _configuration[$"Environments:{environment}:ConnectionString"];
        if (!string.IsNullOrEmpty(configConnectionString))
        {
            return configConnectionString;
        }

        throw new InvalidOperationException($"No connection string configured for environment '{environment}'. " +
                                            $"Set it in appsettings.Database.json under Environments:{environment}:ConnectionString.");
    }
}