using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Request.Infrastructure;
using Workflow.Data;
using Document.Data;
using Notification.Data;
using Auth.Infrastructure;
using Auth;
using Auth.Domain.Identity;
using Appraisal.Infrastructure;
using Collateral.Data;
using Common.Infrastructure;
using Parameter.Data;
using Integration.Infrastructure;

namespace Database.Migration;

public class EfCoreMigrationService : IEfCoreMigrationService
{
    private readonly ILogger<EfCoreMigrationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public EfCoreMigrationService(ILogger<EfCoreMigrationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> MigrateAsync(string environment = "Development")
    {
        return await MigrateAsync(null, environment);
    }

    public async Task<bool> MigrateAsync(string? connectionString, string environment = "Development")
    {
        var contextTypes = GetDbContextTypes();

        foreach (var contextType in contextTypes)
            try
            {
                _logger.LogInformation("Running EF Core migrations for: {ContextType}", contextType.Name);

                if (!string.IsNullOrEmpty(connectionString))
                {
                    // Create context with specific connection string for testing
                    var context = CreateContextWithConnectionString(contextType, connectionString);
                    using (context)
                    {
                        await ProcessMigrations(context, contextType);
                    }
                }
                else
                {
                    // Use registered context from DI container - scope will manage disposal
                    using var scope = _serviceProvider.CreateScope();
                    var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                    await ProcessMigrations(context, contextType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EF Core migration failed for: {ContextType}", contextType.Name);
                throw new InvalidOperationException($"EF Core migration failed for {contextType.Name}: {ex.Message}", ex);
            }

        return true;
    }

    private Type[] GetDbContextTypes()
    {
        // Order matters: modules that are depended upon by migrations in other modules must run first.
        // CommonDbContext creates common.RequestStatusSummaries used by a Request migration.
        // ParameterDbContext creates parameter tables referenced by Appraisal.
        return new[]
        {
            typeof(CommonDbContext),
            typeof(ParameterDbContext),
            typeof(RequestDbContext),
            typeof(WorkflowDbContext),
            typeof(DocumentDbContext),
            typeof(NotificationDbContext),
            typeof(AuthDbContext),
            typeof(IntegrationDbContext),
            typeof(AppraisalDbContext),
            typeof(CollateralDbContext)
        };
    }

    private DbContext CreateContextWithConnectionString(Type contextType, string connectionString)
    {
        return contextType.Name switch
        {
            nameof(CommonDbContext) => CreateCommonDbContext(connectionString),
            nameof(ParameterDbContext) => CreateParameterDbContext(connectionString),
            nameof(RequestDbContext) => CreateRequestDbContext(connectionString),
            nameof(WorkflowDbContext) => CreateWorkflowDbContext(connectionString),
            nameof(DocumentDbContext) => CreateDocumentDbContext(connectionString),
            nameof(NotificationDbContext) => CreateNotificationDbContext(connectionString),
            nameof(AuthDbContext) => CreateAuthDbContext(connectionString),
            nameof(IntegrationDbContext) => CreateIntegrationDbContext(connectionString),
            nameof(AppraisalDbContext) => CreateAppraisalDbContext(connectionString),
            nameof(CollateralDbContext) => CreateCollateralDbContext(connectionString),
            _ => throw new ArgumentException($"Unknown context type: {contextType.Name}")
        };
    }

    private RequestDbContext CreateRequestDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RequestDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(RequestDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "request");
        });
        return new RequestDbContext(optionsBuilder.Options);
    }

    private WorkflowDbContext CreateWorkflowDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WorkflowDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "workflow");
        });
        return new WorkflowDbContext(optionsBuilder.Options);
    }

    private DocumentDbContext CreateDocumentDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(DocumentDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "document");
        });
        return new DocumentDbContext(optionsBuilder.Options);
    }

    private NotificationDbContext CreateNotificationDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "notification");
        });
        return new NotificationDbContext(optionsBuilder.Options);
    }

    private AuthDbContext CreateAuthDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AuthDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
        });
        optionsBuilder.UseOpenIddict();
        return new AuthDbContext(optionsBuilder.Options);
    }

    private CommonDbContext CreateCommonDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CommonDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(CommonDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "common");
        });
        return new CommonDbContext(optionsBuilder.Options);
    }

    private ParameterDbContext CreateParameterDbContext(string connectionString)
    {
        // ParameterModule registers ParameterDbContext with UseSqlServer only (no custom
        // history table), so migrations are tracked in the default dbo.__EFMigrationsHistory.
        // This factory must match that configuration exactly so the same history table is used.
        var optionsBuilder = new DbContextOptionsBuilder<ParameterDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ParameterDbContext(optionsBuilder.Options);
    }

    private IntegrationDbContext CreateIntegrationDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(IntegrationDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "integration");
        });
        return new IntegrationDbContext(optionsBuilder.Options);
    }

    private AppraisalDbContext CreateAppraisalDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppraisalDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AppraisalDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "appraisal");
        });
        return new AppraisalDbContext(optionsBuilder.Options);
    }

    private CollateralDbContext CreateCollateralDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CollateralDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(CollateralDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "collateral");
        });
        return new CollateralDbContext(optionsBuilder.Options);
    }

    private async Task ProcessMigrations(DbContext context, Type contextType)
    {
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
}