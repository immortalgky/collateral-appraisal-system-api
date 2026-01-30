# Auth Module & OpenIddict Analysis

## Task: Analyze Auth Module and OpenIddict Implementation

### Todo List
- [x] Explore Auth module directory structure
- [x] Analyze OpenIddict configuration
- [x] Review TokenService implementation
- [x] Review OpenIddictController endpoints
- [x] Understand permission calculation logic
- [x] Document security considerations
- [x] Create comprehensive analysis report

---

## Analysis Report

### 1. Module Architecture Overview

The Auth implementation consists of **two sub-modules**:

| Module | Path | Purpose |
|--------|------|---------|
| OAuth2OpenId | `Modules/Auth/OAuth2OpenId/` | Core OpenIddict server implementation |
| Auth | `Modules/Auth/Auth/` | High-level CQRS wrapper with Carter endpoints |

### 2. OpenIddict Configuration (`OpenIddictModule.cs`)

#### Supported OAuth2/OIDC Flows

| Flow | Method | PKCE Required |
|------|--------|---------------|
| Authorization Code | `AllowAuthorizationCodeFlow()` | Yes (line 65) |
| Client Credentials | `AllowClientCredentialsFlow()` | N/A |
| Password (Resource Owner) | `AllowPasswordFlow()` | No |
| Refresh Token | `AllowRefreshTokenFlow()` | N/A |

#### Registered Endpoints

| Endpoint | URI | Purpose |
|----------|-----|---------|
| Token | `/connect/token` | Exchange credentials for tokens |
| Authorization | `/connect/authorize` | User consent/login redirect |
| End Session | `/connect/logout` | Logout and session termination |

#### Security Configuration

```
Development Mode:
- AcceptAnonymousClients() enabled (line 73) ⚠️
- Access token encryption disabled (line 109)
- Development certificates used (lines 83-84)

Production Mode:
- Anonymous clients rejected
- Access token encryption enabled
- Requires proper certificate configuration (lines 90-101)
```

### 3. Token Service (`TokenService.cs`)

#### Authorization Code Flow Token Creation

```csharp
CreateAuthCodeFlowAccessTokenPrincipal():
1. Extract userId and username from auth code principal
2. Load user with permissions from database
3. Create ClaimsIdentity with:
   - Subject (user ID)
   - Name (username)
   - Permissions (calculated from user + roles)
   - Roles (assigned roles)
4. Set claim destinations (ID token vs Access token)
5. Return ClaimsPrincipal with requested scopes
```

#### Client Credentials Flow Token Creation

```csharp
CreateClientCredFlowAccessTokenPrincipal():
1. Look up application by ClientId
2. Create ClaimsIdentity with:
   - Subject (client ID)
   - Name (display name)
   - Audience ("resource_server")
   - Permissions (application permissions)
3. Set scopes and resources
4. Return ClaimsPrincipal
```

#### Permission Calculation Logic (`CalcUserPermissions`)

```
User's Final Permissions =
    (Granted User Permissions)
    + (Role Permissions - Explicitly Denied User Permissions)

Steps:
1. Collect permissions where UserPermission.IsGranted = true
2. Collect permissions where UserPermission.IsGranted = false (denied)
3. For each role the user has:
   - Get role's permissions
   - Add to final set (excluding explicitly denied ones)
```

This allows:
- Direct permission grants to users
- Role-based permission inheritance
- Explicit permission denial (override role grants)

### 4. OAuth2 Controller (`OpenIddictController.cs`)

#### `/connect/authorize` (GET)

```
Flow:
1. Check if user is authenticated
2. If not → Redirect to /Account/Login with ReturnUrl
3. If yes → Auto-approve (demo mode)
   - Create ClaimsIdentity with Subject and Name
   - Set claim destinations
   - SignIn with OpenIddict scheme
```

#### `/connect/token` (POST)

```
Flow:
1. Extract OpenIddict request from HttpContext
2. Validate grant_type (only authorization_code and client_credentials supported)
3. Route to appropriate handler:
   - client_credentials → HandleClientCredentialsGrant()
   - authorization_code → HandleAuthorizationCodeGrant()
4. Return SignIn result with token principal
```

### 5. Identity Model

| Entity | Key Fields | Purpose |
|--------|------------|---------|
| ApplicationUser | Id (Guid), UserName, Permissions | User identity |
| ApplicationRole | Id (Guid), Name, Description, Permissions | Role definition |
| Permission | PermissionCode (unique), Description | Permission definition |
| UserPermission | UserId, PermissionId, IsGranted | User-level grants/denials |
| RolePermission | RoleId, PermissionId | Role-permission mapping |

### 6. Database Context

- **Class**: `OpenIddictDbContext`
- **Schema**: `auth`
- **Base**: `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- **Additional DbSets**: Permissions, UserPermissions, RolePermissions

### 7. Security Considerations

#### Strengths
1. **PKCE Required** - Authorization code flow requires Proof Key for Code Exchange
2. **Anti-CSRF Protection** - X-CSRF-TOKEN header with HttpOnly cookies
3. **Password Policy** - 8+ chars, upper/lower/digit/special required
4. **Claim Destinations** - Proper separation of ID token vs Access token claims
5. **Permission Denial** - Explicit denial can override role grants

#### Areas to Review
1. **Development Mode Risks** (line 73):
   - `AcceptAnonymousClients()` allows unregistered clients
   - Should be removed/disabled in production

2. **Auto-Approve Flow** (OpenIddictController.cs:24):
   - Currently auto-approves all consent requests
   - Consider implementing proper consent UI for production

3. **Password Grant Enabled** (line 67):
   - Resource Owner Password flow is deprecated
   - Consider removing for new applications

4. **No Rate Limiting**:
   - Token endpoint has no visible rate limiting
   - Consider adding to prevent brute-force attacks

5. **Certificate Configuration** (lines 96-101):
   - Production certificate loading throws exception if config exists but isn't implemented
   - Fallback to dev certs in production (lines 103-105)

---

## Review Summary

### What Was Analyzed
- Complete Auth module structure (103 files across 2 sub-modules)
- OpenIddict server configuration and supported flows
- Token generation logic for user and client credentials flows
- Permission calculation with user/role inheritance and denial support
- OAuth2 controller endpoints

### Key Findings
1. **Well-structured** modular design following DDD patterns
2. **Security-conscious** with PKCE, CSRF protection, and password policies
3. **Flexible permissions** system supporting inheritance and explicit denials
4. **Development vs Production** separation for certificates and anonymous clients
5. **Production gaps**: consent UI, rate limiting, and certificate implementation need attention

### Files Reviewed
- `Modules/Auth/OAuth2OpenId/OpenIddictModule.cs` (136 lines)
- `Modules/Auth/OAuth2OpenId/Application/Services/TokenService.cs` (179 lines)
- `Modules/Auth/OAuth2OpenId/Application/Controllers/OpenIddictController.cs` (77 lines)

---

## Implementation: `/auth/me` Endpoint

### Task: Add Missing User Info Endpoint for Frontend

### Todo List
- [x] Read existing Auth feature structure for reference
- [x] Create Me feature folder and files
- [x] Update todo.md with implementation details
- [ ] Commit and push changes

### Files Created

```
Modules/Auth/Auth/Application/Features/Auth/Me/
├── MeQuery.cs          # CQRS query with UserId
├── MeResult.cs         # Internal result type
├── MeResponse.cs       # External response DTO
├── MeQueryHandler.cs   # Handler with permission calculation
└── MeEndpoint.cs       # Carter endpoint (GET /auth/me)
```

### Implementation Details

#### 1. MeEndpoint.cs
- **Route**: `GET /auth/me`
- **Auth**: `RequireAuthorization()` - requires valid JWT
- **Flow**:
  1. Extract `sub` claim from JWT (user ID)
  2. Send MeQuery to handler
  3. Return MeResponse with user profile

#### 2. MeQueryHandler.cs
- Loads user with permissions from database
- Gets user's roles via UserManager
- Calculates effective permissions:
  - Granted user permissions
  - Role permissions (excluding explicitly denied)
- Returns MeResult

#### 3. Response Format
```json
{
  "id": "guid",
  "username": "john.doe",
  "email": "john@example.com",
  "roles": ["Admin", "Appraiser"],
  "permissions": ["request.create", "request.approve"]
}
```

### Security Considerations
- Endpoint requires authentication (JWT Bearer token)
- User ID extracted from validated JWT claims (not user input)
- Follows existing CQRS pattern for consistency
- No sensitive data exposed (password hash, etc.)

### Usage
```bash
# Get current user info
curl -X GET https://localhost:7111/auth/me \
  -H "Authorization: Bearer {access_token}"
```

### Review
- Follows existing Auth module CQRS pattern
- Uses same permission calculation logic as TokenService
- Minimal code, maximum reuse
- No security vulnerabilities introduced
