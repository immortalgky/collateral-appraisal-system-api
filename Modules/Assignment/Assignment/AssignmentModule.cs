<<<<<<< HEAD
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
// using Assignment.Configurations;
using Assignment.Data.Repository;
using Shared.Data.Extensions;
using Shared.Data.Interceptors;

=======
>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666
namespace Assignment;

public static class AssignmentModule
{
    public static IServiceCollection AddAssignmentModule(this IServiceCollection services, IConfiguration configuration)
    {
<<<<<<< HEAD
        // Configure Mapster mappings
        // MappingConfiguration.ConfigureMappings();

        // Application User Case services
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();

        // Infrastructure services
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<AssignmentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        // services.AddScoped<IDataSeeder<RequestDbContext>, RequestDataSeed>();
=======
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
>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666

        return services;
    }

    public static IApplicationBuilder UseAssignmentModule(this IApplicationBuilder app)
    {
        app.UseMigration<AssignmentDbContext>();
<<<<<<< HEAD
=======
        app.UseMigration<AppraisalSagaDbContext>();
>>>>>>> 3434ca92f42e614d268511d3a6e0d95cb6f4d666

        return app;
    }
}