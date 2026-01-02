# OAuth2 Quick Reference

## Developer Cheat Sheet for OAuth2/OpenIddict

This is your go-to reference for common OAuth2 tasks, commands, and configurations in our Collateral Appraisal System.

---

## 🚀 Quick Start Commands

### Start Development Environment
```bash
# Start infrastructure
docker compose up -d

# Run application
dotnet run --project Bootstrapper/Api

# Access URLs
# API: https://localhost:7111
# Auth: https://localhost:7111/connect/authorize
# Login: https://localhost:7111/Account/Login
```

### Default Credentials
- **Username:** `admin`
- **Password:** `P@ssw0rd!`

---

## 🔑 OAuth2 Endpoints

| Endpoint | Method | Purpose | Example |
|----------|--------|---------|---------|
| `/connect/authorize` | GET | Start authorization flow | `?response_type=code&client_id=spa&redirect_uri=...` |
| `/connect/token` | POST | Exchange code for tokens | `grant_type=authorization_code&code=...` |
| `/connect/logout` | POST | End session | Clear tokens and redirect |
| `/Account/Login` | GET/POST | User login page | Username/password form |

---

## 🎫 Token Requests

### Authorization Code Flow (SPA)
```bash
# 1. Get authorization code (browser redirect)
GET https://localhost:7111/connect/authorize?response_type=code&client_id=spa&redirect_uri=https%3A%2F%2Flocalhost%3A3000%2Fcallback&scope=openid%20profile%20email&code_challenge=xyz&code_challenge_method=S256

# 2. Exchange code for tokens
curl -X POST https://localhost:7111/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=YOUR_CODE&client_id=spa&code_verifier=xyz&redirect_uri=https%3A%2F%2Flocalhost%3A3000%2Fcallback"
```

### Client Credentials Flow (Service-to-Service)
```bash
curl -X POST https://localhost:7111/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=background-service&client_secret=SECRET&scope=api:process"
```

### Refresh Token Flow
```bash
curl -X POST https://localhost:7111/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&refresh_token=YOUR_REFRESH_TOKEN&client_id=spa"
```

---

## 🛡️ API Protection

### Protect Controller/Action
```csharp
[ApiController]
[Authorize] // Require authentication
public class MyController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "RequireScope")] // Require specific scope
    public IActionResult GetData() { /* ... */ }
}
```

### Make Authenticated API Call
```bash
curl -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
     https://localhost:7111/requests
```

### JavaScript API Call
```javascript
const response = await fetch('/requests', {
    headers: {
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json'
    }
});
```

---

## ⚙️ Configuration Snippets

### Add New OAuth2 Client
```csharp
// In AuthDataSeed.cs
if (await manager.FindByClientIdAsync("my-client") is null)
{
    await manager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = "my-client",
        ClientSecret = "my-secret", // Only for confidential clients
        DisplayName = "My Application",
        ClientType = OpenIddictConstants.ClientTypes.Public, // or Confidential
        
        RedirectUris = {
            new Uri("https://localhost:3000/callback")
        },
        
        Permissions = {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.OpenId,
            OpenIddictConstants.Permissions.Scopes.Profile
        },
        
        Requirements = {
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange // PKCE
        }
    });
}
```

### Add Custom Scope
```csharp
// In OpenIddictModule.cs
options.RegisterScopes(
    OpenIddictConstants.Scopes.OpenId,
    OpenIddictConstants.Scopes.Profile,
    OpenIddictConstants.Scopes.Email,
    "custom:scope" // Your custom scope
);
```

### Authorization Policy
```csharp
// In Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireScope", policy =>
        policy.RequireClaim("scope", "custom:scope"));
});
```

---

## 🔍 Database Queries

### Check Registered Clients
```sql
SELECT ClientId, DisplayName, Type, Permissions 
FROM OpenIddictApplications;
```

### View Active Tokens
```sql
SELECT Type, Subject, ExpirationDate, Status
FROM OpenIddictTokens 
WHERE ExpirationDate > GETUTCDATE()
ORDER BY CreationDate DESC;
```

### Check User Accounts
```sql
SELECT UserName, Email, LockoutEnabled, LastLoginDate
FROM AspNetUsers;
```

### View Authorizations
```sql
SELECT a.DisplayName, o.Subject, o.Scopes, o.Status
FROM OpenIddictAuthorizations o
JOIN OpenIddictApplications a ON o.ApplicationId = a.Id;
```

---

## 🧪 Testing & Debugging

### Decode JWT Token
```powershell
# PowerShell
$token = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
$parts = $token.Split('.')
$payload = $parts[1]
$padded = $payload + ("=" * (4 - ($payload.Length % 4)))
$decoded = [System.Convert]::FromBase64String($padded)
[System.Text.Encoding]::UTF8.GetString($decoded)
```

```bash
# Or use online tool: https://jwt.io
```

### Test Token Validation
```bash
# Valid token should return data
curl -H "Authorization: Bearer VALID_TOKEN" https://localhost:7111/requests

# Invalid token should return 401
curl -H "Authorization: Bearer invalid-token" https://localhost:7111/requests

# Expired token should return 401
curl -H "Authorization: Bearer EXPIRED_TOKEN" https://localhost:7111/requests
```

### Check Certificate Issues
```bash
# View certificate details
openssl x509 -in certificate.pem -text -noout

# Test HTTPS connection
curl -v https://localhost:7111/connect/token
```

---

## 🚨 Common Error Codes

| HTTP Code | OAuth2 Error | Description | Solution |
|-----------|--------------|-------------|----------|
| `400` | `invalid_request` | Malformed request | Check required parameters |
| `400` | `invalid_grant` | Invalid auth code/refresh token | Get new authorization |
| `400` | `unsupported_grant_type` | Grant type not allowed | Check client permissions |
| `401` | `invalid_token` | Token invalid/expired | Refresh or re-authenticate |
| `403` | `insufficient_scope` | Missing required scope | Request proper scopes |

---

## 📋 Security Checklist

### Development
- [ ] HTTPS enabled (`https://localhost:7111`)
- [ ] Strong admin password set
- [ ] PKCE enabled for public clients
- [ ] Anti-forgery tokens on forms

### Production
- [ ] Real certificates (not development certs)
- [ ] Anonymous clients disabled
- [ ] CORS restricted to specific domains
- [ ] Client secrets in secure storage
- [ ] Token encryption enabled
- [ ] Security headers configured
- [ ] Rate limiting implemented

---

## 🔧 Troubleshooting Commands

### Clear Authentication State
```bash
# Clear browser cookies and local storage
# Or use incognito/private browsing
```

### Reset Database
```bash
# Drop and recreate database
docker compose down
docker volume rm collateral-appraisal-system-api_sqlserver-data
docker compose up -d
dotnet ef database update --project Modules/Auth/OAuth2OpenId --startup-project Bootstrapper/Api
```

### Check Logs
```bash
# Application logs
tail -f Logs/log-development-*.txt

# Docker logs
docker compose logs -f

# SQL Server logs
docker logs collateral-appraisal-system-api-sqlserver-1
```

### Test Configuration
```bash
# Check OpenIddict discovery document
curl https://localhost:7111/.well-known/openid_configuration

# Test database connection
dotnet ef dbcontext info --project Modules/Auth/OAuth2OpenId --startup-project Bootstrapper/Api
```

---

## 📚 Flow Decision Tree

```
Need authentication?
├─ User-facing app?
│  ├─ Has secure backend? → Authorization Code Flow
│  └─ SPA/Mobile? → Authorization Code Flow + PKCE
└─ Service-to-service? → Client Credentials Flow

Token expired?
├─ Have refresh token? → Refresh Token Flow
└─ No refresh token? → Re-authenticate
```

---

## 🎯 Common Tasks

### Add New Protected Endpoint
1. Add `[Authorize]` attribute
2. Optionally add scope requirement
3. Test with valid/invalid tokens

### Register New Client Application
1. Add to `AuthDataSeed.cs`
2. Restart application
3. Test authentication flow

### Debug Authentication Issues
1. Check network requests in browser dev tools
2. Decode JWT tokens to inspect claims
3. Check database for client/token records
4. Review application logs

### Update Client Configuration
1. Modify `AuthDataSeed.cs`
2. Delete existing client from database
3. Restart application to re-seed

---

## 🔗 Useful URLs

- **OpenIddict Docs:** https://documentation.openiddict.com/
- **JWT Decoder:** https://jwt.io
- **OAuth2 Spec:** https://tools.ietf.org/html/rfc6749
- **PKCE Spec:** https://tools.ietf.org/html/rfc7636
- **OpenID Connect:** https://openid.net/connect/

---

## 📞 Need Help?

1. **Check logs** first - most issues show up in application logs
2. **Verify configuration** - compare with working examples
3. **Test incrementally** - isolate the failing component
4. **Use browser dev tools** - inspect network requests and responses
5. **Check database state** - verify clients and tokens are correct

This reference should handle 90% of your daily OAuth2 tasks. Bookmark it! 📌