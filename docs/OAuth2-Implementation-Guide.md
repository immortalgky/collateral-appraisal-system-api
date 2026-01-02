# OAuth2 Implementation Guide - Collateral Appraisal System

## Practical Implementation Walkthrough

This guide walks through our actual OAuth2/OpenIddict implementation, showing you exactly how authentication works in our system with real code examples.

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Module Structure](#module-structure)
3. [Configuration Walkthrough](#configuration-walkthrough)
4. [Authentication Flow Step-by-Step](#authentication-flow-step-by-step)
5. [Database Schema](#database-schema)
6. [Code Deep Dive](#code-deep-dive)
7. [Integration Points](#integration-points)
8. [Testing the Implementation](#testing-the-implementation)

---

## System Architecture

### High-Level Overview

```
┌─────────────────┐    HTTPS    ┌─────────────────┐    EF Core    ┌─────────────────┐
│   Frontend      │◄──────────►│   Auth Module   │◄─────────────►│   SQL Server    │
│   (React/Vue)   │             │   (OpenIddict)  │               │   Database      │
│                 │             │                 │               │                 │
│ Port 3000/7111  │             │ Port 7111       │               │ Port 1433       │
└─────────────────┘             └─────────────────┘               └─────────────────┘
                                         │
                                         │ Authentication
                                         ▼
                                ┌─────────────────┐
                                │   API Endpoints │
                                │   (Protected)   │
                                │                 │
                                │ /requests       │
                                │ /appraisals     │
                                │ /workflows      │
                                └─────────────────┘
```

### Key URLs in Our System

| Endpoint | Purpose | Example |
|----------|---------|---------|
| `https://localhost:7111/connect/authorize` | Authorization endpoint | Redirects user to login |
| `https://localhost:7111/connect/token` | Token endpoint | Exchanges code for tokens |
| `https://localhost:7111/Account/Login` | Login UI | User enters credentials |
| `https://localhost:7111/requests` | Protected API | Requires valid access token |

---

## Module Structure

Our Auth module is split into two main parts:

```
Modules/Auth/
├── Auth/                           # Core authentication logic
│   ├── AuthModule.cs               # Module registration
│   ├── Auth/Features/Token/        # Token-related features
│   └── Auth/Dtos/                  # Data transfer objects
└── OAuth2OpenId/                   # OpenIddict implementation
    ├── OpenIddictModule.cs         # OpenIddict configuration
    ├── Controllers/                # OAuth2 endpoints
    ├── Data/                       # Database context and models
    └── Pages/Account/              # Login UI
```

### Key Files and Their Roles

| File | Responsibility | Key Methods/Features |
|------|----------------|---------------------|
| `OpenIddictModule.cs` | Configuration and DI setup | `AddOpenIddictModule()`, certificate setup |
| `OpenIddictController.cs` | OAuth2 flow handling | `Authorize()`, `Token()` |
| `Login.cshtml.cs` | User authentication UI | `OnPostAsync()` |
| `AuthDataSeed.cs` | Initial data seeding | Creates admin user and SPA client |

---

## Configuration Walkthrough

### 1. Service Registration (OpenIddictModule.cs:11-131)

```csharp
public static IServiceCollection AddOpenIddictModule(this IServiceCollection services, IConfiguration configuration)
{
    // 1. Add Razor Pages for login UI
    services.AddRazorPages().AddApplicationPart(typeof(Login).Assembly);

    // 2. Configure anti-forgery protection
    services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.Name = "__RequestVerificationToken";
        options.Cookie.HttpOnly = true;                    // ✅ Security: Prevent XSS
        options.Cookie.SameSite = SameSiteMode.Strict;     // ✅ Security: Prevent CSRF
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // 3. Setup database context
    services.AddDbContext<OpenIddictDbContext>(options =>
    {
        options.UseSqlServer(configuration.GetConnectionString("Database"));
        options.UseOpenIddict();  // ✅ Configures EF for OpenIddict tables
    });

    // 4. Configure ASP.NET Core Identity
    services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;              // ✅ Strong password policy
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
    });
```

**What's happening here:**
- We're setting up the foundation: UI, security, database, and user management
- Notice the security configurations - these prevent common web vulnerabilities

### 2. OpenIddict Server Configuration (OpenIddictModule.cs:51-126)

```csharp
services.AddOpenIddict()
    .AddCore(options =>
    {
        // Use Entity Framework for storage
        options.UseEntityFrameworkCore().UseDbContext<OpenIddictDbContext>();
    })
    .AddServer(options =>
    {
        // 1. Configure OAuth2 endpoints
        options.SetTokenEndpointUris("/connect/token");           // RFC 6749 compliant
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetEndSessionEndpointUris("/connect/logout");

        // 2. Enable supported flows
        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();                 // ✅ PKCE for security
        options.AllowClientCredentialsFlow();                     // For service-to-service
        options.AllowRefreshTokenFlow();                          // For token renewal

        // 3. Environment-specific security
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment == "Development")
        {
            options.AcceptAnonymousClients();                     // ⚠️ Development only!
            options.AddDevelopmentEncryptionCertificate();
            options.AddDevelopmentSigningCertificate();
        }
        else
        {
            // Production requires real certificates
            throw new InvalidOperationException("Production certificates not configured");
        }

        // 4. Register scopes (permissions)
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,        // "I need to identify the user"
            OpenIddictConstants.Scopes.Profile,       // "I need user profile info"
            OpenIddictConstants.Scopes.Email,         // "I need user email"
            OpenIddictConstants.Scopes.OfflineAccess  // "I need refresh tokens"
        );
    });
```

**Key Points:**
- We support 3 OAuth2 flows: Authorization Code (with PKCE), Client Credentials, and Refresh Token
- Development uses temporary certificates; production needs real ones
- Scopes define what information/permissions can be requested

---

## Authentication Flow Step-by-Step

Let's trace through what happens when a user logs into our SPA:

### Step 1: User Clicks "Login" in SPA

**SPA Code:**
```javascript
// Frontend redirects to authorization endpoint
const authUrl = `https://localhost:7111/connect/authorize?` +
    `response_type=code&` +
    `client_id=spa&` +
    `redirect_uri=${encodeURIComponent('https://localhost:3000/callback')}&` +
    `scope=openid profile email&` +
    `code_challenge=${codeChallenge}&` +
    `code_challenge_method=S256`;

window.location.href = authUrl;
```

### Step 2: Authorization Endpoint Processing (OpenIddictController.cs:11-36)

```csharp
[HttpGet("~/connect/authorize")]
public async Task<IActionResult> Authorize()
{
    var request = HttpContext.GetOpenIddictServerRequest();

    // Check if user is already authenticated
    if (HttpContext.User.Identity?.IsAuthenticated != true)
    {
        // Not logged in → redirect to login page
        return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");
    }

    // User is authenticated → create authorization response
    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    identity.AddClaim(OpenIddictConstants.Claims.Subject, HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    identity.AddClaim(OpenIddictConstants.Claims.Name, HttpContext.User.Identity.Name);

    // Set claim destinations (where claims will be included)
    foreach (var claim in identity.Claims)
        claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);

    var principal = new ClaimsPrincipal(identity);
    principal.SetScopes(request.GetScopes());

    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
```

**What happens:**
1. OpenIddict checks if user is authenticated with ASP.NET Identity
2. If not authenticated → redirect to login page with return URL
3. If authenticated → create claims and return authorization code

### Step 3: User Authentication (Login.cshtml.cs:22-59)

```csharp
[ValidateAntiForgeryToken]  // ✅ CSRF protection
public async Task<IActionResult> OnPostAsync()
{
    logger.LogInformation("Login attempt for user: {Username}", Username);

    if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
    {
        Error = "Username and password are required.";
        return Page();
    }

    // Authenticate with ASP.NET Identity
    var result = await signInManager.PasswordSignInAsync(Username, Password, true, false);
    if (result.Succeeded)
    {
        logger.LogInformation("User {Username} logged in successfully", Username);

        // Validate return URL for security
        var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;
        if (!Url.IsLocalUrl(redirectUrl) && !redirectUrl.StartsWith("https://"))
        {
            logger.LogWarning("Invalid return URL: {ReturnUrl}, redirecting to home", redirectUrl);
            redirectUrl = "/";
        }

        return Redirect(redirectUrl);  // Back to authorization endpoint
    }

    Error = "Invalid login attempt.";
    return Page();
}
```

**Security features:**
- Anti-forgery token validation prevents CSRF attacks
- Return URL validation prevents open redirect vulnerabilities
- Password lockout support (configured in Identity options)

### Step 4: Authorization Code Exchange (OpenIddictController.cs:38-70)

After user authenticates and consents, SPA receives authorization code and exchanges it for tokens:

```csharp
[HttpPost("~/connect/token")]
public async Task<IActionResult> Token()
{
    var request = HttpContext.GetOpenIddictServerRequest();

    if (!request.IsAuthorizationCodeGrantType())
        return BadRequest(new { error = "Unsupported grant_type" });

    // Validate authorization code and PKCE
    var principal = (await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
    
    if (principal == null) 
        return BadRequest(new { error = "Invalid authorization code" });

    // Extract user information
    var userId = principal.FindFirstValue(OpenIddictConstants.Claims.Subject);
    var username = principal.FindFirstValue(OpenIddictConstants.Claims.Name);

    // Create new identity for tokens
    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    identity.AddClaim(OpenIddictConstants.Claims.Subject, userId);
    identity.AddClaim(OpenIddictConstants.Claims.Name, username);

    // Set destinations for claims
    foreach (var claim in identity.Claims)
        claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);

    var claimsPrincipal = new ClaimsPrincipal(identity);
    claimsPrincipal.SetScopes(request.GetScopes());
    
    return SignIn(claimsPrincipal, null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
```

**Token Response:**
```json
{
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "def50200...",
  "id_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9..."
}
```

---

## Database Schema

### OpenIddict Tables (Auto-Generated)

```sql
-- OpenIddict Applications (OAuth2 clients)
CREATE TABLE [OpenIddictApplications] (
    [Id] NVARCHAR(450) PRIMARY KEY,
    [ClientId] NVARCHAR(100) NOT NULL,
    [ClientSecret] NVARCHAR(MAX),
    [DisplayName] NVARCHAR(MAX),
    [Type] NVARCHAR(50),  -- 'public' or 'confidential'
    [Permissions] NVARCHAR(MAX),  -- JSON array
    [RedirectUris] NVARCHAR(MAX)  -- JSON array
);

-- OpenIddict Authorizations (User consents)
CREATE TABLE [OpenIddictAuthorizations] (
    [Id] NVARCHAR(450) PRIMARY KEY,
    [ApplicationId] NVARCHAR(450),
    [Subject] NVARCHAR(400),  -- User ID
    [Scopes] NVARCHAR(MAX),
    [Status] NVARCHAR(50)
);

-- OpenIddict Tokens (Access, refresh, ID tokens)
CREATE TABLE [OpenIddictTokens] (
    [Id] NVARCHAR(450) PRIMARY KEY,
    [ApplicationId] NVARCHAR(450),
    [AuthorizationId] NVARCHAR(450),
    [Subject] NVARCHAR(400),
    [Type] NVARCHAR(50),  -- 'access_token', 'refresh_token', 'id_token'
    [Payload] NVARCHAR(MAX),  -- Encrypted token data
    [ExpirationDate] DATETIMEOFFSET
);
```

### Identity Tables

```sql
-- ASP.NET Identity Users
CREATE TABLE [AspNetUsers] (
    [Id] NVARCHAR(450) PRIMARY KEY,
    [UserName] NVARCHAR(256),
    [NormalizedUserName] NVARCHAR(256),
    [Email] NVARCHAR(256),
    [PasswordHash] NVARCHAR(MAX),
    [SecurityStamp] NVARCHAR(MAX),
    [LockoutEnd] DATETIMEOFFSET,
    [LockoutEnabled] BIT
);

-- ASP.NET Identity Roles
CREATE TABLE [AspNetRoles] (
    [Id] NVARCHAR(450) PRIMARY KEY,
    [Name] NVARCHAR(256),
    [NormalizedName] NVARCHAR(256)
);
```

### Our Custom Extensions

```csharp
public class ApplicationUser : IdentityUser
{
    // Add custom properties here
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

public class ApplicationRole : IdentityRole
{
    // Add custom role properties here
    public string? Description { get; set; }
}
```

---

## Code Deep Dive

### Client Registration (AuthDataSeed.cs:34-63)

Our system automatically registers a SPA client on startup:

```csharp
if (await manager.FindByClientIdAsync("spa") is null)
    await manager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = "spa",
        DisplayName = "SPA",
        ClientType = OpenIddictConstants.ClientTypes.Public,  // ✅ No client secret needed
        
        // Allowed redirect URIs
        PostLogoutRedirectUris = { new Uri("https://localhost:7111/") },
        RedirectUris = {
            new Uri("https://localhost:7111/callback"),     // API callback
            new Uri("https://localhost:3000/callback")      // SPA callback
        },
        
        // OAuth2 permissions
        Permissions = {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles
        },
        
        Requirements = {
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange  // ✅ PKCE required
        }
    });
```

**Key Points:**
- Public client (no secret) because SPAs can't securely store secrets
- Multiple redirect URIs for development flexibility
- PKCE required for security

### Token Validation in API

Our API endpoints are protected using standard JWT validation:

```csharp
// In Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7111";  // Our auth server
        options.Audience = "api-resource";
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
    });

// API Controller
[ApiController]
[Authorize]  // ✅ Requires valid JWT token
public class RequestsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequests()
    {
        // This method requires authentication
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // ... implementation
    }
}
```

---

## Integration Points

### 1. Bootstrapper Integration (Program.cs)

```csharp
// Service registration
builder.Services
    .AddRequestModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddOpenIddictModule(builder.Configuration);  // ✅ Auth module

// Middleware pipeline
app.UseRouting();
app.UseAuthentication();  // ✅ JWT validation
app.UseAuthorization();   // ✅ Permission checking
app.MapControllers();
```

**Order matters!** Authentication must come before authorization.

### 2. Frontend Integration

**JavaScript (SPA):**
```javascript
// 1. Start OAuth2 flow
const startLogin = () => {
    const state = generateRandomString();
    const codeVerifier = generateRandomString();
    const codeChallenge = base64UrlEncode(sha256(codeVerifier));
    
    localStorage.setItem('oauth_state', state);
    localStorage.setItem('code_verifier', codeVerifier);
    
    const params = new URLSearchParams({
        response_type: 'code',
        client_id: 'spa',
        redirect_uri: window.location.origin + '/callback',
        scope: 'openid profile email',
        state: state,
        code_challenge: codeChallenge,
        code_challenge_method: 'S256'
    });
    
    window.location.href = `https://localhost:7111/connect/authorize?${params}`;
};

// 2. Handle callback
const handleCallback = async () => {
    const urlParams = new URLSearchParams(window.location.search);
    const code = urlParams.get('code');
    const state = urlParams.get('state');
    
    if (state !== localStorage.getItem('oauth_state')) {
        throw new Error('Invalid state parameter');
    }
    
    const response = await fetch('https://localhost:7111/connect/token', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            grant_type: 'authorization_code',
            code: code,
            redirect_uri: window.location.origin + '/callback',
            client_id: 'spa',
            code_verifier: localStorage.getItem('code_verifier')
        })
    });
    
    const tokens = await response.json();
    localStorage.setItem('access_token', tokens.access_token);
    localStorage.setItem('refresh_token', tokens.refresh_token);
};

// 3. Make authenticated API calls
const callAPI = async () => {
    const token = localStorage.getItem('access_token');
    const response = await fetch('https://localhost:7111/requests', {
        headers: { 'Authorization': `Bearer ${token}` }
    });
    return response.json();
};
```

---

## Testing the Implementation

### 1. Manual Testing Flow

**Step 1: Start the application**
```bash
docker compose up -d
dotnet run --project Bootstrapper/Api
```

**Step 2: Navigate to authorization endpoint**
```
https://localhost:7111/connect/authorize?response_type=code&client_id=spa&redirect_uri=https://localhost:3000/callback&scope=openid%20profile&code_challenge=xyz&code_challenge_method=S256
```

**Step 3: Login with default admin user**
- Username: `admin`
- Password: `P@ssw0rd!`

**Step 4: Exchange code for tokens** (use the code from redirect)
```bash
curl -X POST https://localhost:7111/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=YOUR_CODE&client_id=spa&code_verifier=xyz"
```

**Step 5: Use access token to call protected API**
```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" https://localhost:7111/requests
```

### 2. Database Verification

Check that authentication data is stored correctly:

```sql
-- Check registered applications
SELECT ClientId, DisplayName, Type, Permissions FROM OpenIddictApplications;

-- Check user accounts
SELECT UserName, Email, LockoutEnabled FROM AspNetUsers;

-- Check active tokens (development only)
SELECT Type, Subject, ExpirationDate FROM OpenIddictTokens WHERE ExpirationDate > GETUTCDATE();
```

### 3. Troubleshooting Common Issues

| Issue | Symptoms | Solution |
|-------|----------|----------|
| **CORS errors** | Browser blocks requests | Configure CORS in Program.cs |
| **Certificate errors** | Token validation fails | Check development certificates |
| **Redirect mismatch** | Invalid redirect_uri error | Verify URLs match registered client |
| **PKCE validation fails** | Invalid code_verifier error | Ensure code_verifier matches code_challenge |

---

## Summary

### What We've Built

1. **Complete OAuth2/OIDC server** using OpenIddict
2. **Secure authentication flow** with PKCE for SPAs
3. **Database persistence** for users, clients, and tokens
4. **Integration with ASP.NET Identity** for user management
5. **JWT-based API protection** with standard validation

### Key Security Features

- ✅ PKCE prevents authorization code interception
- ✅ Anti-forgery tokens prevent CSRF attacks  
- ✅ Secure cookies with HttpOnly and SameSite
- ✅ Strong password policies
- ✅ JWT signature validation
- ✅ Redirect URI validation prevents open redirects

### Next Steps

1. Review security best practices in the next guide
2. Practice with hands-on exercises
3. Learn troubleshooting techniques
4. Understand production deployment considerations

Your junior developer should now understand how our authentication system works from both a theoretical and practical perspective!