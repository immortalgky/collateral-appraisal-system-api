using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Common services: carter, mediatR, fluentvalidators, etc.
var requestAssembly = typeof(RequestModule).Assembly;
var authAssembly = typeof(AuthModule).Assembly;
var notificationAssembly = typeof(NotificationModule).Assembly;
var documentAssembly = typeof(DocumentModule).Assembly;

builder.Services.AddCarterWithAssemblies(requestAssembly, authAssembly, notificationAssembly, documentAssembly);
builder.Services.AddMediatRWithAssemblies(requestAssembly, authAssembly, notificationAssembly, documentAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddMassTransitWithAssemblies(builder.Configuration, requestAssembly, authAssembly,
    notificationAssembly);

builder.Services.AddHttpClient("CAS", client => { client.BaseAddress = new Uri("https://localhost:7111"); });

builder.Services.AddAuthorization();

// Module services: request, etc.
builder.Services
    .AddRequestModule(builder.Configuration)
    .AddAuthModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddOpenIddictModule(builder.Configuration)
    .AddDocumentModule(builder.Configuration);

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
            policy.WithOrigins("https://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Optional if you need cookies/auth headers
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

app.UseSerilogRequestLogging();
app.UseExceptionHandler(options => { });

app
    .UseRequestModule()
    .UseAuthModule()
    .UseNotificationModule()
    .UseOpenIddictModule()
    .UseDocumentModule();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

await app.RunAsync();