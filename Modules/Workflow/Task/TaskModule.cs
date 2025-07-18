namespace Task;

public static class TaskModule
{
    public static IServiceCollection AddTaskModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITaskRepository, TaskRepository>();


        services.AddDbContext<TaskDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        return services;
    }

    public static IApplicationBuilder UseTaskModule(this IApplicationBuilder app)
    {
        app.UseMigration<TaskDbContext>();

        return app;
    }
}