using Shared.Data.Outbox;
using Workflow.Domain;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;
using Workflow.Meetings.ReadModels;
using Workflow.Workflow.Models;
using Workflow.Data.Entities;
using Workflow.Sla.Models;
using Workflow.DocumentFollowups.Domain;

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
    
    // Task assignment configuration
    public DbSet<TaskAssignmentConfiguration> TaskAssignmentConfigurations => Set<TaskAssignmentConfiguration>();

    // Activity process configuration (submission pipeline)
    public DbSet<ActivityProcessConfiguration> ActivityProcessConfigurations => Set<ActivityProcessConfiguration>();

    // Workflow support entities
    public DbSet<WorkflowOutbox> WorkflowOutboxes => Set<WorkflowOutbox>();
    public DbSet<WorkflowBookmark> WorkflowBookmarks => Set<WorkflowBookmark>();
    public DbSet<WorkflowExecutionLog> WorkflowExecutionLogs => Set<WorkflowExecutionLog>();
    public DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions => Set<WorkflowDefinitionVersion>();
    public DbSet<WorkflowExternalCall> WorkflowExternalCalls => Set<WorkflowExternalCall>();

    // Committee and approval entities
    public DbSet<Committee> Committees => Set<Committee>();
    public DbSet<ApprovalVote> ApprovalVotes => Set<ApprovalVote>();

    // SLA entities
    public DbSet<SlaConfiguration> SlaConfigurations => Set<SlaConfiguration>();
    public DbSet<WorkflowSlaConfiguration> WorkflowSlaConfigurations => Set<WorkflowSlaConfiguration>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<BusinessHoursConfig> BusinessHoursConfigs => Set<BusinessHoursConfig>();
    public DbSet<SlaBreachLog> SlaBreachLogs => Set<SlaBreachLog>();

    // Document followup entities
    public DbSet<DocumentFollowup> DocumentFollowups => Set<DocumentFollowup>();

    // Meeting entities
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingItem> MeetingItems => Set<MeetingItem>();
    public DbSet<MeetingQueueItem> MeetingQueueItems => Set<MeetingQueueItem>();
    public DbSet<MeetingMember> MeetingMembers => Set<MeetingMember>();
    public DbSet<AppraisalAcknowledgementQueueItem> AppraisalAcknowledgementQueueItems => Set<AppraisalAcknowledgementQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("workflow");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Integration event outbox for reliable messaging
        modelBuilder.AddIntegrationEventOutbox();

        base.OnModelCreating(modelBuilder);
    }
}