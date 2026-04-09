# Task: Add User Claims to Access Token and Update CurrentUserService

## Summary
Add user profile claims (given_name, family_name, email, preferred_username) to the OpenIddict access token, then update CurrentUserService to extract these values from the token claims.

## Todo Items

- [x] **1. Update TokenService.cs** - Add profile claims to access token
  - Add `given_name` (FirstName)
  - Add `family_name` (LastName)
  - Add `email` (Email)
  - Add `preferred_username` (Username)

- [x] **2. Update ICurrentUserService.cs** - Add Email property to interface

- [x] **3. Update CurrentUserService.cs** - Extract claims from token
  - Fix `FirstName` to use `given_name` claim
  - Fix `LastName` to use `family_name` claim
  - Add `Email` property from `email` claim

- [x] **4. Build and verify** - Solution compiles with 0 errors

## Review

### Changes Made

#### 1. TokenService.cs (line 68-71)
Added four new claims to the access token in `CreateAuthCodeFlowAccessTokenPrincipal`:
- `given_name` - user's first name
- `family_name` - user's last name
- `email` - user's email address
- `preferred_username` - user's username

These use OpenIddict standard claim constants for interoperability.

#### 2. ICurrentUserService.cs (line 31-35)
Added the `Email` property to the interface with XML documentation.

#### 3. CurrentUserService.cs (line 21-23)
- Changed `FirstName` from hardcoded `""` to extract from `given_name` claim
- Changed `LastName` from hardcoded `""` to extract from `family_name` claim
- Added `Email` property to extract from `email` claim

### How It Works

**Token Generation Flow:**
1. When a user authenticates via the authorization code flow, `CreateAuthCodeFlowAccessTokenPrincipal` is called
2. The method loads the `ApplicationUser` from the database (already includes FirstName, LastName, Email)
3. These values are added as claims to the `ClaimsIdentity` using OpenIddict standard claim names
4. The claims are encoded into the JWT access token

**Token Consumption Flow:**
1. When an authenticated request comes in, the JWT is validated and claims are extracted
2. `CurrentUserService` accesses `HttpContext.User` (the `ClaimsPrincipal`)
3. Properties like `FirstName`, `LastName`, `Email` use `FindFirst()` to extract the claim values
4. Returns `null` if the claim doesn't exist (e.g., unauthenticated user)

### Verification
Run the API, authenticate, and call `/auth/me` endpoint - it should now return actual FirstName, LastName, and Email values from the token claims instead of empty strings.
