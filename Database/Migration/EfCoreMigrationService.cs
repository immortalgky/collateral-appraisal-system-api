using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Request.Data;
using Workflow.Data;
using Document.Data;
using Notification.Data;
using OAuth2OpenId.Data;
using OAuth2OpenId;
using OAuth2OpenId.Identity.Models;

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
        {
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
                return false;
            }
        }

        return true;
    }

    private Type[] GetDbContextTypes()
    {
        // Get all registered DbContext types
        return new[]
        {
            typeof(RequestDbContext),
            typeof(WorkflowDbContext),
            typeof(AppraisalSagaDbContext),
            typeof(DocumentDbContext),
            typeof(NotificationDbContext),
            typeof(OpenIddictDbContext)
        };
    }

    private DbContext CreateContextWithConnectionString(Type contextType, string connectionString)
    {
        return contextType.Name switch
        {
            nameof(RequestDbContext) => CreateRequestDbContext(connectionString),
            nameof(WorkflowDbContext) => CreateWorkflowDbContext(connectionString),
            nameof(AppraisalSagaDbContext) => CreateAppraisalSagaDbContext(connectionString),
            nameof(DocumentDbContext) => CreateDocumentDbContext(connectionString),
            nameof(NotificationDbContext) => CreateNotificationDbContext(connectionString),
            nameof(OpenIddictDbContext) => CreateOpenIddictDbContext(connectionString),
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

    private AppraisalSagaDbContext CreateAppraisalSagaDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppraisalSagaDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(AppraisalSagaDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
        });
        return new AppraisalSagaDbContext(optionsBuilder.Options);
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

    private OpenIddictDbContext CreateOpenIddictDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenIddictDbContext>();
        //optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(OpenIddictDbContext).Assembly.GetName().Name);
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
        });
        optionsBuilder.UseOpenIddict();
        return new OpenIddictDbContext(optionsBuilder.Options);
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