using Shared.Data.Seed;
using Workflow.AssigneeSelection.Engine;
using Workflow.AssigneeSelection.Configuration;
using Workflow.AssigneeSelection.Pipeline;
using Workflow.AssigneeSelection.Services;
using Workflow.Workflow.Activities;
using Workflow.Workflow.Activities.Approval;
using Workflow.AssigneeSelection.Teams;
using Workflow.Domain.Committees;
using Workflow.Services.Configuration;
using Workflow.Workflow.Repositories;
using Workflow.Workflow.Activities.Factories;
using Workflow.Workflow.Actions.Core;
using Workflow.Workflow.Engine;
using Workflow.Workflow.Engine.Expression;
using Workflow.Workflow.Pipeline;
using Workflow.Workflow.Pipeline.Steps;
using Workflow.Workflow.Services;
using Workflow.Infrastructure;
using Workflow.Sla.Services;
using Workflow.Workflow.Hubs;

namespace Workflow;

public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkflowUnitOfWork, WorkflowUnitOfWork>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();

        // User group and hashing services
        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IGroupHashService, GroupHashService>();

        // Assignee selector services
        services.AddScoped<ManualAssigneeSelector>();
        services.AddScoped<RoundRobinAssigneeSelector>();
        services.AddScoped<WorkloadBasedAssigneeSelector>();
        services.AddScoped<RandomAssigneeSelector>();
        services.AddScoped<SupervisorAssigneeSelector>();
        services.AddScoped<PreviousOwnerAssigneeSelector>();
        services.AddScoped<StartedByAssigneeSelector>();
        services.AddScoped<PoolAssigneeSelector>();
        services.AddScoped<IAssigneeSelectorFactory, AssigneeSelectorFactory>();
        services.AddScoped<TeamConstrainedAssigneeSelector>();
        services.AddScoped<VariableAssigneeSelector>();
        services.AddScoped<ICascadingAssignmentEngine, CascadingAssignmentEngine>();

        // Team service — queries auth schema (Company = Team, Role = ActivityRole)
        services.AddScoped<ITeamService, CompanyTeamService>();

        // Assignment pipeline — 5-stage orchestrator
        services.AddScoped<IAssignmentContextBuilder, AssignmentContextBuilder>();
        services.AddScoped<IAssignmentFilter, TeamFilter>();
        services.AddScoped<IAssignmentFilter, ExclusionFilter>();
        services.AddScoped<IAssignmentFilter, ActivityRoleFilter>();
        services.AddScoped<IAssignmentValidator, TeamMembershipValidator>();
        services.AddScoped<IAssignmentValidator, ExclusionRuleValidator>();
        services.AddScoped<IAssignmentFinalizer, AssignmentFinalizer>();
        services.AddScoped<IAssignmentPipeline, AssignmentPipeline>();

        // Custom assignment services - extensible assignment logic
        services.AddCustomAssignmentServices()
            .AddService<ClientPreferenceAssignmentService>()
            .AddService<BusinessRulesAssignmentService>()
            .Build();

        // Mock supervisor configuration (remove when UserManagement is implemented)
        services.Configure<MockSupervisorOptions>(configuration.GetSection(MockSupervisorOptions.SectionName));
        services.PostConfigure<MockSupervisorOptions>(options => options.Validate());

        // Workflow services - new clean architecture
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowActivityExecutionRepository, WorkflowActivityExecutionRepository>();
        services.AddScoped<IWorkflowActivityFactory, WorkflowActivityFactory>();

        // Version repository
        services.AddScoped<IWorkflowDefinitionVersionRepository, WorkflowDefinitionVersionRepository>();

        // Additional repositories
        services.AddScoped<IWorkflowOutboxRepository, WorkflowOutboxRepository>();
        services.AddScoped<IWorkflowBookmarkRepository, WorkflowBookmarkRepository>();
        services.AddScoped<IWorkflowExecutionLogRepository, WorkflowExecutionLogRepository>();

        // Bookmark and fault handling services
        services.AddScoped<IWorkflowBookmarkService, WorkflowBookmarkService>();
        services.AddScoped<IWorkflowFaultHandler, WorkflowFaultHandler>();

        // NEW ARCHITECTURE: Specialized service components
        services.AddScoped<IFlowControlManager, FlowControlManager>();
        services.AddScoped<IWorkflowLifecycleManager, WorkflowLifecycleManager>();
        services.AddScoped<IWorkflowPersistenceService, WorkflowPersistenceService>();
        services.AddScoped<IWorkflowEventPublisher, WorkflowEventPublisher>();
        services.AddScoped<IWorkflowStateManager, WorkflowStateManager>();

        // Core execution engine - orchestration responsibilities
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();

        // Service layer - thin coordination layer 
        services.AddScoped<IWorkflowService, WorkflowService>();

        services.AddScoped<IWorkflowNotificationService, WorkflowNotificationService>();
        services.AddScoped<IWorkflowAuditService, WorkflowAuditService>();
        services.AddScoped<IWorkflowResilienceService, WorkflowResilienceService>();
        services.AddScoped<ITaskConfigurationService, TaskConfigurationService>();

        // Workflow expression evaluator and action executor
        services.AddScoped<IExpressionEvaluator, ExpressionEvaluator>();
        services.AddScoped<IWorkflowExpressionEvaluator, WorkflowExpressionEvaluator>();
        services.AddScoped<IWorkflowActionExecutor, WorkflowActionExecutor>();

        // Workflow activities
        services.AddScoped<TaskActivity>();
        services.AddScoped<RoutingActivity>();
        services.AddScoped<CompanySelectionActivity>();
        services.AddScoped<InternalFollowupSelectionActivity>();
        services.AddScoped<ApprovalActivity>();

        // Approval infrastructure
        services.AddScoped<IApprovalMemberResolver, ApprovalMemberResolver>();
        services.AddScoped<IApprovalVoteRepository, ApprovalVoteRepository>();
        services.AddScoped<ICommitteeRepository, CommitteeRepository>();

        // Company routing
        services.AddScoped<ICompanyRoundRobinService, CompanyRoundRobinService>();

        // Internal staff followup routing
        services.AddScoped<IInternalStaffRoundRobinService, InternalStaffRoundRobinService>();

        // SLA services
        services.AddScoped<IBusinessTimeCalculator, BusinessTimeCalculator>();
        services.AddScoped<ISlaCalculator, SlaCalculator>();
        services.AddHostedService<SlaMonitorService>();

        // Activity process pipeline (submission pipeline)
        services.AddScoped<IActivityProcessStep, UpdateAppraisalStatusStep>();
        services.AddScoped<IActivityProcessStep, UpdateAssignmentStatusStep>();
        services.AddScoped<IActivityProcessStep, ValidateHasAppraisedValueStep>();
        services.AddScoped<IActivityProcessStep, ValidateTaskOwnershipStep>();
        services.AddScoped<IActivityProcessStep, ValidateDecisionConstraintsStep>();
        services.AddScoped<ProcessStepResolver>();
        services.AddScoped<IActivityProcessPipeline, ActivityProcessPipeline>();

        // Data seeders
        services.AddScoped<IDataSeeder<WorkflowDbContext>, Data.Seed.ActivityProcessConfigurationSeeder>();
        services.AddScoped<IDataSeeder<WorkflowDbContext>, Data.Seed.CommitteeDataSeed>();

        // Workflow DbContext with its own migration assembly and history table
        services.AddDbContext<WorkflowDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure();
                sqlOptions.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.GetName()
                    .Name); // Workflow assembly
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "workflow");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseWorkflowModule(this IApplicationBuilder app)
    {
        app.UseMigration<WorkflowDbContext>();

        // Configure SignalR workflow hub
        app.UseEndpoints(endpoints => { endpoints.MapHub<WorkflowHub>("/workflowHub"); });

        return app;
    }
}