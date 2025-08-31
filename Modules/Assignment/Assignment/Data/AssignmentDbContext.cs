using Assignment.Workflow.Models;
using Assignment.Data.Entities;

namespace Assignment.Data;

public class AssignmentDbContext(DbContextOptions<AssignmentDbContext> options) : DbContext(options)
{
    public DbSet<PendingTask> PendingTasks => Set<PendingTask>();
    public DbSet<CompletedTask> CompletedTasks => Set<CompletedTask>();
    public DbSet<RoundRobinQueue> RoundRobinQueue => Set<RoundRobinQueue>();
    
    // Workflow entities
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowActivityExecution> WorkflowActivityExecutions => Set<WorkflowActivityExecution>();
    
    // Task assignment configuration
    public DbSet<TaskAssignmentConfiguration> TaskAssignmentConfigurations => Set<TaskAssignmentConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assignment");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}