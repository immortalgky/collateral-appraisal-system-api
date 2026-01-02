# OAuth2 Hands-On Exercises

## Practical Learning Exercises for Junior Developers

These exercises will help you understand OAuth2/OpenIddict implementation through hands-on practice. Each exercise builds on the previous ones and includes detailed solutions.

## Prerequisites

Before starting these exercises, ensure you have:

- ✅ Read `OAuth2-OpenIddict-Fundamentals.md`
- ✅ Read `OAuth2-Implementation-Guide.md`
- ✅ Development environment set up (Docker, .NET 9, SQL Server)
- ✅ Application running at https://localhost:7111

---

## Exercise 1: Trace the Authorization Flow

**Objective:** Understand the complete OAuth2 authorization flow by tracing each step.

### Task

Follow the authorization flow manually using browser developer tools and document each step.

### Steps

1. **Start the application**
   ```bash
   docker compose up -d
   dotnet run --project Bootstrapper/Api
   ```

2. **Open browser developer tools** (F12) and go to Network tab

3. **Navigate to authorization endpoint:**
   ```
   https://localhost:7111/connect/authorize?response_type=code&client_id=spa&redirect_uri=https%3A%2F%2Flocalhost%3A3000%2Fcallback&scope=openid%20profile%20email&code_challenge=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk&code_challenge_method=S256
   ```

4. **Document each HTTP request/response you see**

5. **Login with default credentials:**
   - Username: `admin`
   - Password: `P@ssw0rd!`

6. **Analyze the redirect URL** that contains the authorization code

### Expected Results

You should see these network requests in order:

1. `GET /connect/authorize` → `302 Redirect` to `/Account/Login`
2. `GET /Account/Login` → `200 OK` (login page)
3. `POST /Account/Login` → `302 Redirect` back to `/connect/authorize`
4. `GET /connect/authorize` → `302 Redirect` to callback URL with `code` parameter

### Questions to Answer

1. What happens when you visit `/connect/authorize` without being logged in?
2. Where is the CSRF protection implemented?
3. What parameters are included in the callback redirect?
4. How long is the authorization code valid for?

<details>
<summary>🔍 Solution - Click to expand</summary>

### Step-by-Step Flow Analysis

1. **Initial Authorization Request**
   ```
   GET /connect/authorize?response_type=code&client_id=spa&redirect_uri=https%3A%2F%2Flocalhost%3A3000%2Fcallback&scope=openid%20profile%20email&code_challenge=xyz&code_challenge_method=S256
   
   Response: 302 Found
   Location: /Account/Login?ReturnUrl=%2Fconnect%2Fauthorize%3F...
   ```

2. **Login Page**
   ```
   GET /Account/Login?ReturnUrl=...
   Response: 200 OK
   Content: HTML login form with anti-forgery token
   ```

3. **Login Submission**
   ```
   POST /Account/Login
   Content-Type: application/x-www-form-urlencoded
   Body: Username=admin&Password=P%40ssw0rd%21&__RequestVerificationToken=...
   
   Response: 302 Found
   Location: /connect/authorize?... (original authorization request)
   ```

4. **Authorization Grant**
   ```
   GET /connect/authorize?... (same as step 1)
   Response: 302 Found
   Location: https://localhost:3000/callback?code=CfDJ8...&scope=openid+profile+email&state=...
   ```

### Answers

1. **Unauthenticated redirect:** OpenIddict middleware detects no authentication and redirects to login page with return URL
2. **CSRF protection:** `__RequestVerificationToken` hidden field in login form, validated with `[ValidateAntiForgeryToken]`
3. **Callback parameters:** `code` (authorization code), `scope` (granted scopes), `state` (if provided in request)
4. **Authorization code lifetime:** 10 minutes by default (configurable in OpenIddict settings)

</details>

---

## Exercise 2: Add a New OAuth2 Client

**Objective:** Create a new confidential client for a background service and test client credentials flow.

### Task

Add a new OAuth2 client called "background-service" that can authenticate using client credentials flow.

### Requirements

- Client ID: `background-service`
- Client Type: Confidential (has secret)
- Grant Type: Client Credentials
- Scopes: Custom scope called `api:process`
- No user interaction required

### Steps

1. **Add the new client to data seeding**

   Modify `Modules/Auth/OAuth2OpenId/Data/Seed/AuthDataSeed.cs`:

   ```csharp
   // Add this after the existing SPA client creation
   if (await manager.FindByClientIdAsync("background-service") is null)
   {
       await manager.CreateAsync(new OpenIddictApplicationDescriptor
       {
           ClientId = "background-service",
           ClientSecret = "your-secure-secret-here", // TODO: Replace with secure secret
           DisplayName = "Background Processing Service",
           ClientType = OpenIddictConstants.ClientTypes.Confidential,
           
           Permissions =
           {
               OpenIddictConstants.Permissions.Endpoints.Token,
               OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
               OpenIddictConstants.Permissions.Scopes.Roles,
               "scp:api:process"  // Custom scope
           }
       });
   }
   ```

2. **Register the custom scope**

   Modify `Modules/Auth/OAuth2OpenId/OpenIddictModule.cs`:

   ```csharp
   options.RegisterScopes(
       OpenIddictConstants.Scopes.OpenId, 
       OpenIddictConstants.Scopes.Profile,
       OpenIddictConstants.Scopes.Email, 
       OpenIddictConstants.Scopes.OfflineAccess,
       "api:process"  // Add your custom scope
   );
   ```

3. **Test the client credentials flow**

   Create an HTTP request to test:

   ```bash
   curl -X POST https://localhost:7111/connect/token \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "grant_type=client_credentials&client_id=background-service&client_secret=your-secure-secret-here&scope=api:process"
   ```

4. **Verify the access token**

   Use the returned token to call a protected API:

   ```bash
   curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" https://localhost:7111/requests
   ```

### Expected Results

- New client appears in `OpenIddictApplications` table
- Client credentials request returns access token
- Access token can be used to call protected APIs
- Token contains `api:process` scope

<details>
<summary>🔍 Solution - Click to expand</summary>

### Complete Implementation

**1. Update AuthDataSeed.cs (line ~64):**
```csharp
// Add after existing SPA client creation
if (await manager.FindByClientIdAsync("background-service") is null)
{
    await manager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = "background-service",
        ClientSecret = "BackgroundService-P@ssw0rd-2024!",
        DisplayName = "Background Processing Service",
        ClientType = OpenIddictConstants.ClientTypes.Confidential,
        
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            "scp:api:process"
        }
    });
}
```

**2. Update OpenIddictModule.cs (line ~77):**
```csharp
options.RegisterScopes(
    OpenIddictConstants.Scopes.OpenId,
    OpenIddictConstants.Scopes.Profile,
    OpenIddictConstants.Scopes.Email,
    OpenIddictConstants.Scopes.OfflineAccess,
    "api:process"
);
```

**3. Test Request:**
```bash
curl -X POST https://localhost:7111/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=background-service&client_secret=BackgroundService-P@ssw0rd-2024!&scope=api:process"
```

**4. Expected Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "api:process"
}
```

**5. Database Verification:**
```sql
SELECT ClientId, DisplayName, Type, Permissions 
FROM OpenIddictApplications 
WHERE ClientId = 'background-service';
```

</details>

---

## Exercise 3: Implement a Protected API Endpoint

**Objective:** Create a new API endpoint that requires specific scopes and test authorization.

### Task

Create a new endpoint `/api/processing/status` that requires the `api:process` scope from Exercise 2.

### Requirements

- Only accessible with `api:process` scope
- Returns processing status information
- Proper authorization error handling
- Logging of access attempts

### Steps

1. **Create a new controller**

   Create `Controllers/ProcessingController.cs`:

   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   [Authorize] // Require authentication
   public class ProcessingController : ControllerBase
   {
       private readonly ILogger<ProcessingController> _logger;

       public ProcessingController(ILogger<ProcessingController> logger)
       {
           _logger = logger;
       }

       [HttpGet("status")]
       [Authorize(Policy = "ProcessingScope")] // Require specific scope
       public IActionResult GetStatus()
       {
           var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
           _logger.LogInformation("Processing status requested by {UserId}", userId);

           return Ok(new
           {
               Status = "Running",
               QueuedJobs = 5,
               ProcessingJobs = 2,
               CompletedJobs = 150,
               Timestamp = DateTimeOffset.UtcNow
           });
       }
   }
   ```

2. **Configure the authorization policy**

   Add to `Program.cs` after authentication setup:

   ```csharp
   builder.Services.AddAuthorization(options =>
   {
       options.AddPolicy("ProcessingScope", policy =>
           policy.RequireScope("api:process"));
   });
   ```

3. **Create a scope requirement and handler**

   Create `Auth/ScopeRequirement.cs`:

   ```csharp
   public class ScopeRequirement : IAuthorizationRequirement
   {
       public string Scope { get; }

       public ScopeRequirement(string scope)
       {
           Scope = scope;
       }
   }

   public class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
   {
       protected override Task HandleRequirementAsync(
           AuthorizationHandlerContext context,
           ScopeRequirement requirement)
       {
           var scopeClaim = context.User.FindFirst("scope");
           if (scopeClaim != null && scopeClaim.Value.Contains(requirement.Scope))
           {
               context.Succeed(requirement);
           }

           return Task.CompletedTask;
       }
   }
   ```

4. **Test the endpoint**

   Test with different tokens:

   ```bash
   # Should fail - no token
   curl https://localhost:7111/api/processing/status

   # Should fail - SPA token without api:process scope
   curl -H "Authorization: Bearer SPA_TOKEN" https://localhost:7111/api/processing/status

   # Should succeed - background service token with api:process scope
   curl -H "Authorization: Bearer BACKGROUND_SERVICE_TOKEN" https://localhost:7111/api/processing/status
   ```

### Expected Results

- Unauthenticated requests return 401 Unauthorized
- Authenticated requests without proper scope return 403 Forbidden
- Properly scoped requests return 200 OK with processing status

<details>
<summary>🔍 Solution - Click to expand</summary>

### Complete Implementation

**1. Create ProcessingController.cs:**
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> _logger;

    public ProcessingController(ILogger<ProcessingController> logger)
    {
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        // Check for api:process scope
        var scopes = User.FindAll("scope").Select(c => c.Value).ToList();
        if (!scopes.Any(s => s.Contains("api:process")))
        {
            _logger.LogWarning("Access denied: Missing api:process scope for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Forbid("Insufficient permissions. Required scope: api:process");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        _logger.LogInformation("Processing status requested by {UserId}", userId);

        return Ok(new
        {
            Status = "Running",
            QueuedJobs = 5,
            ProcessingJobs = 2,
            CompletedJobs = 150,
            Timestamp = DateTimeOffset.UtcNow,
            AccessedBy = userId,
            Scopes = scopes
        });
    }
}
```

**2. Test Results:**

```bash
# No authentication
curl https://localhost:7111/api/processing/status
# Response: 401 Unauthorized

# With background service token
curl -H "Authorization: Bearer [BACKGROUND_SERVICE_TOKEN]" https://localhost:7111/api/processing/status
# Response: 200 OK
{
  "status": "Running",
  "queuedJobs": 5,
  "processingJobs": 2,
  "completedJobs": 150,
  "timestamp": "2024-01-15T10:30:00Z",
  "accessedBy": "system",
  "scopes": ["api:process"]
}
```

**3. Alternative Policy-Based Approach:**

```csharp
// In Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProcessingScope", policy =>
        policy.RequireClaim("scope", "api:process"));
});

// In Controller
[HttpGet("status")]
[Authorize(Policy = "ProcessingScope")]
public IActionResult GetStatus() { ... }
```

</details>

---

## Exercise 4: Debug Authentication Issues

**Objective:** Learn to identify and fix common OAuth2 authentication problems.

### Scenario

You receive these bug reports from users:

1. "Login page shows CSRF error"
2. "Access token expired but refresh doesn't work"
3. "SPA can't connect after deploying to new domain"
4. "Background service gets 401 errors randomly"

### Task

For each scenario, identify the problem and implement a solution.

### Debugging Tools

Use these tools to investigate:

```bash
# Check OpenIddict database tables
SELECT TOP 10 * FROM OpenIddictApplications;
SELECT TOP 10 * FROM OpenIddictTokens WHERE ExpirationDate > GETUTCDATE();
SELECT TOP 10 * FROM OpenIddictAuthorizations;

# Decode JWT tokens (use jwt.io or PowerShell)
# PowerShell example:
$token = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
$payload = $token.Split('.')[1]
$decodedBytes = [System.Convert]::FromBase64String($payload + "==")
[System.Text.Encoding]::UTF8.GetString($decodedBytes)
```

### Scenarios to Debug

#### Scenario 1: CSRF Error on Login

**Symptoms:**
- Users see "The provided anti-forgery token was invalid" on login
- Happens intermittently

**Investigation Steps:**
1. Check browser cookies
2. Review anti-forgery configuration
3. Look for HTTPS/HTTP mixed content

<details>
<summary>🔍 Solution - Scenario 1</summary>

**Root Cause:** Anti-forgery tokens are tied to HTTPS but login form is loading over HTTP in some cases.

**Solution:**
```csharp
// In OpenIddictModule.cs - Fix anti-forgery configuration
services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Force HTTPS
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.RequireSsl = true; // Require SSL
});

// In Program.cs - Force HTTPS redirect
app.UseHttpsRedirection();
```

**Prevention:** Always test authentication flows over HTTPS, even in development.

</details>

#### Scenario 2: Refresh Token Issues

**Symptoms:**
- Access tokens expire after 1 hour
- Refresh token requests return "invalid_grant"
- Users must re-login frequently

**Investigation Steps:**
1. Check refresh token configuration
2. Verify token storage
3. Look for token rotation issues

<details>
<summary>🔍 Solution - Scenario 2</summary>

**Root Cause:** Refresh tokens are being reused after they've been consumed (token rotation).

**Solution:**
```csharp
// In client code - Handle refresh token rotation
async function refreshAccessToken() {
    const refreshToken = localStorage.getItem('refresh_token');
    
    const response = await fetch('/connect/token', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
            grant_type: 'refresh_token',
            refresh_token: refreshToken,
            client_id: 'spa'
        })
    });
    
    if (response.ok) {
        const tokens = await response.json();
        localStorage.setItem('access_token', tokens.access_token);
        
        // Update refresh token if new one provided
        if (tokens.refresh_token) {
            localStorage.setItem('refresh_token', tokens.refresh_token);
        }
        
        return tokens.access_token;
    } else {
        // Refresh failed - redirect to login
        localStorage.clear();
        window.location.href = '/login';
    }
}
```

**Prevention:** Always update the refresh token when a new one is provided.

</details>

#### Scenario 3: SPA Domain Issues

**Symptoms:**
- SPA works on localhost:3000
- Fails when deployed to https://myapp.com
- Gets "invalid_redirect_uri" error

**Investigation Steps:**
1. Check registered redirect URIs
2. Verify CORS configuration
3. Check client registration

<details>
<summary>🔍 Solution - Scenario 3</summary>

**Root Cause:** New domain not registered in OAuth2 client configuration.

**Solution:**
```csharp
// Update AuthDataSeed.cs to include production domain
RedirectUris = {
    new Uri("https://localhost:7111/callback"),
    new Uri("https://localhost:3000/callback"),
    new Uri("https://myapp.com/callback"),      // Add production domain
    new Uri("https://myapp.com/silent-renew")  // Add silent renew endpoint
},
PostLogoutRedirectUris = {
    new Uri("https://localhost:7111/"),
    new Uri("https://localhost:3000/"),
    new Uri("https://myapp.com/")               // Add production domain
}
```

**Prevention:** Environment-specific client configuration or wildcard support (carefully implemented).

</details>

#### Scenario 4: Background Service 401 Errors

**Symptoms:**
- Background service authentication works sometimes
- Gets 401 errors randomly
- Token appears valid when decoded

**Investigation Steps:**
1. Check token lifetime and refresh logic
2. Verify system clock synchronization
3. Look for concurrent token refresh attempts

<details>
<summary>🔍 Solution - Scenario 4</summary>

**Root Cause:** Service is using expired tokens due to lack of proactive refresh.

**Solution:**
```csharp
public class TokenManager
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private string? _currentToken;
    private DateTime _tokenExpiry;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    public async Task<string> GetValidTokenAsync()
    {
        // Check if token needs refresh (with 5-minute buffer)
        if (_currentToken == null || DateTime.UtcNow.AddMinutes(5) >= _tokenExpiry)
        {
            await _refreshSemaphore.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_currentToken == null || DateTime.UtcNow.AddMinutes(5) >= _tokenExpiry)
                {
                    await RefreshTokenAsync();
                }
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        return _currentToken!;
    }

    private async Task RefreshTokenAsync()
    {
        var response = await _httpClient.PostAsync("/connect/token", 
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config["OAuth:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["OAuth:ClientSecret"]),
                new KeyValuePair<string, string>("scope", "api:process")
            }));

        var tokenResponse = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse);
        
        _currentToken = token.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
    }
}
```

**Prevention:** Proactive token refresh with proper concurrency handling.

</details>

---

## Exercise 5: Production Security Hardening

**Objective:** Configure the system for production deployment with proper security measures.

### Task

Prepare the OAuth2 configuration for production deployment by implementing security hardening measures.

### Requirements

- Replace development certificates with production certificates
- Configure proper CORS policies
- Implement security headers
- Set up proper client secret management
- Configure rate limiting

### Steps

1. **Create certificate configuration**

   Add to `appsettings.Production.json`:

   ```json
   {
     "OAuth2": {
       "SigningCertificate": {
         "Source": "KeyVault",
         "KeyVaultUri": "https://mykeyvault.vault.azure.net/",
         "CertificateName": "oauth-signing-cert"
       },
       "EncryptionCertificate": {
         "Source": "KeyVault", 
         "KeyVaultUri": "https://mykeyvault.vault.azure.net/",
         "CertificateName": "oauth-encryption-cert"
       }
     }
   }
   ```

2. **Implement certificate provider**

   Create `Security/ProductionCertificateProvider.cs`:

   ```csharp
   public class ProductionCertificateProvider : ICertificateProvider
   {
       public async Task<X509Certificate2> GetSigningCertificateAsync()
       {
           // Implementation to load from Azure Key Vault
           throw new NotImplementedException("Implement Azure Key Vault integration");
       }

       public async Task<X509Certificate2> GetEncryptionCertificateAsync()
       {
           // Implementation to load from Azure Key Vault
           throw new NotImplementedException("Implement Azure Key Vault integration");
       }
   }
   ```

3. **Configure CORS for production**

   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("Production", builder =>
       {
           builder
               .WithOrigins("https://myapp.com", "https://admin.myapp.com")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
       });
   });
   ```

4. **Add security headers middleware**

   ```csharp
   app.Use(async (context, next) =>
   {
       context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
       context.Response.Headers.Add("X-Frame-Options", "DENY");
       context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
       context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
       await next();
   });
   ```

5. **Test production configuration**

   Create a staging environment that mimics production and test all authentication flows.

### Expected Results

- No development certificates in production
- CORS restricted to specific domains
- Security headers present in all responses
- Rate limiting prevents brute force attacks
- Client secrets stored securely

<details>
<summary>🔍 Solution - Click to expand</summary>

### Complete Production Configuration

**1. Update OpenIddictModule.cs for production:**

```csharp
public static IServiceCollection AddOpenIddictModule(this IServiceCollection services, IConfiguration configuration)
{
    // Environment detection
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    var isProduction = environment == "Production";

    // Certificate configuration
    if (isProduction)
    {
        services.AddScoped<ICertificateProvider, ProductionCertificateProvider>();
    }
    else
    {
        services.AddScoped<ICertificateProvider, DevelopmentCertificateProvider>();
    }

    services.AddOpenIddict()
        .AddServer(options =>
        {
            // Production security hardening
            if (isProduction)
            {
                // No anonymous clients in production
                // options.AcceptAnonymousClients(); // REMOVED

                // Use production certificates
                var certProvider = services.BuildServiceProvider().GetRequiredService<ICertificateProvider>();
                options.AddSigningCertificate(certProvider.GetSigningCertificateAsync().GetAwaiter().GetResult());
                options.AddEncryptionCertificate(certProvider.GetEncryptionCertificateAsync().GetAwaiter().GetResult());

                // Enable access token encryption
                options.EnableTokenEncryption();
            }
            else
            {
                // Development settings
                options.AcceptAnonymousClients();
                options.AddDevelopmentEncryptionCertificate();
                options.AddDevelopmentSigningCertificate();
                options.DisableAccessTokenEncryption();
            }
        });

    return services;
}
```

**2. CORS Configuration:**

```csharp
// In Program.cs
if (builder.Environment.IsProduction())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Production", policy =>
        {
            policy
                .WithOrigins(
                    "https://myapp.com",
                    "https://admin.myapp.com",
                    "https://api.myapp.com"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Development", policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
}

// Apply CORS
app.UseCors(builder.Environment.IsProduction() ? "Production" : "Development");
```

**3. Security Headers Middleware:**

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Add("Strict-Transport-Security", 
                "max-age=31536000; includeSubDomains");
        }

        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<SecurityHeadersMiddleware>();
```

**4. Rate Limiting (ASP.NET Core 7+):**

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 5;
    });
});

// Apply to auth endpoints
app.MapPost("/connect/token", ...)
   .RequireRateLimiting("AuthPolicy");
```

**5. Production Client Management:**

```csharp
// Environment-specific client configuration
public class ProductionAuthDataSeed : IDataSeeder<OpenIddictDbContext>
{
    public async Task SeedAllAsync()
    {
        // Production client with secure configuration
        if (await manager.FindByClientIdAsync("production-spa") is null)
        {
            var clientSecret = await GetSecretFromVault("oauth-spa-secret");
            
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "production-spa",
                ClientSecret = clientSecret,
                DisplayName = "Production SPA",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                
                RedirectUris = {
                    new Uri("https://myapp.com/callback"),
                    new Uri("https://myapp.com/silent-renew")
                },
                
                Requirements = {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                }
            });
        }
    }
}
```

</details>

---

## Summary and Next Steps

### What You've Learned

Through these exercises, you should now understand:

1. **OAuth2 Flow Mechanics** - How requests and responses work in practice
2. **Client Management** - How to register and configure different client types
3. **API Protection** - How to secure endpoints with proper authorization
4. **Troubleshooting Skills** - How to identify and fix common authentication issues
5. **Production Security** - How to harden the system for production deployment

### Skills Assessment Checklist

Can you now:

- [ ] Trace an OAuth2 authorization flow from start to finish?
- [ ] Add new OAuth2 clients with appropriate security settings?
- [ ] Create protected API endpoints with scope requirements?
- [ ] Debug common authentication and authorization issues?
- [ ] Configure production-ready security settings?

### Practice Recommendations

1. **Set up a test SPA** that implements the full OAuth2 flow
2. **Create integration tests** for your OAuth2 flows
3. **Practice certificate management** in a staging environment
4. **Implement monitoring** for authentication events
5. **Review security logs** regularly for suspicious activity

### Additional Resources for Continued Learning

- **OpenIddict Documentation:** https://documentation.openiddict.com/
- **OAuth2 RFC 6749:** https://tools.ietf.org/html/rfc6749
- **PKCE RFC 7636:** https://tools.ietf.org/html/rfc7636
- **OpenID Connect Specification:** https://openid.net/connect/

You're now ready to work confidently with OAuth2 and OpenIddict in production systems!