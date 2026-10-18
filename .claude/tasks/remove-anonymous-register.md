# Remove anonymous `POST /auth/register`

## Todo
- [x] Re-verify callers of `/auth/register` and the RegisterUser types (BE, tests, `.http`, docs, deploy, FE `origin/main`)
- [x] Delete `Modules/Auth/Auth/Application/Features/Auth/RegisterUser/` (endpoint, request, response, command, handler, result, validator)
- [x] Delete `httpRequests/RegisterUser.http`
- [x] Drop `RegisterUserCommand` from `PasswordCommandRedactionTests`
- [x] Remove the `/auth/register` operation and `RegisterUser*` schemas from `docs/v1.json`; strike the WBS line in `docs/data-model/20-implementation-wbs.md`
- [x] Integration test `Tests/Integration/Auth.Integration.Tests/AnonymousAccountCreationTests.cs` (route table has no `/auth/register`)
- [x] Build solution, run `Auth.Tests` and the filtered Auth integration run

## Review
**What / why:** `/auth/register` was `.AllowAnonymous()` and accepted `Roles` and `Permissions`, so any caller who could reach
the API could create an Admin account. Deleted outright (requiring login would still let any external-company user grant
themselves Admin). Admin user creation stays on `Users/CreateUser` (`POST /auth/users`).

**Callers checked:** nothing else calls it. BE: only `RegisterUserDto`/`RegisterUserPermissionDto` (different types, used by
`CreateUserCommandHandler` and `RegistrationService`) and the redaction test. FE `origin/main`: only the generated
`src/shared/schemas/api.client.json` and `v1.ts`. Docs: the WBS line and the generated OpenAPI export `docs/v1.json`, both
edited in this change.

**Kept:** `IRegistrationService`/`RegistrationService`, `RegisterUserDto`, `RegisterUserPermissionDto`. `RegistrationService` now
has one caller, `CreateUserCommandHandler`; LDAP login never creates users.

**Test:** the route table is checked rather than a status code, because an unmatched path may be answered 401 by a fallback
policy. The anonymous 401 guard for `/auth/users` belongs to the stacked anonymous-endpoints PR.

**Deferred (not changed):** the now-dead `RegisterUserDto.Permissions`/`AvatarUrl`/`RegisterUserPermissionDto` path in
`RegistrationService` (CreateUser passes `Permissions=[]`), and inlining/renaming `IRegistrationService` (one caller left).

## Out of scope / follow-up
Post-incident audit for accounts self-registered before deploy. Draft queries are saved; review findings to cover: second-generation
accounts created by suspects, actor changes in `AuthAuditLogs`, `CompanyId`, deny rows, broader privilege codes, `IsInRole`
checks, and a `deploy/README.md` runbook entry. Direct `UserPermission` grants are still honoured at login and nothing in the
admin UI can revoke them.
