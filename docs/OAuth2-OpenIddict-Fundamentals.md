# OAuth2 and OpenIddict Fundamentals

This document provides a comprehensive introduction to OAuth2, OpenID Connect, and the OpenIddict implementation used in our Collateral Appraisal System.

## Table of Contents

1. [What is OAuth2?](#what-is-oauth2)
2. [What is OpenID Connect?](#what-is-openid-connect)
3. [Key Terminology](#key-terminology)
4. [OAuth2 Grant Types](#oauth2-grant-types)
5. [OpenIddict Framework](#openiddict-framework)
6. [Tokens Explained](#tokens-explained)
7. [Flow Diagrams](#flow-diagrams)
8. [When to Use Each Flow](#when-to-use-each-flow)

---

## What is OAuth2?

OAuth2 is an **authorization framework** that enables applications to obtain limited access to user accounts. It works by delegating user authentication to the service that hosts the user account and authorizing third-party applications to access the user account.

### Key Concepts:

- **Not an authentication protocol** - OAuth2 is about authorization (what you can do)
- **Delegation** - Users delegate access to their resources without sharing credentials
- **Scopes** - Define what permissions are being requested
- **Tokens** - Used instead of passwords for accessing resources

### Real-World Analogy:

Think of OAuth2 like a hotel key card system:
- You (user) check into a hotel (authorization server)
- The hotel gives you a key card (access token) with specific permissions
- The key card works for your room, gym, and pool (scopes) but not the hotel safe
- The key card expires after checkout (token expiration)
- You don't need to give your credit card to every hotel service

---

## What is OpenID Connect?

OpenID Connect (OIDC) is an **identity layer** built on top of OAuth2. While OAuth2 handles authorization, OIDC adds authentication capabilities.

### Key Differences:

| OAuth2 | OpenID Connect |
|--------|----------------|
| Authorization only | Authentication + Authorization |
| Access tokens | Access tokens + ID tokens |
| Scopes for resources | Scopes for identity info |
| "What can I access?" | "Who am I?" + "What can I access?" |

### OIDC Additions:

- **ID Token** - Contains user identity information
- **UserInfo Endpoint** - Provides additional user details
- **Standard Claims** - Predefined user attributes (name, email, etc.)

---

## Key Terminology

### Core Entities

| Term | Definition | Example in Our System |
|------|------------|----------------------|
| **Resource Owner** | The user who owns the data | End user of our appraisal system |
| **Client** | Application requesting access | Our SPA frontend, mobile app |
| **Authorization Server** | Issues tokens after authentication | Our OpenIddict server |
| **Resource Server** | Hosts protected resources | Our API endpoints |

### Tokens

| Token Type | Purpose | Contents | Lifespan |
|------------|---------|----------|----------|
| **Access Token** | Authorize API calls | Scopes, permissions | Short (1 hour) |
| **Refresh Token** | Get new access tokens | Long-lived credentials | Long (days/weeks) |
| **ID Token** | User identity information | User claims, authentication details | Short (1 hour) |
| **Authorization Code** | Temporary code for token exchange | One-time use code | Very short (10 minutes) |

### Other Important Terms

- **Scope** - Permission level (e.g., `read:appraisals`, `write:requests`)
- **Grant Type** - Method of obtaining tokens
- **Claims** - Pieces of information about the user
- **Client Credentials** - ID and secret identifying the client application
- **PKCE** - Proof Key for Code Exchange (security extension)

---

## OAuth2 Grant Types

### 1. Authorization Code Flow (Most Common)

**Best for:** Web applications, SPAs, mobile apps

**How it works:**
1. User clicks "Login"
2. Redirected to authorization server
3. User authenticates and consents
4. Server returns authorization code
5. Client exchanges code for tokens

```
User → Client → Authorization Server → User → Client → Authorization Server
                    (login page)              (code)    (tokens)
```

### 2. Client Credentials Flow

**Best for:** Server-to-server communication, background services

**How it works:**
1. Client authenticates with its credentials
2. Server returns access token directly
3. No user interaction required

```
Client → Authorization Server
       ← Access Token
```

### 3. Refresh Token Flow

**Best for:** Refreshing expired access tokens

**How it works:**
1. Access token expires
2. Client sends refresh token
3. Server returns new access token

```
Client → Authorization Server (refresh token)
       ← New Access Token
```

### 4. Implicit Flow (Deprecated)

**Status:** Not recommended for security reasons
**Replaced by:** Authorization Code Flow with PKCE

---

## OpenIddict Framework

OpenIddict is a .NET implementation of OAuth2/OpenID Connect that we use in our system.

### Why OpenIddict?

- **Self-hosted** - Full control over authentication
- **Flexible** - Supports all OAuth2 flows
- **Secure** - Built-in security best practices
- **ASP.NET Core Integration** - Native middleware support
- **Entity Framework** - Database storage for applications and tokens

### Architecture in Our System

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   SPA Frontend  │    │  OpenIddict     │    │   API Endpoints │
│   (Client)      │◄──►│  Auth Server    │    │  (Resource      │
│                 │    │                 │    │   Server)       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │   SQL Server    │
                       │   (Tokens,      │
                       │    Users)       │
                       └─────────────────┘
```

### Key Components

1. **OpenIddictModule.cs** - Configuration and setup
2. **OpenIddictController.cs** - Handles OAuth2 endpoints
3. **OpenIddictDbContext.cs** - Database operations
4. **Login.cshtml.cs** - User authentication UI

---

## Tokens Explained

### Access Token Structure

Access tokens in our system are **JWT (JSON Web Tokens)** with this structure:

```json
{
  "header": {
    "alg": "RS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "user-id-123",           // Subject (user ID)
    "aud": "api-resource",          // Audience (our API)
    "iss": "https://our-auth-server", // Issuer
    "exp": 1640995200,              // Expiration
    "iat": 1640991600,              // Issued at
    "scope": "read:requests write:appraisals"
  },
  "signature": "..."
}
```

### ID Token Structure

ID tokens contain user identity information:

```json
{
  "sub": "user-id-123",
  "name": "John Doe",
  "email": "john.doe@company.com",
  "preferred_username": "admin",
  "aud": "spa",                    // Client ID
  "exp": 1640995200,
  "iat": 1640991600
}
```

### Token Validation

Our API validates tokens by:

1. **Signature verification** - Using public key
2. **Expiration check** - Ensuring token is not expired
3. **Audience verification** - Confirming token is for our API
4. **Issuer verification** - Confirming it came from our auth server

---

## Flow Diagrams

### Authorization Code Flow (Our Primary Flow)

```
┌─────────┐                                    ┌─────────────────┐
│   SPA   │                                    │  Auth Server    │
│         │                                    │  (OpenIddict)   │
└─────────┘                                    └─────────────────┘
     │                                                   │
     │ 1. GET /connect/authorize                        │
     │    ?response_type=code                           │
     │    &client_id=spa                                │
     │    &redirect_uri=callback                        │
     │    &scope=openid profile                         │
     │    &code_challenge=xyz                           │
     │────────────────────────────────────────────────►│
     │                                                  │
     │ 2. 302 Redirect to /Account/Login                │
     │◄─────────────────────────────────────────────────│
     │                                                  │
     │ 3. POST /Account/Login                           │
     │    username=admin&password=P@ssw0rd!             │
     │────────────────────────────────────────────────►│
     │                                                  │
     │ 4. 302 Redirect with authorization code          │
     │    https://spa/callback?code=abc123              │
     │◄─────────────────────────────────────────────────│
     │                                                  │
     │ 5. POST /connect/token                           │
     │    grant_type=authorization_code                 │
     │    &code=abc123                                  │
     │    &code_verifier=xyz                            │
     │────────────────────────────────────────────────►│
     │                                                  │
     │ 6. Access Token + ID Token                       │
     │◄─────────────────────────────────────────────────│
     │                                                  │

┌─────────┐                                    ┌─────────────────┐
│   SPA   │                                    │   API Server    │
└─────────┘                                    └─────────────────┘
     │                                                   │
     │ 7. GET /requests                                  │
     │    Authorization: Bearer [access_token]           │
     │────────────────────────────────────────────────►│
     │                                                  │
     │ 8. Protected resource data                       │
     │◄─────────────────────────────────────────────────│
```

### Client Credentials Flow

```
┌─────────────────┐                           ┌─────────────────┐
│  Background     │                           │  Auth Server    │
│  Service        │                           │  (OpenIddict)   │
└─────────────────┘                           └─────────────────┘
         │                                             │
         │ 1. POST /connect/token                      │
         │    grant_type=client_credentials            │
         │    &client_id=service                       │
         │    &client_secret=secret                    │
         │    &scope=api:process                       │
         │───────────────────────────────────────────►│
         │                                             │
         │ 2. Access Token                             │
         │◄────────────────────────────────────────────│
         │                                             │
         │ 3. Use token to call APIs                   │
         │                                             │
```

---

## When to Use Each Flow

### Decision Tree

```
Is this a user-facing application?
├─ YES: Does it have a backend server?
│  ├─ YES: Authorization Code Flow (traditional web app)
│  └─ NO: Authorization Code Flow + PKCE (SPA, mobile)
│
└─ NO: Is this service-to-service communication?
   └─ YES: Client Credentials Flow
```

### Our System Usage

| Application Type | Flow Used | Example |
|------------------|-----------|---------|
| **SPA Frontend** | Authorization Code + PKCE | React/Vue app |
| **Mobile App** | Authorization Code + PKCE | iOS/Android app |
| **Background Service** | Client Credentials | Automated appraisal processor |
| **Admin Dashboard** | Authorization Code | Server-rendered admin UI |

### Security Considerations by Flow

| Flow | Security Features | Use Cases |
|------|------------------|-----------|
| **Authorization Code + PKCE** | ✅ Most secure<br/>✅ PKCE prevents code interception<br/>✅ Short-lived codes | Public clients (SPA, mobile) |
| **Client Credentials** | ✅ Direct client authentication<br/>⚠️ Requires secure client secret storage | Trusted server environments |
| **Refresh Token** | ✅ Reduces auth server requests<br/>⚠️ Long-lived, must be stored securely | All interactive flows |

---

## Summary

### Key Takeaways

1. **OAuth2** is for authorization, **OpenID Connect** adds authentication
2. **Authorization Code Flow** is the most secure for user-facing apps
3. **PKCE** is essential for public clients (SPAs, mobile apps)
4. **Access tokens** are short-lived, **refresh tokens** are long-lived
5. **OpenIddict** provides a complete OAuth2/OIDC implementation for .NET

### Next Steps

After understanding these fundamentals, you should:

1. Review our implementation in `OAuth2-Implementation-Guide.md`
2. Understand security best practices
3. Practice with hands-on exercises
4. Learn troubleshooting techniques

### Questions to Consider

- How does our SPA frontend obtain tokens?
- What scopes does our API require?
- How do we handle token expiration?
- What happens when a user logs out?

These questions will be answered as we dive into the implementation details in the next document.