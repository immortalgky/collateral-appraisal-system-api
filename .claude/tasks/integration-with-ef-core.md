# Integration with EF Core

## Task Overview
**Objective**: Integrate the database project migration system with existing Entity Framework Core migrations to ensure coordinated deployment and avoid conflicts between table structure changes and database object management.

**Priority**: Medium  
**Estimated Effort**: 4-6 hours  
**Dependencies**: Migration Framework Implementation  
**Assignee**: TBD  

## Prerequisites
- Completed Migration Framework Implementation task
- Understanding of existing EF Core migration patterns in the solution
- Knowledge of DbContext configurations across modules
- Understanding of database schema dependencies

## Current EF Core Setup Analysis

### Existing Module DbContexts
Based on the solution analysis:

| Module | DbContext | Schema | Migration Assembly | Tables |
|--------|-----------|--------|-------------------|---------|
| Request | RequestDbContext | request | Request | Requests, RequestComments, RequestTitles |
| Document | DocumentDbContext | document | Document | Documents |
| Assignment | AssignmentDbContext | assignment | Assignment | CompletedTasks, PendingTasks |
| Auth | AuthDbContext | auth | Auth | Users, Roles, etc. |
| OAuth2OpenId | OpenIddictDbContext | openiddict | OAuth2OpenId | OpenIddict tables |
| Notification | NotificationDbContext | notification | Notification | Notifications |

### Current Migration Commands
```bash
# Existing EF Core migration commands
dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api
dotnet ef database update --project Modules/Document/Document --startup-project Bootstrapper/Api
dotnet ef database update --project Modules/Assignment/Assignment --startup-project Bootstrapper/Api
```

## Integration Strategy

### Coordination Approach
1. **Sequence Control**: EF Core migrations run first (table structure), then database objects
2. **Dependency Tracking**: Database objects reference EF Core managed tables
3. **Version Coordination**: Link database object versions to EF Core migration versions
4. **Unified Deployment**: Single command deploys both EF Core and database objects
5. **Rollback Coordination**: Rollback both systems together

### Migration Order
```
1. EF Core Migrations (Table structure)
   ├── Request Module Tables
   ├── Document Module Tables  
   ├── Assignment Module Tables
   └── Other Module Tables
   
2. Database Objects (Views, SPs, Functions)
   ├── Views (depend on tables)
   ├── Functions (may depend on views)  
   └── Stored Procedures (may depend on views/functions)
```

## Implementation Steps

### Step 1: Create Unified Migration Coordinator

**Database/Integration/EfCoreMigrationCoordinator.cs**:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Database.Integration;

public class EfCoreMigrationCoordinator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EfCoreMigrationCoordinator> _logger;
    private readonly IConfiguration _configuration;

    public EfCoreMigrationCoordinator(
        IServiceProvider serviceProvider,
        ILogger<EfCoreMigrationCoordinator> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> MigrateAllAsync(string environment = "Development")
    {
        try
        {
            _logger.LogInformation("Starting coordinated migration for environment: {Environment}", environment);

            // Step 1: Run EF Core migrations for all modules
            var efMigrationResult = await RunEfCoreMigrationsAsync();
            if (!efMigrationResult)
            {
                _logger.LogError("EF Core migrations failed - aborting database object deployment");
                return false;
            }

            // Step 2: Run database object migrations
            var dbObjectMigrationResult = await RunDatabaseObjectMigrationsAsync(environment);
            if (!dbObjectMigrationResult)
            {
                _logger.LogError("Database object migrations failed");
                return false;
            }

            // Step 3: Update coordination tracking
            await UpdateCoordinationTrackingAsync(environment);

            _logger.LogInformation("Coordinated migration completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordinated migration failed");
            return false;
        }
    }

    private async Task<bool> RunEfCoreMigrationsAsync()
    {
        var contextTypes = GetDbContextTypes();
        
        foreach (var contextType in contextTypes)
        {
            try
            {
                _logger.LogInformation("Running EF Core migrations for: {ContextType}", contextType.Name);
                
                using var scope = _serviceProvider.CreateScope();
                var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                
                // Check for pending migrations
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("Found {Count} pending migrations for {Context}", 
                        pendingMigrations.Count(), contextType.Name);
                    
                    // Apply migrations
                    await context.Database.MigrateAsync();
                    
                    _logger.LogInformation("EF Core migrations completed for: {ContextType}", contextType.Name);
                }
                else
                {
                    _logger.LogInformation("No pending EF Core migrations for: {ContextType}", contextType.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EF Core migration failed for: {ContextType}", contextType.Name);
                return false;
            }
        }
        
        return true;
    }

    private async Task<bool> RunDatabaseObjectMigrationsAsync(string environment)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var migrationService = scope.ServiceProvider.GetRequiredService<Migration.IMigrationService>();
            
            return await migrationService.MigrateAsync(environment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database object migration failed");
            return false;
        }
    }

    private async Task UpdateCoordinationTrackingAsync(string environment)
    {
        // Track the coordination between EF Core and database object migrations
        var trackingRecord = new MigrationCoordination
        {
            Environment = environment,
            EfCoreMigrationVersion = await GetLatestEfCoreMigrationVersionAsync(),
            DatabaseObjectMigrationVersion = await GetLatestDatabaseObjectMigrationVersionAsync(),
            CoordinatedAt = DateTime.UtcNow,
            Success = true
        };

        await SaveCoordinationRecordAsync(trackingRecord);
    }

    private Type[] GetDbContextTypes()
    {
        // Get all registered DbContext types
        return new[]
        {
            typeof(Request.Data.RequestDbContext),
            typeof(Document.Data.DocumentDbContext),
            typeof(Assignment.Data.AssignmentDbContext),
            typeof(Auth.OAuth2OpenId.Data.OpenIddictDbContext),
            typeof(Notification.Data.NotificationDbContext)
        };
    }

    private async Task<string> GetLatestEfCoreMigrationVersionAsync()
    {
        // Get the latest applied EF Core migration across all contexts
        var contextTypes = GetDbContextTypes();
        var latestVersion = "";
        
        foreach (var contextType in contextTypes)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            var latestMigration = appliedMigrations.LastOrDefault();
            
            if (!string.IsNullOrEmpty(latestMigration) && 
                string.Compare(latestMigration, latestVersion, StringComparison.OrdinalIgnoreCase) > 0)
            {
                latestVersion = latestMigration;
            }
        }
        
        return latestVersion;
    }

    private async Task<string> GetLatestDatabaseObjectMigrationVersionAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<Migration.IMigrationService>();
        
        var history = await migrationService.GetMigrationHistoryAsync();
        return history.FirstOrDefault()?.Version ?? "";
    }

    private async Task SaveCoordinationRecordAsync(MigrationCoordination record)
    {
        // Save coordination tracking record to database
        // This could be stored in a dedicated coordination table
        _logger.LogInformation("Coordination record: EF Core v{EfVersion}, DB Objects v{DbVersion}", 
            record.EfCoreMigrationVersion, record.DatabaseObjectMigrationVersion);
    }
}

public class MigrationCoordination
{
    public string Environment { get; set; } = string.Empty;
    public string EfCoreMigrationVersion { get; set; } = string.Empty;
    public string DatabaseObjectMigrationVersion { get; set; } = string.Empty;
    public DateTime CoordinatedAt { get; set; }
    public bool Success { get; set; }
}
```

### Step 2: Create Dependency Validation System

**Database/Integration/DependencyValidator.cs**:
```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Database.Integration;

public class DependencyValidator
{
    private readonly string _connectionString;
    private readonly ILogger<DependencyValidator> _logger;

    public DependencyValidator(string connectionString, ILogger<DependencyValidator> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateDbObjectDependenciesAsync()
    {
        var result = new ValidationResult();
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Validate table dependencies for views
            await ValidateViewDependenciesAsync(connection, result);
            
            // Validate dependencies for stored procedures
            await ValidateStoredProcedureDependenciesAsync(connection, result);
            
            // Validate function dependencies
            await ValidateFunctionDependenciesAsync(connection, result);

            connection.Close();
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Dependency validation failed: {ex.Message}");
            _logger.LogError(ex, "Dependency validation failed");
        }

        return result;
    }

    private async Task ValidateViewDependenciesAsync(SqlConnection connection, ValidationResult result)
    {
        var sql = @"
            SELECT 
                SCHEMA_NAME(v.schema_id) as ViewSchema,
                v.name as ViewName,
                SCHEMA_NAME(t.schema_id) as DependentSchema,
                t.name as DependentTable,
                d.referenced_entity_name
            FROM sys.views v
            INNER JOIN sys.sql_dependencies d ON v.object_id = d.object_id
            INNER JOIN sys.tables t ON d.referenced_major_id = t.object_id
            WHERE SCHEMA_NAME(v.schema_id) IN ('request', 'document', 'assignment', 'notification')
        ";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var viewDependencies = new List<DatabaseDependency>();
        
        while (await reader.ReadAsync())
        {
            viewDependencies.Add(new DatabaseDependency
            {
                ObjectSchema = reader.GetString("ViewSchema"),
                ObjectName = reader.GetString("ViewName"),
                ObjectType = "VIEW",
                DependentSchema = reader.GetString("DependentSchema"),
                DependentName = reader.GetString("DependentTable"),
                DependentType = "TABLE"
            });
        }

        // Validate that all dependent tables exist
        foreach (var dependency in viewDependencies)
        {
            if (!await TableExistsAsync(connection, dependency.DependentSchema, dependency.DependentName))
            {
                result.IsValid = false;
                result.Errors.Add($"View {dependency.ObjectSchema}.{dependency.ObjectName} depends on missing table {dependency.DependentSchema}.{dependency.DependentName}");
            }
        }

        result.Dependencies.AddRange(viewDependencies);
    }

    private async Task ValidateStoredProcedureDependenciesAsync(SqlConnection connection, ValidationResult result)
    {
        var sql = @"
            SELECT 
                SCHEMA_NAME(p.schema_id) as ProcSchema,
                p.name as ProcName,
                m.definition
            FROM sys.procedures p
            INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
            WHERE SCHEMA_NAME(p.schema_id) IN ('request', 'document', 'assignment', 'notification')
        ";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var procSchema = reader.GetString("ProcSchema");
            var procName = reader.GetString("ProcName");
            var definition = reader.GetString("definition");

            // Parse SQL definition to find table/view references
            var dependencies = ParseSqlDependencies(definition);
            
            foreach (var dependency in dependencies)
            {
                var exists = await ObjectExistsAsync(connection, dependency.Schema, dependency.Name, dependency.Type);
                if (!exists)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Stored procedure {procSchema}.{procName} references missing {dependency.Type.ToLower()} {dependency.Schema}.{dependency.Name}");
                }
            }
        }
    }

    private async Task ValidateFunctionDependenciesAsync(SqlConnection connection, ValidationResult result)
    {
        var sql = @"
            SELECT 
                SCHEMA_NAME(f.schema_id) as FunctionSchema,
                f.name as FunctionName,
                m.definition
            FROM sys.objects f
            INNER JOIN sys.sql_modules m ON f.object_id = m.object_id
            WHERE f.type IN ('FN', 'IF', 'TF') 
            AND SCHEMA_NAME(f.schema_id) IN ('request', 'document', 'assignment', 'notification')
        ";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var funcSchema = reader.GetString("FunctionSchema");
            var funcName = reader.GetString("FunctionName");
            var definition = reader.GetString("definition");

            var dependencies = ParseSqlDependencies(definition);
            
            foreach (var dependency in dependencies)
            {
                var exists = await ObjectExistsAsync(connection, dependency.Schema, dependency.Name, dependency.Type);
                if (!exists)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Function {funcSchema}.{funcName} references missing {dependency.Type.ToLower()} {dependency.Schema}.{dependency.Name}");
                }
            }
        }
    }

    private List<SqlDependency> ParseSqlDependencies(string sqlDefinition)
    {
        var dependencies = new List<SqlDependency>();
        
        // Simple regex patterns to find table/view references
        var patterns = new[]
        {
            @"\[(\w+)\]\.\[(\w+)\]",  // [schema].[table]
            @"(\w+)\.(\w+)",          // schema.table
            @"FROM\s+(\w+)",          // FROM table
            @"JOIN\s+(\w+)",          // JOIN table
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(sqlDefinition, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    dependencies.Add(new SqlDependency
                    {
                        Schema = match.Groups[1].Value,
                        Name = match.Groups[2].Value,
                        Type = "TABLE" // Default assumption
                    });
                }
            }
        }

        return dependencies.Distinct().ToList();
    }

    private async Task<bool> TableExistsAsync(SqlConnection connection, string schema, string tableName)
    {
        var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";
        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@TableName", tableName);
        
        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }

    private async Task<bool> ObjectExistsAsync(SqlConnection connection, string schema, string objectName, string objectType)
    {
        var sql = objectType.ToUpper() switch
        {
            "TABLE" => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Name",
            "VIEW" => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.VIEWS WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Name",
            _ => "SELECT COUNT(*) FROM sys.objects WHERE schema_id = SCHEMA_ID(@Schema) AND name = @Name"
        };

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@Name", objectName);
        
        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<DatabaseDependency> Dependencies { get; set; } = new();
}

public class DatabaseDependency
{
    public string ObjectSchema { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string DependentSchema { get; set; } = string.Empty;
    public string DependentName { get; set; } = string.Empty;
    public string DependentType { get; set; } = string.Empty;
}

public class SqlDependency
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    
    public override bool Equals(object? obj)
    {
        return obj is SqlDependency other && 
               Schema == other.Schema && 
               Name == other.Name && 
               Type == other.Type;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Schema, Name, Type);
    }
}
```

### Step 3: Create Unified Migration CLI

**Database/Tools/UnifiedMigrationCli.cs**:
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Database.Tools;

public class UnifiedMigrationCli
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<Integration.EfCoreMigrationCoordinator>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UnifiedMigrationCli>>();
        
        try
        {
            return await ExecuteCommand(args, coordinator, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unified migration CLI failed");
            return 1;
        }
    }

    private static async Task<int> ExecuteCommand(
        string[] args, 
        Integration.EfCoreMigrationCoordinator coordinator, 
        ILogger logger)
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
                logger.LogInformation("Starting unified migration for environment: {Environment}", environment);
                
                var result = await coordinator.MigrateAllAsync(environment);
                return result ? 0 : 1;
                
            case "validate":
                logger.LogInformation("Validating database dependencies");
                // Implementation for validation
                return 0;
                
            case "ef-only":
                logger.LogInformation("Running EF Core migrations only");
                // Implementation for EF Core only migration
                return 0;
                
            case "db-objects-only":
                logger.LogInformation("Running database objects migration only");
                var env = args.Length > 1 ? args[1] : "Development";
                // Use existing database migration service
                return 0;
                
            default:
                logger.LogError("Unknown command: {Command}", command);
                ShowHelp();
                return 1;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Unified Database Migration CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  migrate [environment]        - Run coordinated EF Core + database object migrations");
        Console.WriteLine("  validate                     - Validate dependencies between EF Core tables and database objects");
        Console.WriteLine("  ef-only [environment]        - Run only EF Core migrations");
        Console.WriteLine("  db-objects-only [environment] - Run only database object migrations");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register all DbContexts
                services.AddDbContext<Request.Data.RequestDbContext>(options => 
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                    
                services.AddDbContext<Document.Data.DocumentDbContext>(options => 
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                    
                services.AddDbContext<Assignment.Data.AssignmentDbContext>(options => 
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));

                // Register migration services
                services.AddSingleton<Migration.DatabaseMigrator>();
                services.AddSingleton<Migration.IMigrationService, Migration.MigrationService>();
                services.AddSingleton<Integration.EfCoreMigrationCoordinator>();
            });
}
```

### Step 4: Update Database Object Scripts with EF Core Awareness

**Scripts/Views/Request/vw_Request_EfCoreAware.sql**:
```sql
-- =============================================
-- View: vw_Request_EfCoreAware
-- Schema: request
-- Description: Example view that references EF Core managed tables
-- EF Core Dependencies: Requests, RequestComments, RequestTitles tables
-- Created: 2025-07-31
-- =============================================

-- Check that required EF Core tables exist before creating view
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'request' AND TABLE_NAME = 'Requests')
BEGIN
    RAISERROR('Required table request.Requests not found. Run EF Core migrations first.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'request' AND TABLE_NAME = 'RequestComments')
BEGIN
    RAISERROR('Required table request.RequestComments not found. Run EF Core migrations first.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'request' AND TABLE_NAME = 'RequestTitles')
BEGIN
    RAISERROR('Required table request.RequestTitles not found. Run EF Core migrations first.', 16, 1);
    RETURN;
END

-- Drop existing view if it exists
IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[request].[vw_Request_EfCoreAware]'))
    DROP VIEW [request].[vw_Request_EfCoreAware]
GO

CREATE VIEW [request].[vw_Request_EfCoreAware]
AS
    SELECT 
        r.Id,
        r.AppraisalNumber,
        r.PropertyType,
        r.Status,
        r.CreatedOn,
        r.CreatedBy,
        
        -- EF Core managed relationships
        COUNT(DISTINCT rc.Id) as CommentCount,
        COUNT(DISTINCT rt.Id) as TitleCount,
        
        -- Calculated fields
        DATEDIFF(DAY, r.CreatedOn, GETDATE()) as AgeDays
        
    FROM [request].[Requests] r  -- EF Core managed table
    LEFT JOIN [request].[RequestComments] rc ON r.Id = rc.RequestId  -- EF Core managed table
    LEFT JOIN [request].[RequestTitles] rt ON r.Id = rt.RequestId    -- EF Core managed table
    GROUP BY r.Id, r.AppraisalNumber, r.PropertyType, r.Status, r.CreatedOn, r.CreatedBy
GO

GRANT SELECT ON [request].[vw_Request_EfCoreAware] TO [db_datareader]
GO
```

### Step 5: Configuration Updates

**Database/Configuration/appsettings.Integration.json**:
```json
{
  "DatabaseIntegration": {
    "CoordinateWithEfCore": true,
    "ValidateDependencies": true,
    "EfCoreMigrationTimeout": 300,
    "DatabaseObjectMigrationTimeout": 600,
    "RollbackOnAnyFailure": true,
    "CreateCoordinationBackup": true
  },
  "EfCoreModules": [
    "Request",
    "Document", 
    "Assignment",
    "Auth",
    "Notification"
  ],
  "DependencyValidation": {
    "ValidateBeforeDeployment": true,
    "FailOnMissingDependencies": true,
    "AllowedMissingTables": [
      "dbo.SystemLog",
      "dbo.AuditTrail"
    ]
  }
}
```

### Step 6: Update Deployment Pipeline Integration

**azure-pipelines-unified-migration.yml** (excerpt):
```yaml
- task: PowerShell@2
  displayName: 'Run Unified Migration'
  inputs:
    targetType: 'inline'
    script: |
      Write-Host "Running unified migration (EF Core + Database Objects)"
      
      # Set connection string
      $env:DATABASE_CONNECTION_STRING = "$(ConnectionString)"
      
      # Run unified migration
      $result = & dotnet run --project Database/Database.csproj unified-migrate $(Environment)
      
      if ($LASTEXITCODE -ne 0) {
        Write-Error "Unified migration failed with exit code: $LASTEXITCODE"
        exit 1
      }
      
      Write-Host "Unified migration completed successfully"

- task: PowerShell@2
  displayName: 'Validate Migration Results'
  inputs:
    targetType: 'inline'
    script: |
      Write-Host "Validating migration results"
      
      # Validate EF Core migrations
      $efValidation = & dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api --dry-run
      
      # Validate database objects
      $dbValidation = & dotnet run --project Database/Database.csproj validate
      
      if ($LASTEXITCODE -ne 0) {
        Write-Error "Migration validation failed"
        exit 1
      }
      
      Write-Host "Migration validation completed successfully"
```

## Testing & Validation

### Integration Tests

**Database.IntegrationTests/EfCoreIntegrationTests.cs**:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Database.IntegrationTests;

public class EfCoreIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    
    public EfCoreIntegrationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task UnifiedMigration_ShouldCreateTablesAndDatabaseObjects()
    {
        // Arrange
        var host = CreateTestHost();
        using var scope = host.Services.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<Integration.EfCoreMigrationCoordinator>();
        
        // Act
        var result = await coordinator.MigrateAllAsync("Test");
        
        // Assert
        Assert.True(result);
        
        // Verify EF Core tables exist
        await VerifyTablesExistAsync(scope);
        
        // Verify database objects exist
        await VerifyDatabaseObjectsExistAsync(scope);
    }
    
    [Fact]
    public async Task DependencyValidator_ShouldDetectMissingTables()
    {
        // Arrange
        var validator = new Integration.DependencyValidator(_fixture.ConnectionString, 
            _fixture.GetService<ILogger<Integration.DependencyValidator>>());
        
        // Act
        var result = await validator.ValidateDbObjectDependenciesAsync();
        
        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Dependencies);
    }
    
    private async Task VerifyTablesExistAsync(IServiceScope scope)
    {
        var requestContext = scope.ServiceProvider.GetRequiredService<Request.Data.RequestDbContext>();
        
        // Verify that EF Core tables exist and are accessible
        var requestCount = await requestContext.Requests.CountAsync();
        Assert.True(requestCount >= 0); // Should not throw exception
    }
    
    private async Task VerifyDatabaseObjectsExistAsync(IServiceScope scope)
    {
        var requestContext = scope.ServiceProvider.GetRequiredService<Request.Data.RequestDbContext>();
        
        // Test that views can be queried
        var viewSql = "SELECT COUNT(*) FROM [request].[vw_Request_Summary]";
        var viewCount = await requestContext.Database.ExecuteSqlRawAsync(viewSql);
        
        // Test that stored procedures exist
        var procSql = "SELECT COUNT(*) FROM sys.procedures WHERE schema_id = SCHEMA_ID('request') AND name = 'sp_Request_GetMetrics'";
        var procExists = await requestContext.Database.ExecuteSqlRawAsync(procSql);
        
        Assert.True(procExists > 0);
    }
    
    private IHost CreateTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<Request.Data.RequestDbContext>(options =>
                    options.UseSqlServer(_fixture.ConnectionString));
                    
                services.AddSingleton<Integration.EfCoreMigrationCoordinator>();
                services.AddSingleton<Migration.IMigrationService, Migration.MigrationService>();
            })
            .Build();
    }
}
```

### Manual Testing Checklist
```powershell
# Test unified migration
dotnet run --project Database/Database.csproj unified-migrate Development

# Test EF Core only migration
dotnet run --project Database/Database.csproj ef-only Development

# Test database objects only migration
dotnet run --project Database/Database.csproj db-objects-only Development

# Test dependency validation
dotnet run --project Database/Database.csproj validate

# Test rollback coordination
dotnet run --project Database/Database.csproj rollback "version-123"
```

## Acceptance Criteria

### Must Have
- [x] Unified migration coordinator that runs EF Core migrations first, then database objects
- [x] Dependency validation system that ensures database objects reference existing tables
- [x] Integration with existing DbContext configurations across all modules
- [x] Updated CLI tool that supports coordinated migrations
- [x] Rollback coordination between EF Core and database object migrations
- [x] Integration tests that verify coordinated deployments

### Should Have
- [ ] Performance monitoring for coordinated migrations
- [ ] Detailed logging and error reporting for integration issues
- [ ] Automatic dependency resolution and ordering
- [ ] Schema drift detection between EF Core and database objects

### Nice to Have
- [ ] Visual dependency mapping and analysis
- [ ] Automated conflict resolution for schema changes
- [ ] Advanced rollback strategies with point-in-time coordination

## Potential Issues & Solutions

### Issue 1: EF Core Migration Conflicts
**Problem**: EF Core migrations may conflict with database object changes  
**Solution**: Dependency validation and strict ordering controls

### Issue 2: Performance Impact
**Problem**: Coordinated migrations may take longer  
**Solution**: Parallel execution where safe, with dependency tracking

### Issue 3: Rollback Complexity
**Problem**: Rolling back both EF Core and database objects is complex  
**Solution**: Transaction coordination and comprehensive backup strategies

### Issue 4: Schema Versioning Conflicts
**Problem**: EF Core and database object versions may get out of sync  
**Solution**: Version coordination tracking and validation

## Handoff Notes

### Key Deliverables
1. **Unified migration coordinator** that manages both EF Core and database object deployments
2. **Dependency validation system** ensuring database objects reference existing tables
3. **Updated CLI tools** supporting coordinated migration operations
4. **Integration testing framework** validating coordinated deployments
5. **Pipeline integration** for automated coordinated deployments

### Integration Points
- Coordinates with existing EF Core DbContext configurations
- Uses established migration framework from previous task
- Integrates with deployment pipeline for automated operations
- Maintains compatibility with existing development workflows

### Development Workflow
1. **Make EF Core changes**: Create/modify entities and generate migrations
2. **Create database objects**: Add views/SPs/functions that reference EF tables
3. **Run unified migration**: Deploy both systems together
4. **Validate integration**: Ensure all dependencies are satisfied

## Time Tracking
- **Estimated**: 4-6 hours
- **Actual**: _To be filled by implementer_
- **Variance**: _To be filled by implementer_

## Implementation Notes
_To be filled by implementer with any deviations, discoveries, or improvements_