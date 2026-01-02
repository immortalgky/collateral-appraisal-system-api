# OAuth2 Security Best Practices

## Critical Security Guidelines for OAuth2/OpenIddict

Security is paramount in authentication systems. This guide covers essential security practices, common vulnerabilities, and how our implementation addresses them.

## Table of Contents

1. [Security Overview](#security-overview)
2. [Common OAuth2 Vulnerabilities](#common-oauth2-vulnerabilities)
3. [PKCE - Proof Key for Code Exchange](#pkce---proof-key-for-code-exchange)
4. [Token Security](#token-security)
5. [Client Security](#client-security)
6. [Transport Security](#transport-security)
7. [Production Security Checklist](#production-security-checklist)
8. [Security Testing](#security-testing)

---

## Security Overview

### OAuth2 Security Principles

1. **Defense in Depth** - Multiple layers of security
2. **Least Privilege** - Minimal necessary permissions
3. **Secure by Default** - Safe defaults, explicit insecure options
4. **Fail Securely** - Secure behavior when errors occur

### Threat Model

Common threats OAuth2 protects against:

| Threat | Description | Mitigation |
|--------|-------------|------------|
| **Credential Theft** | Password/token interception | Token-based auth, short expiration |
| **Session Hijacking** | Stealing user sessions | HTTPS, secure cookies, CSRF tokens |
| **Authorization Code Interception** | Stealing auth codes | PKCE, HTTPS, short expiration |
| **Token Replay** | Reusing captured tokens | Token binding, audience validation |
| **Phishing** | Fake authorization servers | Certificate validation, HTTPS |

---

## Common OAuth2 Vulnerabilities

### 1. Authorization Code Interception

**Problem:** Malicious apps can intercept authorization codes

**Example Attack:**
```
1. Legitimate app redirects to: myapp://callback?code=abc123
2. Malicious app registers same redirect URI
3. Malicious app receives the code
4. Malicious app exchanges code for tokens
```

**Our Protection:**
```csharp
// PKCE is required for all public clients
options.AllowAuthorizationCodeFlow()
       .RequireProofKeyForCodeExchange();  // ✅ Mandatory PKCE

Requirements = {
    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
}
```

### 2. Cross-Site Request Forgery (CSRF)

**Problem:** Malicious sites trigger unauthorized actions

**Example Attack:**
```html
<!-- Malicious site auto-submits form -->
<form action="https://oauth-server/connect/authorize" method="POST">
    <input name="response_type" value="code">
    <input name="client_id" value="victim-app">
</form>
<script>document.forms[0].submit();</script>
```

**Our Protection:**
```csharp
// Anti-forgery tokens on all forms
[ValidateAntiForgeryToken]
public async Task<IActionResult> OnPostAsync()

// Secure cookie configuration
services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;           // ✅ Prevent XSS access
    options.Cookie.SameSite = SameSiteMode.Strict;  // ✅ Prevent CSRF
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
```

### 3. Open Redirect Vulnerabilities

**Problem:** Authorization server redirects to malicious URLs

**Example Attack:**
```
GET /connect/authorize?redirect_uri=https://evil.com/steal-tokens
```

**Our Protection:**
```csharp
// Strictly validate redirect URIs
public void OnGet(string returnUrl = null)
{
    ReturnUrl = returnUrl ?? "/";
}

// Validate return URLs
if (!Url.IsLocalUrl(redirectUrl) && !redirectUrl.StartsWith("https://"))
{
    logger.LogWarning("Invalid return URL: {ReturnUrl}", redirectUrl);
    redirectUrl = "/";  // ✅ Safe fallback
}
```

### 4. Token Leakage

**Problem:** Tokens exposed in logs, URLs, or client storage

**Common Mistakes:**
```javascript
// ❌ Never put tokens in URLs
window.location.href = `/dashboard?token=${accessToken}`;

// ❌ Never log tokens
console.log('Access token:', accessToken);

// ❌ Insecure storage
sessionStorage.setItem('token', accessToken);  // XSS vulnerable
```

**Secure Practices:**
```javascript
// ✅ Secure storage (still has risks in SPA)
localStorage.setItem('access_token', token);

// ✅ Use httpOnly cookies when possible (backend apps)
// Set-Cookie: access_token=xyz; HttpOnly; Secure; SameSite=Strict

// ✅ Never log sensitive data
logger.LogInformation("User {UserId} authenticated", userId);  // Not the token!
```

---

## PKCE - Proof Key for Code Exchange

### What is PKCE?

PKCE prevents authorization code interception attacks by cryptographically linking the authorization request with the token request.

### How PKCE Works

```
1. Client generates code_verifier (random string)
2. Client creates code_challenge = SHA256(code_verifier)
3. Authorization request includes code_challenge
4. Auth server stores code_challenge with auth code
5. Token request includes code_verifier
6. Auth server validates SHA256(code_verifier) == code_challenge
```

### Implementation in Our System

**Frontend (SPA):**
```javascript
// 1. Generate PKCE parameters
function generateCodeVerifier() {
    const array = new Uint32Array(56/2);
    crypto.getRandomValues(array);
    return Array.from(array, dec => ('0' + dec.toString(16)).substr(-2)).join('');
}

function generateCodeChallenge(verifier) {
    return base64URLEncode(sha256(verifier));
}

// 2. Authorization request with PKCE
const codeVerifier = generateCodeVerifier();
const codeChallenge = generateCodeChallenge(codeVerifier);

const authUrl = `https://localhost:7111/connect/authorize?` +
    `response_type=code&` +
    `client_id=spa&` +
    `code_challenge=${codeChallenge}&` +
    `code_challenge_method=S256`;  // ✅ SHA256 hashing

// 3. Token request with verifier
const tokenResponse = await fetch('/connect/token', {
    method: 'POST',
    body: new URLSearchParams({
        grant_type: 'authorization_code',
        code: authorizationCode,
        code_verifier: codeVerifier  // ✅ Proves client identity
    })
});
```

**Backend Configuration:**
```csharp
// PKCE is mandatory for public clients
options.AllowAuthorizationCodeFlow()
       .RequireProofKeyForCodeExchange();

Requirements = {
    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange  // ✅ Required
}
```

### PKCE Security Benefits

- ✅ **Authorization Code Protection** - Even if intercepted, code is useless without verifier
- ✅ **No Client Secret Required** - Safe for public clients (SPAs, mobile)
- ✅ **Standards Compliant** - RFC 7636 specification
- ✅ **Backward Compatible** - Works with existing OAuth2 flows

---

## Token Security

### Token Types and Lifespans

| Token Type | Purpose | Recommended Lifespan | Storage |
|------------|---------|---------------------|---------|
| **Authorization Code** | Temporary exchange token | 10 minutes | Server-side only |
| **Access Token** | API access | 1-24 hours | Client memory/storage |
| **Refresh Token** | Token renewal | 30-90 days | Secure client storage |
| **ID Token** | User identity | 1 hour | Client verification only |

### Access Token Security

**JWT Structure:**
```javascript
// Header
{
  "alg": "RS256",  // ✅ Strong signing algorithm
  "typ": "JWT"
}

// Payload
{
  "sub": "user-123",
  "aud": "api-resource",      // ✅ Audience validation
  "iss": "https://auth-server", // ✅ Issuer validation
  "exp": 1640995200,          // ✅ Expiration time
  "iat": 1640991600,          // ✅ Issued at time
  "jti": "token-id-456",      // ✅ Unique token ID
  "scope": "read:requests"    // ✅ Limited permissions
}
```

**Validation Checklist:**
```csharp
// API token validation
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7111";       // ✅ Trusted issuer
        options.Audience = "api-resource";                  // ✅ Intended audience
        options.RequireHttpsMetadata = true;                // ✅ HTTPS only
        options.ClockSkew = TimeSpan.FromMinutes(5);        // ✅ Limited clock skew
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,                          // ✅ Verify issuer
            ValidateAudience = true,                        // ✅ Verify audience
            ValidateLifetime = true,                        // ✅ Check expiration
            ValidateIssuerSigningKey = true,                // ✅ Verify signature
            ClockSkew = TimeSpan.FromMinutes(5)            // ✅ Account for clock drift
        };
    });
```

### Refresh Token Security

**Best Practices:**
```csharp
// Refresh token configuration
options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));     // ✅ Limited lifetime
options.SetAccessTokenLifetime(TimeSpan.FromHours(1));     // ✅ Short access token life

// Refresh token rotation
services.Configure<OpenIddictServerOptions>(options =>
{
    options.UseRollingRefreshTokens = true;  // ✅ New refresh token on each use
});
```

**Refresh Flow Security:**
```csharp
// Validate refresh token
if (request.IsRefreshTokenGrantType())
{
    var refreshToken = request.RefreshToken;
    
    // Validate token hasn't been revoked
    var tokenEntry = await tokenManager.FindByIdAsync(tokenId);
    if (tokenEntry?.Status != OpenIddictConstants.Statuses.Valid)
    {
        return BadRequest(new { error = "invalid_grant" });
    }
    
    // Issue new tokens and revoke old refresh token
    await tokenManager.TryRevokeAsync(tokenEntry);  // ✅ Revoke old token
}
```

---

## Client Security

### Public vs Confidential Clients

| Client Type | Has Secret | Examples | Security Model |
|-------------|------------|----------|----------------|
| **Public** | No | SPAs, Mobile Apps | PKCE, short-lived tokens |
| **Confidential** | Yes | Server apps, APIs | Client authentication |

### Client Registration Security

**Our SPA Client (Public):**
```csharp
await manager.CreateAsync(new OpenIddictApplicationDescriptor
{
    ClientId = "spa",
    ClientType = OpenIddictConstants.ClientTypes.Public,    // ✅ No secret
    
    // Strict redirect URI validation
    RedirectUris = {
        new Uri("https://localhost:7111/callback"),         // ✅ Exact matches only
        new Uri("https://localhost:3000/callback")
    },
    
    // Limited permissions
    Permissions = {
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.ResponseTypes.Code
        // ✅ No implicit or password grants
    },
    
    Requirements = {
        OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange  // ✅ PKCE mandatory
    }
});
```

### Client Secret Management (Confidential Clients)

**Secure Secret Handling:**
```csharp
// ❌ Never hardcode secrets
ClientSecret = "hardcoded-secret";

// ✅ Use configuration
ClientSecret = configuration["OAuth2:Clients:BackgroundService:Secret"];

// ✅ Use Azure Key Vault / HashiCorp Vault
ClientSecret = await keyVault.GetSecretAsync("oauth-client-secret");

// ✅ Rotate secrets regularly
services.AddHostedService<ClientSecretRotationService>();
```

---

## Transport Security

### HTTPS Requirements

**Development Configuration:**
```csharp
if (environment == "Development")
{
    // ⚠️ Development only - allows HTTP
    options.UseAspNetCore()
        .DisableTransportSecurityRequirement();
}
else
{
    // ✅ Production - HTTPS required
    options.UseAspNetCore()
        .EnableTransportSecurityRequirement();
}
```

### Certificate Security

**Production Certificates:**
```csharp
// ❌ Never use development certificates in production
options.AddDevelopmentEncryptionCertificate();
options.AddDevelopmentSigningCertificate();

// ✅ Use proper certificates
options.AddSigningCertificate(GetSigningCertificate());
options.AddEncryptionCertificate(GetEncryptionCertificate());

private X509Certificate2 GetSigningCertificate()
{
    // Load from secure storage
    var certData = await keyVault.GetSecretAsync("signing-cert");
    return new X509Certificate2(Convert.FromBase64String(certData));
}
```

### Headers and Policies

**Security Headers:**
```csharp
app.Use(async (context, next) =>
{
    // ✅ Security headers
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // ✅ HSTS (HTTPS Strict Transport Security)
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains");
    }
    
    await next();
});
```

---

## Production Security Checklist

### Before Deployment

#### Server Configuration
- [ ] **Real certificates configured** (not development certificates)
- [ ] **HTTPS enforced** (`RequireHttpsMetadata = true`)
- [ ] **Anonymous clients disabled** (`AcceptAnonymousClients()` removed)
- [ ] **Strong passwords enforced** in Identity configuration
- [ ] **Account lockout enabled** with reasonable limits
- [ ] **Token encryption enabled** in production

#### Client Configuration  
- [ ] **Redirect URIs validated** (exact matches, no wildcards)
- [ ] **PKCE required** for all public clients
- [ ] **Scopes minimized** (principle of least privilege)
- [ ] **Client secrets secured** (for confidential clients)

#### Database Security
- [ ] **Connection strings encrypted** in production
- [ ] **Database access restricted** (firewall, VPN)
- [ ] **Backup encryption enabled**
- [ ] **Audit logging configured**

#### Monitoring & Logging
- [ ] **Security events logged** (failed logins, token errors)
- [ ] **No sensitive data in logs** (tokens, passwords)
- [ ] **Monitoring alerts configured** (suspicious activity)
- [ ] **Log retention policies** defined

### Runtime Security

#### Token Management
- [ ] **Short access token lifetimes** (1-24 hours)
- [ ] **Refresh token rotation** enabled
- [ ] **Token revocation** implemented
- [ ] **Token binding** considered for high-security scenarios

#### API Protection
- [ ] **All endpoints authenticated** (except public ones)
- [ ] **Rate limiting** implemented
- [ ] **Input validation** on all endpoints
- [ ] **CORS properly configured** (not `*` in production)

---

## Security Testing

### Automated Security Tests

**Token Validation Tests:**
```csharp
[Test]
public async Task AccessToken_ShouldRejectExpiredToken()
{
    // Arrange
    var expiredToken = GenerateExpiredToken();
    
    // Act
    var response = await client.GetAsync("/requests", 
        new AuthenticationHeaderValue("Bearer", expiredToken));
    
    // Assert
    Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Test]
public async Task AccessToken_ShouldRejectInvalidAudience()
{
    // Arrange
    var tokenWithWrongAudience = GenerateTokenWithAudience("wrong-api");
    
    // Act & Assert
    var response = await client.GetAsync("/requests", 
        new AuthenticationHeaderValue("Bearer", tokenWithWrongAudience));
    Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

### Manual Security Testing

**PKCE Flow Test:**
```bash
# 1. Start authorization without PKCE (should fail)
curl "https://localhost:7111/connect/authorize?response_type=code&client_id=spa&redirect_uri=https://localhost:3000/callback"

# 2. Start authorization with PKCE (should succeed)
curl "https://localhost:7111/connect/authorize?response_type=code&client_id=spa&redirect_uri=https://localhost:3000/callback&code_challenge=xyz&code_challenge_method=S256"
```

**Token Security Test:**
```bash
# Test expired token (should return 401)
curl -H "Authorization: Bearer EXPIRED_TOKEN" https://localhost:7111/requests

# Test token with wrong audience (should return 401)
curl -H "Authorization: Bearer WRONG_AUDIENCE_TOKEN" https://localhost:7111/requests

# Test malformed token (should return 401)
curl -H "Authorization: Bearer invalid-token" https://localhost:7111/requests
```

### Penetration Testing Focus Areas

1. **Authorization bypass attempts**
2. **Token manipulation** (modify claims, change audience)
3. **PKCE bypass** attempts
4. **Redirect URI manipulation**
5. **CSRF attacks** on authorization endpoints
6. **Session fixation** attacks
7. **Timing attacks** on token validation

---

## Summary

### Security Layers in Our System

1. **Transport Security** - HTTPS, secure headers, HSTS
2. **Authentication** - Strong passwords, account lockout, MFA ready
3. **Authorization** - JWT validation, audience checking, scope enforcement  
4. **Token Security** - Short lifetimes, signature validation, secure storage
5. **CSRF Protection** - Anti-forgery tokens, SameSite cookies
6. **Code Injection** - PKCE, redirect URI validation
7. **Monitoring** - Security logging, alert systems

### Key Security Principles Applied

- ✅ **Defense in Depth** - Multiple security layers
- ✅ **Principle of Least Privilege** - Minimal necessary permissions
- ✅ **Secure by Default** - Safe configurations out of the box
- ✅ **Fail Securely** - Safe behavior on errors
- ✅ **Regular Updates** - Keep dependencies current
- ✅ **Security Monitoring** - Log and alert on security events

Your junior developer should now understand both the security threats OAuth2 addresses and how our implementation protects against them. Security is not optional - it must be built into every aspect of the authentication system.