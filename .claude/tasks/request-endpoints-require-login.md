# Require login on request endpoints

## Todo
- [x] 1. Remove `.AllowAnonymous()` from the Request, RequestComment, RequestDocument (list) and Reappraisal endpoints so the fallback policy applies
- [x] 2. `DELETE /requests/{id}` returns the declared `DeleteRequestResponse` (`{ isSuccess }`) instead of a bare bool
- [x] 3. Drop the now-redundant `.RequireAuthorization()` on reappraisal `restore` (one way of expressing the rule)
- [x] 4. `AnonymousEndpointsTests`: assert the set of endpoints carrying `IAllowAnonymous` equals an explicit allowlist, plus one live `GET /requests` 401; `/documents/{id}/download` stays anonymous
- [x] 5. Also remove `.AllowAnonymous()` from `DELETE /parameter/{parId:long}`, `POST /api/workflows/definitions`, `PUT /market-comparables/{id}` and `DELETE /market-comparable/{id}`  after checking every caller; add them to `AnonymousEndpointsTests`
- [x] 6. `AnonymousEndpointsTests` also enumerates every `RouteEndpoint` and asserts the set carrying `IAllowAnonymous` equals an explicit, commented allowlist
- [x] 7. Add `X-Dev-Auth: dev-bypass` to the 20 scratch requests in 16 `httpRequests/*.http` files (19 hit the now login-only routes; `CreateRequest.http` uses `@baseUrl .../api/v1`, i.e. the Integration module's `/api/v1/requests`, so its header is harmless but unrelated). Also fixed wrong verbs in `SubmitRequest.http` (POST), `UpdateDraftRequest.http`, `UpdateRequest.http` and `Clear.http` (PUT)
- [x] 8. Full-solution test run

## Review

### What changed
- Removed `.AllowAnonymous()` from 22 endpoints (8 Requests, 5 RequestComments, 1 RequestDocuments list, 4 Reappraisal, plus the four below). `AuthModule` sets a fallback policy of `RequireAuthenticatedUser()`, so they now require login with no per-endpoint policy.
- `DeleteRequestEndpoint` now returns `new DeleteRequestResponse(result.IsSuccess)`, matching its `.Produces<DeleteRequestResponse>()` and the FE's generated schema (user-approved). `DeleteRequestTests` reads the object again.
- Reappraisal `restore` no longer carries its own `.RequireAuthorization()`; all five routes rely on the fallback.
- New `Tests/Integration/Security/AnonymousEndpointsTests.cs`.

### Why
- Anonymous `GET /requests` returned 200 with customer names. The `.AllowAnonymous()` calls date from refactor `192d6029`.
- The FE calls all of these through an axios instance that always sends `Authorization: Bearer`; no server-side code, job or script calls them over HTTP.
- `GET /documents/{id}/download` deliberately stays anonymous: files opened in a new browser tab cannot send a Bearer header.

### Parameter / workflow-definition / market-comparable routes: caller evidence
- `DELETE /parameter/{parId:long}`: only FE call site is `parameterMaintenance/api/parameter.ts` (`useDeleteParameter`, TH + EN rows), through `@shared/api/axiosInstance`, which attaches `Authorization: Bearer`.
- `PUT /market-comparables/{id}`: only FE call site is `appraisal/api/marketComparable.ts` (`useUpdateMarketComparable`), through `axiosInstance`.
- `DELETE /market-comparable/{id}` (singular, path unchanged): no caller anywhere (BE, FE source on origin/main, `.http` files, docs). The FE's `useDeleteMarketComparable` calls the plural `/market-comparables/{id}`, which has no DELETE route, so the delete never reaches the handler.
- `POST /api/workflows/definitions`: no FE caller. Only manual dev files `httpRequests/Workflow/Create*WorkflowDefinition.http` (4), which send `X-Dev-Auth: dev-bypass`; that handler authenticates the request, so they keep working locally. Definitions are loaded by the `*WorkflowDefinitionSeeder` classes, not over HTTP.
- No server-side `HttpClient`, Hangfire job, seeder, DbUp/migration script or deploy script calls any of the four (the only loopback client, `"CAS"`, calls `/connect/token`). The FE has no raw `fetch`, raw axios request, form action or `sendBeacon` to these paths. No tests referenced them.
- None had an explicit `.RequireAuthorization(...)`, so nothing conflicts with the fallback policy.

### Left as-is by decision
- Decided / accepted: dev-bypass header enabled on SIT/UAT, accepted 2026-10-07 (UAT is test data only); revisit if any non-Production env gets real data.
- `POST /requests` returns 500 on a malformed body (user decision).
- The `?? "anonymous"` fallbacks in `DeleteRequestCommandHandler` / `RemoveRequestCommentCommandHandler` stay as defensive code.

### Open / out of scope
- Reappraisal routes are login-only but not permission-gated (`REAPPRAISAL_VIEW` exists and is unenforced); `InitiateReappraisal` takes Requestor/Creator from the request body.
- Comment remove/update and request delete/update/submit have no ownership checks.
- Market comparable delete path mismatch (FE plural vs API singular `/market-comparable/{id}`) is deliberately NOT fixed: it would make the FE delete reachable for the first time, and the handler soft-deletes a pool comparable that appraisals/pricing may still link to. Fixing the path needs, in the same change, an in-use guard (AppraisalComparable / PricingComparableLink), a permission, an IsDeleted check, and the DeleteMarketComparableResponse shape.
- The delete-market-comparable endpoint returns a bare bool while declaring `DeleteMarketComparableResponse` (same mismatch DeleteRequest had); not changed here.
- `CreateWorkflowDefinition` takes `CreatedBy` from the request body and has no permission check.
- `DeleteParameter` returns a bare bool while declaring `DeleteParameterResponse`.
- The four routes above are login-only, not permission-gated (the Parameter menu is gated by `PARAMETER_MANAGE` in the FE only).
- Still anonymous (pinned by the allowlist in `AnonymousEndpointsTests`; any new anonymous endpoint fails that test): `/auth/token`, `/auth/refresh`, `/documents/{id}/download`, legacy AS400 `POST /api/v1/appraisals/result`, OpenIddict `connect/*` and `Account/*` pages, OpenAPI/Scalar, `/health*`, and the Hangfire dashboard in Development only.
