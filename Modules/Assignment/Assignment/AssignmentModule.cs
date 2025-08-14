using Assignment.Workflow.Repositories;
using Assignment.Workflow.Activities.Factories;
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
        services.AddScoped<IAssigneeSelectorFactory, AssigneeSelectorFactory>();

        // Workflow services
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
        services.AddScoped<IWorkflowActivityFactory, WorkflowActivityFactory>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IWorkflowNotificationService, WorkflowNotificationService>();

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