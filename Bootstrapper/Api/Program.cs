using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Add shared services (time abstraction, security, etc.)
builder.Services.AddSharedServices(builder.Configuration);

// Common services: carter, mediatR, fluentvalidators, etc.
var requestAssembly = typeof(RequestModule).Assembly;
var authAssembly = typeof(AuthModule).Assembly;
var notificationAssembly = typeof(NotificationModule).Assembly;
var documentAssembly = typeof(DocumentModule).Assembly;
var assignmentAssembly = typeof(AssignmentModule).Assembly;

builder.Services.AddCarterWithAssemblies(requestAssembly, authAssembly, notificationAssembly, documentAssembly, assignmentAssembly);
builder.Services.AddMediatRWithAssemblies(requestAssembly, authAssembly, notificationAssembly, documentAssembly, assignmentAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// builder.Services.AddMassTransitWithAssemblies(builder.Configuration, requestAssembly, authAssembly,
//     notificationAssembly);

builder.Services.AddScoped<ISqlConnectionFactory>(provider =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("Database")!));

builder.Services.AddDbContext<AppraisalSagaDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database"), sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(AppraisalSagaDbContext).Assembly.GetName().Name);
        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "saga");
        sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
    });
});

// Module services: register modules BEFORE MassTransit so consumer wrappers are registered in DI
builder.Services
    .AddRequestModule(builder.Configuration)
    .AddAuthModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddDocumentModule(builder.Configuration)
    .AddAssignmentModule(builder.Configuration)
    .AddOpenIddictModule(builder.Configuration);

builder.Services.AddMassTransitWithAssemblies(builder.Configuration,
    requestAssembly, authAssembly, notificationAssembly, assignmentAssembly);

builder.Services.AddHttpClient("CAS", client => { client.BaseAddress = new Uri("https://localhost:7111"); });

builder.Services.AddAuthorization();

// Module services: request, etc.
// builder.Services
//     .AddRequestModule(builder.Configuration)
//     .AddAuthModule(builder.Configuration)
//     .AddNotificationModule(builder.Configuration)
//     .AddDocumentModule(builder.Configuration)
//     .AddAssignmentModule(builder.Configuration)
//     .AddOpenIddictModule(builder.Configuration);

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
            policy
                .WithOrigins("https://localhost:3000", "https://localhost:7111", "null")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for SignalR
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) app.MapOpenApi();
//if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(AppContext.BaseDirectory, "Assets")),
    RequestPath = "/Assets"
});

app.UseCors("SPAPolicy");
app.UseSerilogRequestLogging();
app.UseExceptionHandler(options => { });

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
    .UseDocumentModule()
    .UseAssignmentModule()
    .UseOpenIddictModule();

await app.RunAsync();

public partial class Program { }