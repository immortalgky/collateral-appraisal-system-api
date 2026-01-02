# OAuth2 Troubleshooting Guide

## Common Issues and Solutions for OAuth2/OpenIddict

This guide helps you quickly identify and resolve common OAuth2 authentication issues in our Collateral Appraisal System.

## Table of Contents

1. [General Troubleshooting Approach](#general-troubleshooting-approach)
2. [Authentication Flow Issues](#authentication-flow-issues)
3. [Token Problems](#token-problems)
4. [API Access Issues](#api-access-issues)
5. [Configuration Problems](#configuration-problems)
6. [Database Issues](#database-issues)
7. [Production Deployment Issues](#production-deployment-issues)
8. [Development Environment Issues](#development-environment-issues)

---

## General Troubleshooting Approach

### Step-by-Step Debugging Process

1. **Identify the symptom** - What exactly is failing?
2. **Locate the error** - Browser console, application logs, network requests
3. **Isolate the component** - Frontend, backend, database, certificates
4. **Check configuration** - Compare with working examples
5. **Test incrementally** - Verify each step of the flow
6. **Verify fixes** - Test the complete flow after changes

### Essential Tools

| Tool | Purpose | How to Access |
|------|---------|---------------|
| **Browser Dev Tools** | Network requests, console errors | F12 in browser |
| **Application Logs** | Server-side errors and info | `Logs/log-development-*.txt` |
| **Database Tools** | Check tokens, clients, users | SQL Server Management Studio |
| **JWT Decoder** | Inspect token contents | https://jwt.io |
| **REST Client** | Test API endpoints | VS Code extension or Postman |

---

## Authentication Flow Issues

### Issue: "invalid_redirect_uri" Error

**Symptoms:**
- Authorization request fails with redirect URI error
- User sees OAuth2 error page instead of login

**Possible Causes:**
```
❌ Redirect URI doesn't match registered URI exactly
❌ Protocol mismatch (http vs https)  
❌ Port number missing or incorrect
❌ URL encoding issues
```

**Solutions:**

1. **Check registered redirect URIs:**
   ```sql
   SELECT ClientId, RedirectUris FROM OpenIddictApplications WHERE ClientId = 'spa';
   ```

2. **Verify exact match:**
   ```csharp
   // Must match exactly - case sensitive
   RedirectUris = {
       new Uri("https://localhost:3000/callback"),  // ✅ Correct
       // not "https://localhost:3000/callback/"    // ❌ Trailing slash
       // not "http://localhost:3000/callback"      // ❌ Wrong protocol  
       // not "https://localhost/callback"          // ❌ Missing port
   }
   ```

3. **Update client registration:**
   ```csharp
   // In AuthDataSeed.cs - add missing URLs
   RedirectUris = {
       new Uri("https://localhost:7111/callback"),
       new Uri("https://localhost:3000/callback"),
       new Uri("https://myapp.com/callback")  // Add production URL
   }
   ```

**Prevention:** Use environment-specific configuration for redirect URIs.

---

### Issue: User Gets Stuck in Login Loop

**Symptoms:**
- User enters credentials successfully
- Gets redirected back to login page repeatedly
- No obvious error messages

**Possible Causes:**
```
❌ Anti-forgery token validation failing
❌ Cookie/session issues  
❌ HTTPS/HTTP mixed content
❌ Clock skew between client and server
```

**Diagnostic Steps:**

1. **Check browser console for errors:**
   ```javascript
   // Look for CSRF/anti-forgery errors
   // Check for mixed content warnings
   ```

2. **Inspect network requests:**
   ```
   POST /Account/Login → 200 OK or 302 redirect?
   Set-Cookie headers present?
   __RequestVerificationToken in form data?
   ```

3. **Verify anti-forgery configuration:**
   ```csharp
   services.AddAntiforgery(options =>
   {
       options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // ⚠️ HTTPS required
       options.Cookie.SameSite = SameSiteMode.Strict;
   });
   ```

**Solutions:**

1. **Fix HTTPS enforcement:**
   ```csharp
   // In development, allow HTTP
   if (environment.IsDevelopment())
   {
       options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
   }
   ```

2. **Clear browser state:**
   ```bash
   # Clear cookies, local storage, and cached data
   # Or use incognito/private browsing mode
   ```

3. **Check server time:**
   ```bash
   # Ensure server time is synchronized
   date  # Linux/Mac
   Get-Date  # PowerShell
   ```

---

### Issue: PKCE Validation Errors

**Symptoms:**
- Token exchange fails with "invalid PKCE" or "invalid code verifier"
- Works in development but fails in production

**Possible Causes:**
```
❌ Code verifier doesn't match code challenge
❌ Wrong hashing algorithm (not SHA256)
❌ Base64 URL encoding issues
❌ PKCE parameters getting lost/modified
```

**Diagnostic Steps:**

1. **Verify PKCE implementation:**
   ```javascript
   // Correct PKCE generation
   function generateCodeVerifier() {
       const array = new Uint32Array(32);
       crypto.getRandomValues(array);
       return base64URLEncode(array);
   }

   function generateCodeChallenge(verifier) {
       const encoder = new TextEncoder();
       const data = encoder.encode(verifier);
       return crypto.subtle.digest('SHA-256', data)
           .then(digest => base64URLEncode(new Uint8Array(digest)));
   }
   ```

2. **Check parameter transmission:**
   ```
   Authorization request: code_challenge=xyz&code_challenge_method=S256
   Token request: code_verifier=original_verifier
   ```

**Solutions:**

1. **Fix encoding issues:**
   ```javascript
   function base64URLEncode(buffer) {
       return btoa(String.fromCharCode(...new Uint8Array(buffer)))
           .replace(/\+/g, '-')
           .replace(/\//g, '_')
           .replace(/=/g, '');
   }
   ```

2. **Verify server configuration:**
   ```csharp
   // Ensure PKCE is required
   Requirements = {
       OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
   }
   ```

**Prevention:** Use well-tested PKCE libraries instead of custom implementations.

---

## Token Problems

### Issue: "Invalid Token" API Errors (401)

**Symptoms:**
- API calls return 401 Unauthorized
- Token appears valid when decoded
- Sometimes works, sometimes doesn't

**Diagnostic Steps:**

1. **Decode and inspect token:**
   ```bash
   # Use jwt.io or PowerShell
   $token = "eyJhbGciOiJSUzI1NiJ9..."
   # Check exp, aud, iss claims
   ```

2. **Check token validation configuration:**
   ```csharp
   services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options =>
       {
           options.Authority = "https://localhost:7111";       // ✅ Correct issuer
           options.Audience = "api-resource";                  // ✅ Expected audience
           options.RequireHttpsMetadata = true;                // ✅ HTTPS validation
           options.ClockSkew = TimeSpan.FromMinutes(5);        // ✅ Clock tolerance
       });
   ```

3. **Verify token claims:**
   ```json
   {
     "iss": "https://localhost:7111",      // Must match Authority
     "aud": "api-resource",                // Must match Audience  
     "exp": 1640995200,                    // Must be future timestamp
     "nbf": 1640991600,                    // Must be past timestamp
     "iat": 1640991600                     // Must be past timestamp
   }
   ```

**Common Solutions:**

| Problem | Symptom | Solution |
|---------|---------|----------|
| **Wrong issuer** | `iss` claim doesn't match | Update `options.Authority` |
| **Wrong audience** | `aud` claim doesn't match | Update `options.Audience` or token request |
| **Token expired** | `exp` in past | Refresh token or re-authenticate |
| **Clock skew** | Time mismatch | Sync server clocks, increase `ClockSkew` |
| **Invalid signature** | Signature verification fails | Check signing certificates |

---

### Issue: Tokens Expire Too Quickly

**Symptoms:**
- Access tokens expire in minutes instead of hours
- Users need to re-authenticate frequently
- Refresh tokens don't work

**Solutions:**

1. **Configure token lifetimes:**
   ```csharp
   services.Configure<OpenIddictServerOptions>(options =>
   {
       options.SetAccessTokenLifetime(TimeSpan.FromHours(1));      // 1 hour
       options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));     // 30 days
       options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(10)); // 10 minutes
   });
   ```

2. **Implement token refresh logic:**
   ```javascript
   class TokenManager {
       async getValidToken() {
           const token = localStorage.getItem('access_token');
           const expires = localStorage.getItem('token_expires');
           
           // Refresh 5 minutes before expiration
           if (!token || Date.now() > (expires - 300000)) {
               return await this.refreshToken();
           }
           
           return token;
       }
       
       async refreshToken() {
           const refreshToken = localStorage.getItem('refresh_token');
           // Implementation...
       }
   }
   ```

---

## API Access Issues

### Issue: "Insufficient Scope" Errors (403)

**Symptoms:**
- API returns 403 Forbidden
- Token is valid but missing permissions
- Different endpoints require different scopes

**Diagnostic Steps:**

1. **Check token scopes:**
   ```bash
   # Decode token and look for "scope" claim
   # Should contain required scopes like "api:process"
   ```

2. **Verify endpoint requirements:**
   ```csharp
   [HttpGet("status")]
   [Authorize(Policy = "ProcessingScope")] // Requires api:process scope
   public IActionResult GetStatus() { ... }
   ```

3. **Check client permissions:**
   ```sql
   SELECT ClientId, Permissions FROM OpenIddictApplications WHERE ClientId = 'spa';
   ```

**Solutions:**

1. **Request proper scopes:**
   ```javascript
   // Include all required scopes in authorization request
   const authUrl = `${baseUrl}/connect/authorize?` +
       `scope=openid profile email api:process api:admin`;
   ```

2. **Update client permissions:**
   ```csharp
   Permissions = {
       // Add scope permissions
       "scp:api:process",
       "scp:api:admin",
       OpenIddictConstants.Permissions.Scopes.OpenId,
       OpenIddictConstants.Permissions.Scopes.Profile
   }
   ```

---

## Configuration Problems

### Issue: Certificate Errors in Production

**Symptoms:**
- Token validation fails in production
- "Unable to obtain configuration" errors
- HTTPS/TLS errors

**Diagnostic Steps:**

1. **Check certificate configuration:**
   ```bash
   # View certificate details
   openssl x509 -in signing-cert.pem -text -noout
   
   # Test HTTPS connection
   curl -v https://your-auth-server/connect/token
   ```

2. **Verify certificate loading:**
   ```csharp
   // Check if certificates are loaded correctly
   var signingKeys = await GetSigningKeysAsync();
   if (!signingKeys.Any())
   {
       throw new InvalidOperationException("No signing certificates found");
   }
   ```

**Solutions:**

1. **Use proper certificates:**
   ```csharp
   // Don't use development certificates in production
   if (environment.IsProduction())
   {
       var cert = LoadCertificateFromSecureStorage();
       options.AddSigningCertificate(cert);
   }
   ```

2. **Configure certificate validation:**
   ```csharp
   services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
   {
       options.BackchannelHttpHandler = new HttpClientHandler
       {
           ServerCertificateCustomValidationCallback = 
               HttpClientHandler.DangerousAcceptAnyServerCertificateValidator // ⚠️ Dev only
       };
   });
   ```

---

## Database Issues

### Issue: Client Not Found Errors

**Symptoms:**
- "Client not found" or "Invalid client" errors
- Worked before but stopped working
- New clients don't appear

**Diagnostic Steps:**

1. **Check client registration:**
   ```sql
   SELECT * FROM OpenIddictApplications WHERE ClientId = 'spa';
   ```

2. **Verify data seeding:**
   ```csharp
   // Check if SeedAllAsync() is being called
   public async Task SeedAllAsync()
   {
       // Add logging to verify execution
       _logger.LogInformation("Seeding OAuth2 clients...");
   }
   ```

**Solutions:**

1. **Force re-seeding:**
   ```sql
   -- Delete existing client to force re-creation
   DELETE FROM OpenIddictApplications WHERE ClientId = 'spa';
   ```

2. **Check seeding order:**
   ```csharp
   // Ensure seeding runs after migrations
   public static void Main(string[] args)
   {
       var host = CreateHostBuilder(args).Build();
       
       using (var scope = host.Services.CreateScope())
       {
           var context = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
           context.Database.Migrate();  // ✅ Migrations first
           
           var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder<OpenIddictDbContext>>();
           seeder.SeedAllAsync().Wait();  // ✅ Then seeding
       }
       
       host.Run();
   }
   ```

---

## Production Deployment Issues

### Issue: CORS Errors in Production

**Symptoms:**
- Works in development, fails in production
- Browser blocks requests with CORS errors
- "Access to fetch blocked by CORS policy"

**Diagnostic Steps:**

1. **Check CORS configuration:**
   ```csharp
   services.AddCors(options =>
   {
       options.AddPolicy("Production", policy =>
       {
           policy.WithOrigins("https://myapp.com")  // ✅ Specific origins
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 .AllowCredentials();
       });
   });
   ```

2. **Verify origin matching:**
   ```javascript
   // Request origin must match exactly
   // https://myapp.com ✅
   // https://myapp.com/ ❌ (trailing slash)
   // http://myapp.com ❌ (wrong protocol)
   ```

**Solutions:**

1. **Environment-specific CORS:**
   ```csharp
   if (builder.Environment.IsProduction())
   {
       builder.Services.AddCors(options =>
       {
           options.AddDefaultPolicy(policy =>
               policy.WithOrigins("https://myapp.com", "https://admin.myapp.com"));
       });
   }
   else
   {
       builder.Services.AddCors(options =>
       {
           options.AddDefaultPolicy(policy =>
               policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
       });
   }
   ```

---

## Development Environment Issues

### Issue: Docker/Database Connection Problems

**Symptoms:**
- "Cannot connect to SQL Server" errors
- Application starts but authentication fails
- Database migrations don't run

**Diagnostic Steps:**

1. **Check Docker containers:**
   ```bash
   docker compose ps
   # Ensure all containers are running
   
   docker compose logs sqlserver
   # Check SQL Server logs
   ```

2. **Test database connection:**
   ```bash
   # Test SQL Server connection
   sqlcmd -S localhost -U sa -P 'P@ssw0rd'
   
   # Or use docker exec
   docker exec -it collateral-appraisal-system-api-sqlserver-1 /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'P@ssw0rd'
   ```

**Solutions:**

1. **Reset Docker environment:**
   ```bash
   # Stop all containers and remove volumes
   docker compose down -v
   
   # Recreate everything
   docker compose up -d
   
   # Run migrations again
   dotnet ef database update --project Modules/Auth/OAuth2OpenId --startup-project Bootstrapper/Api
   ```

2. **Check port conflicts:**
   ```bash
   # Check if ports are available
   netstat -an | grep 1433  # SQL Server
   netstat -an | grep 6379  # Redis  
   netstat -an | grep 5672  # RabbitMQ
   ```

---

## Quick Diagnostic Commands

### Health Check Commands
```bash
# Test application startup
curl https://localhost:7111/health

# Check OpenIddict discovery
curl https://localhost:7111/.well-known/openid_configuration

# Test database connection
dotnet ef dbcontext info --project Modules/Auth/OAuth2OpenId --startup-project Bootstrapper/Api

# Check certificate expiration
openssl x509 -in cert.pem -noout -dates
```

### Database Queries
```sql
-- Check client configuration
SELECT ClientId, DisplayName, Type, Permissions, RedirectUris FROM OpenIddictApplications;

-- View active tokens
SELECT Type, Subject, Status, ExpirationDate FROM OpenIddictTokens WHERE ExpirationDate > GETUTCDATE();

-- Check user accounts
SELECT UserName, Email, LockoutEnabled FROM AspNetUsers;

-- View recent authorizations
SELECT TOP 10 * FROM OpenIddictAuthorizations ORDER BY Id DESC;
```

### Log Analysis
```bash
# Application logs
tail -f Logs/log-development-*.txt | grep -i error

# Docker logs
docker compose logs -f --tail=100

# Filter OAuth2 specific logs
docker compose logs | grep -i "openiddict\|oauth\|token"
```

---

## Emergency Recovery Procedures

### Complete Authentication Reset

If everything is broken and you need to start fresh:

```bash
# 1. Stop application
docker compose down

# 2. Remove all data
docker volume prune -f

# 3. Delete local logs
rm -rf Logs/*

# 4. Restart infrastructure
docker compose up -d

# 5. Run migrations
dotnet ef database update --project Modules/Auth/OAuth2OpenId --startup-project Bootstrapper/Api

# 6. Start application
dotnet run --project Bootstrapper/Api
```

### Database-Only Reset

If only authentication data is corrupted:

```sql
-- Clear OpenIddict tables
DELETE FROM OpenIddictTokens;
DELETE FROM OpenIddictAuthorizations; 
DELETE FROM OpenIddictApplications;

-- Clear user accounts (optional)
DELETE FROM AspNetUserRoles;
DELETE FROM AspNetUsers;

-- Application will recreate everything on next startup
```

---

## Prevention Strategies

### Monitoring and Alerting

1. **Log authentication events:**
   ```csharp
   _logger.LogInformation("User {UserId} authenticated successfully", userId);
   _logger.LogWarning("Failed login attempt for {Username}", username);
   _logger.LogError("Token validation failed: {Error}", error);
   ```

2. **Health checks:**
   ```csharp
   builder.Services.AddHealthChecks()
       .AddDbContext<OpenIddictDbContext>()
       .AddCheck<OAuthConfigurationHealthCheck>("oauth-config");
   ```

### Automated Testing

1. **Integration tests for auth flows:**
   ```csharp
   [Test]
   public async Task AuthorizationFlow_ShouldReturnTokens()
   {
       // Test complete OAuth2 flow
   }
   ```

2. **Token validation tests:**
   ```csharp
   [Test]
   public async Task InvalidToken_ShouldReturn401()
   {
       // Test API security
   }
   ```

Remember: Most OAuth2 issues are configuration problems. Start with the basics (URLs, certificates, database state) before diving into complex debugging scenarios!