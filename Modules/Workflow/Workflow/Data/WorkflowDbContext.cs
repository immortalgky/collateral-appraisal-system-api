using Workflow.Workflow.Models;
using Workflow.Data.Entities;

namespace Workflow.Data;

public class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<PendingTask> PendingTasks => Set<PendingTask>();
    public DbSet<CompletedTask> CompletedTasks => Set<CompletedTask>();
    public DbSet<RoundRobinQueue> RoundRobinQueue => Set<RoundRobinQueue>();
    
    // Workflow entities
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowActivityExecution> WorkflowActivityExecutions => Set<WorkflowActivityExecution>();
    public DbSet<WorkflowBookmark> WorkflowBookmarks => Set<WorkflowBookmark>();
    public DbSet<WorkflowExecutionLog> WorkflowExecutionLogs => Set<WorkflowExecutionLog>();
    public DbSet<WorkflowOutbox> WorkflowOutboxes => Set<WorkflowOutbox>();
    public DbSet<WorkflowExternalCall> WorkflowExternalCalls => Set<WorkflowExternalCall>();
    
    // Task assignment configuration
    public DbSet<TaskAssignmentConfiguration> TaskAssignmentConfigurations => Set<TaskAssignmentConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workflow");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}