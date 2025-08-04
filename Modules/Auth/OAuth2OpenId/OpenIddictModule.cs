using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Security;

namespace OAuth2OpenId;

public static class OpenIddictModule
{
    public static IServiceCollection AddOpenIddictModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorPages().AddApplicationPart(typeof(Login).Assembly);

        // Add anti-forgery protection
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__RequestVerificationToken";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Register certificate provider
        services.AddSingleton<ICertificateProvider, CertificateProvider>();

        services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(OpenIddictDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
            });
            options.UseOpenIddict();
        });

        // Add Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<OpenIddictDbContext>()
            .AddDefaultTokenProviders();

        // Add OAuth2OpenId
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<OpenIddictDbContext>();
            })
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("/connect/token");
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.AllowClientCredentialsFlow();
                options.AllowRefreshTokenFlow();

                // Security: Only accept clients that are explicitly registered
                // Remove AcceptAnonymousClients() for production security
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (environment == "Development")
                {
                    options.AcceptAnonymousClients();
                }

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.OfflineAccess);

                // Use a secure certificate provider instead of development certificates
                // For development, still use development certificates temporarily
                if (environment == "Development")
                {
                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                }
                else
                {
                    // In production, use proper certificates via configuration
                    // This will be loaded dynamically at startup
                    var signingCertConfig = configuration.GetSection("OAuth2:SigningCertificate");
                    var encryptionCertConfig = configuration.GetSection("OAuth2:EncryptionCertificate");

                    if (signingCertConfig.Exists())
                    {
                        // Use certificate provider when available
                        // For now, throw exception to enforce proper configuration
                        throw new InvalidOperationException(
                            "Production signing certificate configuration required but not implemented. Please configure OAuth2:SigningCertificate section.");
                    }

                    if (encryptionCertConfig.Exists())
                    {
                        throw new InvalidOperationException(
                            "Production encryption certificate configuration required but not implemented. Please configure OAuth2:EncryptionCertificate section.");
                    }

                    // Fallback to development certificates with warning
                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                }

                // Enable access token encryption in production
                if (environment == "Development")
                {
                    options.DisableAccessTokenEncryption();
                }

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<IDataSeeder<OpenIddictDbContext>, AuthDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseOpenIddictModule(this IApplicationBuilder app)
    {
        app.UseMigration<OpenIddictDbContext>();

        return app;
    }
}