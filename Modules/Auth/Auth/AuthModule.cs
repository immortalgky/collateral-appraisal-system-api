using Auth.Application.Configurations;
using Auth.Domain.Companies;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Identity;
using Auth.Infrastructure.Repository;
using Auth.Infrastructure.Seed;
using Auth.Infrastructure.Session;
using Auth.Application.Services;
using Auth.Contracts.Companies;
using Auth.Contracts.Users;
using Auth.Scheduling;
using Fido2NetLib;
using Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenIddict.Validation.AspNetCore;
using Shared.Identity;
using Shared.Scheduling;
using Shared.Security;
using Shared.Time;

namespace Auth;

public static class AuthModule
{
    /// <summary>
    /// Whether the X-Dev-Auth bypass is accepted: every environment except Production. An unset/empty
    /// ASPNETCORE_ENVIRONMENT counts as Production so a misconfigured server fails closed.
    /// </summary>
    private static bool AllowDevBypass
    {
        get
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return !string.IsNullOrEmpty(environment)
                   && !environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Razor Pages for login UI
        services.AddRazorPages().AddApplicationPart(typeof(Login).Assembly);

        // Anti-forgery protection
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__RequestVerificationToken";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Certificate provider
        services.AddSingleton<ICertificateProvider, CertificateProvider>();

        // DbContext
        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AuthDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
            });
            options.UseOpenIddict();
        });

        // ASP.NET Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Complexity rules are enforced by DbPasswordValidator from the DB-maintained policy
                // (so admin edits apply without a restart). Relax the built-in checks so they don't
                // double-enforce a stale, hardcoded rule set.
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequiredLength = 1;
                options.Password.RequiredUniqueChars = 0;

                // Lockout is fully DB-maintained: these are only first-boot fallbacks (used until the
                // policy row exists). UseAuthModule's ApplyLockoutPolicy overrides all of them from the
                // DB policy at startup (Identity reads lockout options as a snapshot).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromDays(365 * 200);
                options.Lockout.AllowedForNewUsers = true;

                // Reject duplicate emails. Without this the default UserValidator skips the email
                // uniqueness check, so create/update would silently allow two accounts to share an
                // email (there is no unique index on NormalizedEmail either).
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddPasswordValidator<DbPasswordValidator>()
            .AddDefaultTokenProviders();

        // Browser sessions (BFF). The SPA holds only the app cookie; the IdP keeps its own SSO cookie.
        var oidcClient = configuration.GetSection(OidcClientOptions.SectionName).Get<OidcClientOptions>()
                         ?? new OidcClientOptions();
        var urls = new BffUrls(
            configuration["AppBaseUrl"] ?? "",
            configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [],
            configuration["Auth:Idp:PublicBaseUrl"]);
        services.AddSingleton(urls);

        // The cookie names carry a suffix derived from the AppBaseUrl port unless one is configured; resolving here
        // also fails the start on an invalid one. Runtime consumers get the same resolved options.
        var sessionOptions = (configuration.GetSection(BrowserSessionOptions.SectionName).Get<BrowserSessionOptions>()
                              ?? new BrowserSessionOptions()).Resolve(urls.PublicBase);
        services.Configure<BrowserSessionOptions>(configuration.GetSection(BrowserSessionOptions.SectionName));
        services.PostConfigure<BrowserSessionOptions>(options => options.Resolve(urls.PublicBase));

        // The IdP (SSO) cookie guards only the auth pages and /connect/*; no API endpoint accepts it.
        // Renamed so it cannot collide with the app cookie, with a per-environment suffix (see
        // BrowserSessionOptions.CookieSuffix). Point it at pages that actually exist: the default
        // AccessDeniedPath had no page, so any cookie-auth forbid returned a bare 404.
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.Name = sessionOptions.IdpCookieName;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        // Identity's two-factor cookies ship with fixed names ("Identity.TwoFactorUserId" / "...RememberMe"), which would
        // collide between environments sharing a host just like the IdP cookie would, so they get the same treatment.
        // Neither slides: the remember cookie's expiry is set per sign-in from the policy (a device that keeps signing in
        // would otherwise never be asked for a code again), and the parked user's five minutes are not extended by activity.
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
            HardenTwoFactorCookie(options, sessionOptions.TwoFactorUserCookieName));
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
        {
            HardenTwoFactorCookie(options, sessionOptions.TwoFactorRememberCookieName);

            // The ticket carries the expiry of the day it was issued, which the policy may have shortened since. A
            // client can keep sending a cookie the server asked it to delete, and Identity would go on treating the
            // device as remembered, clearing the failed-code count on every correct password: unlimited code guesses.
            // So the policy's hours are enforced where the cookie is validated, in front of Identity's own security-stamp
            // check (kept, not replaced), and every reader of the cookie sees the same answer.
            var checkStamp = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                var scoped = context.HttpContext.RequestServices;
                var hours = (await scoped.GetRequiredService<IPasswordPolicyProvider>().GetAsync()).TwoFactorRememberHours;
                var issued = context.Properties.IssuedUtc;
                if (hours <= 0 || issued is null
                    || issued.Value.UtcDateTime.AddHours(hours) <= scoped.GetRequiredService<IDateTimeProvider>().UtcNow)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);
                    return;
                }

                await checkStamp(context);
            };
        });

        // Validate the security stamp on every cookie-authenticated request instead of the 30-minute
        // default. Rotating a password (an admin reset, or opening a new access window on a shared
        // temporary account) changes that stamp, and until it is re-checked the previous holder's
        // cookie still mints authorization codes — for up to half an hour after the credential they
        // knew was taken away. The cookie only guards the auth pages and /connect/authorize, so the
        // extra lookup is a keyed read on a handful of requests, not on the SPA's API traffic (which
        // authenticates with the app session cookie).
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        // DB-maintained password policy: cached reader + history recorder. Lockout settings are
        // applied imperatively after migration/seeding in UseAuthModule (NOT via IConfigureOptions —
        // reading the DB during options-configuration at container startup deadlocks).
        services.AddScoped<IPasswordPolicyProvider, PasswordPolicyProvider>();
        services.AddScoped<IPasswordHistoryRecorder, PasswordHistoryRecorder>();
        services.AddScoped<TwoFactorService>();
        // Passkeys (WebAuthn). The relying-party id and origins come from the public URLs above; Fido2 itself holds no state.
        services.AddSingleton<PasskeySettings>();
        services.AddSingleton<PasskeyStateSealer>();
        services.AddSingleton<IFido2>(sp =>
        {
            var passkey = sp.GetRequiredService<PasskeySettings>();
            return new Fido2(new Fido2Configuration
            {
                RPID = passkey.RpId,
                RPName = PasskeySettings.RpName,
                Origins = passkey.Origins
            }, metadataService: null!);
        });
        services.AddScoped<PasskeyService>();
        services.AddScoped<LoginCompletion>();

        // Unit of Work for the Auth module — enables ITransactionalCommand<IAuthUnitOfWork> so
        // multi-step writes (e.g. user creation + group/team links) are atomic.
        services.AddScoped<IAuthUnitOfWork, AuthUnitOfWork>();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        // OpenIddict
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthDbContext>();
            })
            .AddServer(options =>
            {
                var appBaseUrl = configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(appBaseUrl))
                    options.SetIssuer(new Uri(appBaseUrl));

                options.SetTokenEndpointUris("/connect/token");
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                options.AllowClientCredentialsFlow();
                options.AllowRefreshTokenFlow();

                options.UseReferenceRefreshTokens();

                // Server-wide fallbacks. A client that pins its own value on the application row
                // stops following these — `spa` does exactly that for all three, so tightening a
                // number here will not reach the SPA. Check /admin/clients as well.
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                // Refresh tokens are rolling and sliding, so this is an IDLE timeout: the window a
                // session may go without refreshing before it dies. Keep it generous — a
                // machine-to-machine integration like `los` inherits this number, and the browser
                // SPA pins its own far shorter one on the application row.
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(15));

                if (environment == "Development") options.AcceptAnonymousClients();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.OfflineAccess,
                    "appraisal.read", "request.write", "document.read", "document.write", "integration");

                if (environment == "Development")
                {
                    options.AddDevelopmentEncryptionCertificate();
                    options.AddDevelopmentSigningCertificate();
                    options.DisableAccessTokenEncryption();
                }
                else
                {
                    var signingCertConfig = configuration.GetSection("OAuth2:SigningCertificate");
                    var encryptionCertConfig = configuration.GetSection("OAuth2:EncryptionCertificate");

                    if (!signingCertConfig.Exists() || !encryptionCertConfig.Exists())
                        throw new InvalidOperationException(
                            "Production requires OAuth2:SigningCertificate and OAuth2:EncryptionCertificate configuration. " +
                            "Configure both sections (Source = 'store' | 'file') in appsettings.{Environment}.json.");

                    // AddOpenIddict configures at service-registration time, so DI isn't available yet.
                    // Instantiate the provider directly — it only reads IConfiguration.
                    using var certLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
                    var certProvider = new CertificateProvider(
                        configuration,
                        certLoggerFactory.CreateLogger<CertificateProvider>());

                    options.AddSigningCertificate(certProvider.GetSigningCertificate());
                    options.AddEncryptionCertificate(certProvider.GetEncryptionCertificate());
                    // Access-token encryption stays ENABLED in production — both servers must
                    // share the same encryption cert (handled by deploying the same PFX to both).
                }

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        // Authentication schemes. One policy scheme picks the real one per request:
        //   dev header (non-production) -> DevBypass
        //   Authorization: Bearer       -> OpenIddict validation (machine clients: LOS/CLS)
        //   anything else               -> the app session cookie (the SPA)
        // Dev-auth bypass is allowed in all non-production environments (Development, SIT, UAT). An
        // unset/empty env var is treated as Production so a misconfigured server fails closed.
        var allowDevBypass = AllowDevBypass;

        // Back-channel calls (discovery, JWKS, code redemption) go to the internal base URL when one is
        // configured: behind the load balancer the public URL's certificate does not match the node's
        // own name (RemoteCertificateNameMismatch). Without one they go to the identity provider's public
        // address — the API's own until the IdP is split out, then the IdP's.
        var authority = (configuration["Auth:InternalAuthBaseUrl"] is { Length: > 0 } internalBase
            ? internalBase
            : urls.IdpPublicBase).TrimEnd('/');

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = AuthSchemes.Selector;
            options.DefaultChallengeScheme = AuthSchemes.Selector;
        });
        if (allowDevBypass)
            authentication.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
                DevAuthenticationHandler.SchemeName, _ => { });

        authentication
            .AddPolicyScheme(AuthSchemes.Selector, "Dev, Bearer or app session", options =>
            {
                options.ForwardDefaultSelector = context => SchemeSelector.Select(context.Request, allowDevBypass);
            })
            .AddCookie(AuthSchemes.AppCookie, options =>
            {
                options.Cookie.Name = sessionOptions.AppCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // Lax, not Strict: email links open API download URLs by top-level navigation.
                options.Cookie.SameSite = SameSiteMode.Lax;
                // The idle timeout is enforced from auth.UserSessions.LastActivityAt (background polling
                // excluded), not from this expiry. The ticket expiry is only a horizon, set far beyond
                // any idle gap so it never decides anything: a request after more than half of it renews it.
                // Not persistent -> a session cookie that dies with the browser.
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = true;
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.Events.OnValidatePrincipal = AppSessionEvents.ValidatePrincipalAsync;
                options.Events.OnRedirectToLogin = AppSessionEvents.RedirectToLogin;
                options.Events.OnRedirectToAccessDenied = AppSessionEvents.RedirectToAccessDenied;
            })
            .AddOpenIdConnect(AuthSchemes.Oidc, options =>
            {
                options.SignInScheme = AuthSchemes.AppCookie;
                options.Authority = authority;
                options.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                options.ClientId = oidcClient.ClientId;
                // Public client for now (PKCE, no secret). Never assign an empty string: OpenIddict
                // rejects a public client that sends a secret.
                if (!string.IsNullOrEmpty(oidcClient.ClientSecret))
                    options.ClientSecret = oidcClient.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                // query keeps the callback a plain GET, which the WAF/F5 rules treat more simply than form_post.
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                // Every sign-in is interactive: the IdP SSO cookie alone must never mint a session.
                options.Prompt = "login";
                options.Scope.Clear();
                options.Scope.Add(OpenIdConnectScope.OpenId);
                options.Scope.Add("profile");
                // No tokens are kept: the app principal is built from the id_token (see TokenValidatedAsync).
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false; // OpenIddict exposes no userinfo endpoint
                options.MapInboundClaims = false; // keep raw `sub` / `name`, like the access token
                options.UseTokenLifetime = false; // the session length is ours, not the 15-minute id_token's
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.SignedOutRedirectUri = urls.SpaHome;
                options.BackchannelTimeout = TimeSpan.FromSeconds(10);
                // The correlation/nonce cookies and the state live this long. The default 15 minutes is shorter
                // than a person may sit on the login page, and "Correlation failed" would cost them a second
                // password prompt. The authorization code itself is minted after the login and stays short-lived.
                options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(60);
                options.ProtocolValidator.NonceLifetime = TimeSpan.FromMinutes(60);
                // The callback is a same-site top-level redirect (response_mode=query), so Lax is enough
                // and survives where the default SameSite=None would be dropped behind a plain-http hop.
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "roles";
                options.Events.OnTokenValidated = AppSessionEvents.TokenValidatedAsync;
                options.Events.OnTicketReceived = AppSessionEvents.TicketReceivedAsync;
                options.Events.OnRedirectToIdentityProvider = AppSessionEvents.RedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProviderForSignOut =
                    AppSessionEvents.RedirectToIdentityProviderForSignOut;
                options.Events.OnRemoteFailure = AppSessionEvents.RemoteFailureAsync;
            });

        // The cookie handler reads its ticket from auth.UserSessions through this store.
        services.AddSingleton<SessionTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(AuthSchemes.AppCookie)
            .Configure<SessionTicketStore>((options, store) => options.SessionStore = store);

        // Authorization policies
        services.AddAuthorizationBuilder().AddPolicies();

        // Data seeding
        services.AddScoped<IDataSeeder<AuthDbContext>, AuthDataSeed>();

        // LDAP
        services.Configure<LdapConfiguration>(configuration.GetSection(LdapConfiguration.SectionName));
        services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IAuthAuditWriter, AuthAuditWriter>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<ICompanyLookupService, CompanyLookupService>();
        services.AddScoped<PermissionResolver>();

        // Browser sessions: server-side store, per-request claims, revoke/end operations.
        services.AddScoped<AuthorizationClaimsBuilder>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<ISessionStatusReader>(sp => sp.GetRequiredService<IUserSessionService>());
        services.AddTransient<UserSessionCleanupJob>();

        // Menu tree cache (single-instance in-memory)
        services.AddMemoryCache();
        services.AddScoped<IMenuTreeCache, MenuTreeCache>();

        // Repositories
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();

        return services;
    }

    public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
    {
        app.UseDataSeeding<AuthDbContext>();
        app.UseModuleRecurringJobs<AuthDbContext>(AuthRecurringJobs.All);

        // Apply the DB-maintained lockout settings to the IdentityOptions snapshot once, after the
        // policy row has been migrated + seeded. Done here (not via IConfigureOptions) because reading
        // the DB during options-configuration at container startup deadlocks. Lockout therefore takes
        // effect on the next restart — the admin screen states this.
        ApplyLockoutPolicy(app.ApplicationServices);

        WarnAboutPasskeySettings(app.ApplicationServices);

        return app;
    }

    /// <summary>
    /// CSRF / cross-site WebSocket check for cookie-authenticated requests. Place it before routing so a
    /// rejected request never reaches an endpoint (and never touches the database).
    /// </summary>
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app) =>
        app.UseMiddleware<CsrfProtectionMiddleware>(AllowDevBypass);

    /// <summary>
    /// Server-side forced password change. Place it between UseAuthentication and UseAuthorization.
    /// </summary>
    public static IApplicationBuilder UseMustChangePasswordGate(this IApplicationBuilder app) =>
        app.UseMiddleware<MustChangePasswordMiddleware>();

    // A passkey is bound to the identity provider's host (the relying-party id) for good. That host cannot be an IP address,
    // and an SPA origin that is neither that host nor a subdomain of it can never register one: the browser refuses. Say so at
    // startup, not at the first user.
    private static void WarnAboutPasskeySettings(IServiceProvider services)
    {
        var settings = services.GetRequiredService<PasskeySettings>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Auth.Passkeys");
        if (settings.RpIdIsIpAddress)
            logger.LogWarning(
                "[PASSKEY-IP-RPID] The identity provider's public address is the IP address {RpId}. WebAuthn does not accept an IP "
                + "address as a relying-party id, so passkeys will not work until the IdP is reached by a host name.", settings.RpId);
        if (settings.MismatchedOrigins.Count == 0) return;

        logger.LogWarning(
            "[PASSKEY-ORIGIN-MISMATCH] Passkeys are bound to {RpId} (the host of the identity provider's public address), but "
            + "these allowed origins are not that host or a subdomain of it, so passkeys cannot be registered from them: {Origins}. "
            + "Serve the SPA under the IdP's host or domain, or set Auth:Idp:PublicBaseUrl accordingly.",
            settings.RpId, string.Join(", ", settings.MismatchedOrigins));
    }

    private static void HardenTwoFactorCookie(CookieAuthenticationOptions options, string name)
    {
        options.Cookie.Name = name;
        options.Cookie.Path = "/"; // __Host- requires it
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        // The parked user gets five minutes from the password, however busy the pages after it are.
        options.SlidingExpiration = false;
    }

    private static void ApplyLockoutPolicy(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var policy = dbContext.PasswordPolicy.AsNoTracking().FirstOrDefault();
        if (policy is null) return;

        var lockout = scope.ServiceProvider
            .GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;
        lockout.AllowedForNewUsers = policy.LockoutEnabled;
        lockout.MaxFailedAccessAttempts = policy.MaxFailedAccessAttempts;
        lockout.DefaultLockoutTimeSpan = policy.LockoutMinutes <= 0
            ? TimeSpan.FromDays(365 * 200)
            : TimeSpan.FromMinutes(policy.LockoutMinutes);
    }

    private static AuthorizationBuilder AddPolicies(this AuthorizationBuilder authorizationBuilder)
    {
        authorizationBuilder
            .AddClientPermissionPolicy("CanReadAuth", ["auth:read"])
            .AddClientPermissionPolicy("CanWriteAuth", ["auth:read", "auth:write"])
            .AddClientPermissionPolicy("CanReadDocument", ["document:read"])
            .AddClientPermissionPolicy("CanWriteDocument", ["document:read", "document:write"])
            .AddClientPermissionPolicy("CanReadNotification", ["notification:read"])
            .AddClientPermissionPolicy(
                "CanWriteNotification",
                ["notification:read", "notification:write"]
            )
            .AddClientPermissionPolicy("CanReadRequest", ["request:read"])
            .AddClientPermissionPolicy("CanWriteRequest", ["request:read", "request:write"])
            .AddUserPermissionPolicy("CanManagePermissions", "PERMISSION_MANAGE")
            .AddUserPermissionPolicy("CanManageRoles", "ROLE_MANAGE")
            .AddUserPermissionPolicy("CanManageGroups", "GROUP_MANAGE")
            .AddUserPermissionPolicy("CanManageTeams", "TEAM_MANAGE")
            .AddUserPermissionPolicy("CanViewAuthAudit", "AUTH_AUDIT_VIEW")
            .AddUserPermissionPolicy("CanManageUsers", "USER_MANAGE")
            .AddUserPermissionPolicy("CanManageMenus", "MENU_MANAGE")
            .AddUserPermissionPolicy("CanManageCompanies", "COMPANY_MANAGE")
            .AddUserPermissionPolicy("CanChangeUserPassword", "USER_CHANGE_PASSWORD")
            .AddUserPermissionPolicy("CanResetUserPassword", "USER_RESET_PASSWORD")
            .AddUserPermissionPolicy("CanManagePasswordPolicy", "PASSWORD_POLICY_MANAGE")
            .AddUserPermissionPolicy("CanReleaseTaskLocks", "TASK_LOCK_MANAGE")
            // Meeting policies — TODO: replace with real role claims once role infrastructure is complete
            .AddUserPermissionPolicy("MeetingAdmin", "MEETING_ADMIN")
            .AddUserPermissionPolicy("MeetingSecretary", "MEETING_SECRETARY")
            .AddUserPermissionPolicy("CommitteeMember", "COMMITTEE_MEMBER")
            .AddScopePolicy("ClsReadAppraisal", "appraisal.read")
            .AddScopePolicy("ClsWriteRequest", "request.write")
            .AddScopePolicy("ClsReadDocument", "document.read")
            .AddScopePolicy("ClsWriteDocument", "document.write")
            .AddScopePolicy("Integration", "integration")
            .AddUserPermissionPolicy("workflow.admin", "WORKFLOW_ADMIN")
            .AddUserPermissionPolicy("WebhookDeliveriesView", "WEBHOOK_DELIVERIES_VIEW")
            .AddUserPermissionPolicy("WebhookDeliveriesRetry", "WEBHOOK_DELIVERIES_RETRY")
            .AddUserPermissionPolicy("FailedMessageView", "FAILED_MESSAGE_VIEW")
            .AddUserPermissionPolicy("FailedMessageManage", "FAILED_MESSAGE_MANAGE")
            // OAuth client / scope registration + webhook subscription admin
            .AddUserPermissionPolicy("OAuthClientsManage", "OAUTH_CLIENTS_MANAGE")
            .AddUserPermissionPolicy("OAuthScopesManage", "OAUTH_SCOPES_MANAGE")
            .AddUserPermissionPolicy("WebhookSubscriptionsManage", "WEBHOOK_SUBSCRIPTIONS_MANAGE")
            .AddUserPermissionPolicy("WebhookSecretReveal", "WEBHOOK_SECRET_REVEAL")
            .AddUserPermissionPolicy("OAuthTokensRevoke", "OAUTH_TOKENS_REVOKE")
            .AddUserPermissionPolicy("LogsView", "LOGS_VIEW")
            // Prefix policies so the optional :TEAM scope variant satisfies the endpoint too
            // (base "TASK_MONITOR_VIEW" is a prefix of "TASK_MONITOR_VIEW:TEAM").
            .AddUserPermissionPrefixPolicy("task-monitor.view", "TASK_MONITOR_VIEW")
            .AddUserPermissionPrefixPolicy("task-monitor.reassign", "TASK_MONITOR_REASSIGN")
            .AddUserPermissionPolicy("history-search.view", "HISTORY_SEARCH_VIEW")
            .AddUserPermissionPolicy("sla-config.manage", "SLA_CONFIG_MANAGE")
            .AddUserPermissionPolicy("job-schedule.manage", "JOB_SCHEDULE_MANAGE")
            .AddUserPermissionPolicy("address-master.manage", "ADDRESS_MASTER_MANAGE")
            .AddUserPermissionPolicy("reappraisal.generate-test-file", "REAPPRAISAL_GENERATE_TEST_FILE")
            .AddUserPermissionPolicy("appraisal.data-correction", "APPRAISAL_DATA_CORRECTION")
            // Appraisal read surface. Accepts EITHER the full APPRAISAL_VIEW or the credit-side
            // APPRAISAL_TRACKING_VIEW; the handlers then mask the fields a tracking-only caller
            // must not receive. A prefix policy on "APPRAISAL_" would be wrong here — it would
            // also match the menu-only APPRAISAL_*_VIEW section permissions that RequestMaker and
            // the appraisal roles already hold.
            .AddUserPermissionAnyPolicy("appraisal.browse",
                ["APPRAISAL_VIEW", "APPRAISAL_TRACKING_VIEW"])
            // ── Monitoring feature policies (FSD §2.6.8) ──────────────────────────
            // Any-prefix policies: caller needs ANY permission with the given prefix.
            .AddMonitoringPrefixPolicy("monitoring.pending-internal", "MONITORING:PENDING_INTERNAL:")
            .AddMonitoringPrefixPolicy("monitoring.pending-external", "MONITORING:PENDING_EXTERNAL:")
            // Single-permission policies (admin screens — no layer split)
            .AddUserPermissionAnyPolicy("monitoring.pending-quotation",
                ["MONITORING:PENDING_QUOTATION"])
            .AddUserPermissionAnyPolicy("monitoring.pending-followup",
                ["MONITORING:PENDING_FOLLOWUP"])
            .AddUserPermissionAnyPolicy("monitoring.pending-evaluation",
                ["MONITORING:PENDING_EVALUATION"])
            .AddUserPermissionAnyPolicy("monitoring.meeting-followup",
                ["MONITORING:MEETING_FOLLOWUP"])
            // Top-breaches: visible to anyone with any OLA monitoring permission
            .AddMonitoringTopBreachesPolicy()
            // Hangfire dashboard: a server-rendered page opened by browser navigation, so it cannot carry a
            // Bearer header. It authenticates with the app session cookie like the rest of the SPA, pinned
            // here because the default scheme would pick DevBypass/Bearer first. A browser with no session is
            // sent through the OIDC sign-in (see AppSessionEvents.RedirectToLogin).
            //
            // Access follows the permission, not a hardcoded role: whoever is granted
            // JOB_SCHEDULE_MANAGE gets in, so the admin screens stay the single place where this is
            // decided. Admin still qualifies, holding every permission.
            //
            // The matching sidebar entry is gated on JOB_SCHEDULE_MANAGE too; its "System" group
            // has no gate of its own and shows whenever any child is visible.
            .AddPolicy("HangfireDashboard", policy =>
                policy.AddAuthenticationSchemes(AuthSchemes.AppCookie)
                    .RequireAuthenticatedUser()
                    .RequireClaim("permissions", "JOB_SCHEDULE_MANAGE"));

        // Default and fallback policies are deliberately NOT pinned to a scheme: the Selector policy
        // scheme routes each request (dev header, Bearer, app cookie). Pinning them to OpenIddict, as
        // production used to, would make every cookie request fail there and only there.
        authorizationBuilder
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return authorizationBuilder;
    }

    private static AuthorizationBuilder AddClientPermissionPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string name,
        string[] allowedValues
    )
    {
        authorizationBuilder.AddPolicy(
            name,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequirePermission(allowedValues);
            }
        );
        return authorizationBuilder;
    }

    private static void RequirePermission(
        this AuthorizationPolicyBuilder policy,
        params string[] allowedValues
    )
    {
        policy.RequireClaim("permissions", allowedValues);
    }

    private static AuthorizationBuilder AddUserPermissionPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string requiredPermission
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("permissions", requiredPermission);
            }
        );
        return authorizationBuilder;
    }

    private static AuthorizationBuilder AddScopePolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string requiredScope
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "scope")
                        .SelectMany(c => c.Value.Split(' '))
                        .Contains(requiredScope));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Policy that passes when the user holds ANY "permissions" claim
    /// that starts with the given prefix (e.g. "MONITORING:PENDING_INTERNAL:").
    /// Used for layer-scoped monitoring screens where the specific layer suffix varies.
    /// </summary>
    private static AuthorizationBuilder AddMonitoringPrefixPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string permissionPrefix
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c => c.Value.StartsWith(permissionPrefix, StringComparison.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Generic prefix policy: passes when the user holds ANY "permissions" claim that starts with
    /// the given prefix. Lets a base permission and its scoped variants (e.g. "TASK_MONITOR_VIEW"
    /// and "TASK_MONITOR_VIEW:TEAM") share one endpoint policy.
    /// </summary>
    private static AuthorizationBuilder AddUserPermissionPrefixPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string permissionPrefix
    ) => authorizationBuilder.AddMonitoringPrefixPolicy(policyName, permissionPrefix);

    /// <summary>
    /// Policy for the top-breaches endpoint: passes when the user holds ANY OLA monitoring
    /// permission (Internal, External, or Followup).
    /// </summary>
    private static AuthorizationBuilder AddMonitoringTopBreachesPolicy(
        this AuthorizationBuilder authorizationBuilder)
    {
        authorizationBuilder.AddPolicy(
            "monitoring.top-breaches",
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c =>
                            c.Value.StartsWith("MONITORING:PENDING_INTERNAL:", StringComparison.OrdinalIgnoreCase) ||
                            c.Value.StartsWith("MONITORING:PENDING_EXTERNAL:", StringComparison.OrdinalIgnoreCase) ||
                            c.Value.Equals("MONITORING:PENDING_FOLLOWUP", StringComparison.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }

    /// <summary>
    /// Policy that passes when the user holds ANY of the listed exact permission codes.
    /// Used for admin-level monitoring screens with a flat permission model.
    /// </summary>
    private static AuthorizationBuilder AddUserPermissionAnyPolicy(
        this AuthorizationBuilder authorizationBuilder,
        string policyName,
        string[] permissionCodes
    )
    {
        authorizationBuilder.AddPolicy(
            policyName,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(ctx =>
                    ctx.User.Claims
                        .Where(c => c.Type == "permissions")
                        .Any(c => permissionCodes.Contains(c.Value, StringComparer.OrdinalIgnoreCase)));
            }
        );
        return authorizationBuilder;
    }
}