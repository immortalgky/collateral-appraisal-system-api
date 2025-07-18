using Elsa.Extensions;
using FastEndpoints;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Task;
using Workflow.Data;

//using Workflow.Workflow.AppraisalSagaState;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Common services: carter, mediatR, fluentvalidators, etc.
var requestAssembly = typeof(RequestModule).Assembly;
var authAssembly = typeof(AuthModule).Assembly;
var notificationAssembly = typeof(NotificationModule).Assembly;
var workflowAssembly = typeof(WorkflowModule).Assembly;
var taskAssembly = typeof(TaskModule).Assembly;

builder.Services.AddCarterWithAssemblies(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);
builder.Services.AddMediatRWithAssemblies(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// builder.Services.AddMassTransitWithAssemblies(builder.Configuration, requestAssembly, authAssembly,
//     notificationAssembly);

// builder.Services.AddDbContext<AppraisalSagaDbContext>((sp, options) =>
// {
//     options.UseSqlServer(builder.Configuration.GetConnectionString("Database"), sqlOptions =>
//     {
//         sqlOptions.MigrationsAssembly(typeof(AppraisalSagaDbContext).Assembly.GetName().Name);
//         sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
//         sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
//     });
// });

builder.Services.AddMassTransit(config =>
{
    config.SetKebabCaseEndpointNameFormatter();

    // config.AddSagaStateMachine<AppraisalStateMachine, AppraisalSagaState>()
    //     .EntityFrameworkRepository(r =>
    //     {
    //         r.ConcurrencyMode = ConcurrencyMode.Pessimistic; // Safer for SQL Server
    //         r.ExistingDbContext<AppraisalSagaDbContext>();
    //         r.LockStatementProvider = new SqlServerLockStatementProvider();
    //     });

    config.AddConsumers(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);
    config.AddSagaStateMachines(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);
    config.AddSagas(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);
    config.AddActivities(requestAssembly, authAssembly, notificationAssembly, workflowAssembly, taskAssembly);

    config.UsingRabbitMq((context, configurator) =>
    {
        configurator.Host(new Uri(builder.Configuration["RabbitMQ:Host"]!), host =>
        {
            host.Username(builder.Configuration["RabbitMQ:Username"]!);
            host.Password(builder.Configuration["RabbitMQ:Password"]!);

            configurator.ConfigureEndpoints(context);
        });
    });
});


builder.Services.AddHttpClient("CAS", client => { client.BaseAddress = new Uri("https://localhost:7111"); });

builder.Services.AddAuthorization();

// Module services: request, etc.
builder.Services
    .AddRequestModule(builder.Configuration)
    .AddAuthModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddWorkflowModule(builder.Configuration)
    .AddTaskModule(builder.Configuration)
    .AddOpenIddictModule(builder.Configuration);

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SPAPolicy",
        policy =>
        {
            policy.WithOrigins("https://localhost:3000", "https://localhost:7111")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Optional if you need cookies/auth headers
        });

    // Add CORS policy for Elsa Studio
    options.AddPolicy("ElsaPolicy",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "Assets")),
    RequestPath = "/Assets"
});

app.UseCors("SPAPolicy");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapCarter();

app
    .UseRequestModule()
    .UseAuthModule()
    .UseNotificationModule()
    .UseWorkflowModule()
    .UseTaskModule()
    .UseOpenIddictModule();

app.UseSerilogRequestLogging();
app.UseExceptionHandler(options => { });

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

await app.RunAsync();