using Assignment.AssigneeSelection.Engine;
using Assignment.AssigneeSelection.Configuration;
using Assignment.AssigneeSelection.Strategies;
using Assignment.AssigneeSelection.Services;
using Assignment.Services.Configuration;
using Assignment.Workflow.Repositories;
using Assignment.Workflow.Activities.Factories;
using Assignment.Workflow.Activities;
using Assignment.Workflow.Engine;
using Assignment.Workflow.Services;

namespace Assignment;

public static class AssignmentModule
{
    public static IServiceCollection AddAssignmentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IAssignmentService, AssignmentService>();

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
        services.AddScoped<IAssigneeSelectorFactory, AssigneeSelectorFactory>();
        services.AddScoped<ICascadingAssignmentEngine, CascadingAssignmentEngine>();

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
        
        // NEW ARCHITECTURE: Specialized service components
        services.AddScoped<IFlowControlManager, FlowControlManager>();
        services.AddScoped<IWorkflowLifecycleManager, WorkflowLifecycleManager>();
        services.AddScoped<IWorkflowPersistenceService, WorkflowPersistenceService>();
        services.AddScoped<IWorkflowEventPublisher, WorkflowEventPublisher>();
        
        // Core execution engine - orchestration responsibilities
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        
        // Service layer - thin coordination layer 
        services.AddScoped<IWorkflowService, WorkflowService>();
        
        services.AddScoped<IWorkflowNotificationService, WorkflowNotificationService>();
        services.AddScoped<ITaskConfigurationService, TaskConfigurationService>();

        // Workflow activities
        services.AddScoped<TaskActivity>();

        // Assignment DbContext with its own migration assembly and history table
        services.AddDbContext<AssignmentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AssignmentDbContext).Assembly.GetName()
                    .Name); // Assignment assembly
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "assignment");
            });
        });

        // Saga DbContext with separate migration assembly and history table
        services.AddDbContext<AppraisalSagaDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppraisalSagaDbContext).Assembly.GetName()
                    .Name); // Separate saga assembly
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseAssignmentModule(this IApplicationBuilder app)
    {
        app.UseMigration<AssignmentDbContext>();
        app.UseMigration<AppraisalSagaDbContext>();

        // Configure SignalR workflow hub
        app.UseEndpoints(endpoints => { endpoints.MapHub<Assignment.Workflow.Hubs.WorkflowHub>("/workflowHub"); });

        return app;
    }
}