# Security & Quality Review — All PRs + SonarCloud Hotspots

**Date:** 2026-06-08
**Scope:** All 198 PRs (#1–#241) on `immortalgky/collateral-appraisal-system-api` + 82 open SonarCloud security hotspots, verified against current `main`.
**Lenses:** (1) SonarCloud security hotspots · (2) high-priority GitHub Copilot review comments · (3) critical business-domain mismatches.

## Method
- Pulled all 788 Copilot inline review comments across 140 PRs (`gh api .../pulls/{n}/comments`, author `Copilot`).
- Keyword-triaged to 325 high-priority candidates (security / correctness / domain), dropping docs `.md` and style nits.
- Each candidate **re-verified against current `main`** by 6 parallel agents — a comment on a merged PR may have been fixed before merge, may be live, or may be a false positive. Only **STILL_PRESENT** and **NEEDS_JUDGMENT** items are reported below.
- SonarCloud hotspots fetched from the public API (`/api/hotspots/search?projectKey=immortalgky_collateral-appraisal-system-api&status=TO_REVIEW`).

## Executive summary
| Lens | Reviewed | Still live | Already fixed / false-positive / N-A |
|---|---|---|---|
| Copilot comments | 788 (325 triaged) | ~104 still-present + ~29 needs-judgment | majority fixed-before-merge or N/A |
| SonarCloud hotspots | 82 | ~10 shipped-code worth acting on | 38 SQL FP + ~31 docs/test tooling + by-design |

**Top risks (act first):**
1. **2× fully-unauthenticated endpoints** (`.AllowAnonymous()` bypassing the global `RequireAuthenticatedUser` fallback): document **download** (IDOR by GUID) and integration **resubmit** (state-mutating).
2. **Hangfire dashboard authorization returns `true` by default in production** (`HangfireExtensions`).
3. **3× endpoints missing their specific auth policy** (commented-out `RequireAuthorization`): `DeleteDocument`, integration `GetParameters`, `GetQuotationValuers` — authenticated but unscoped.
4. **3× Auth admin read endpoints not permission-scoped** (`GetUsers`, `GetUserById`, `GetGroups`) — any authenticated user can enumerate users/groups.
5. **Token refresh drops scopes** (`TokenService`) — refreshed access tokens can come back with empty scopes / missing permission destinations.
6. **Pervasive `InvalidOperationException` → HTTP 500** where 404/400 is expected — ~20 handlers.

---

## A. Security findings still live on `main`

| Sev | PR | File / location | Issue | Fix |
|---|---|---|---|---|
| **HIGH** | 105 | `Document/.../DownloadDocument/DownloadDocumentEndpoint.cs:60` | `.AllowAnonymous()` — anyone with a document GUID downloads the file (peers require `CanReadDocument`). IDOR + unauthenticated. | Replace with `.RequireAuthorization("CanReadDocument")`. |
| **HIGH** | 201 | `Integration/.../ResubmitRequest/ResubmitRequestEndpoint.cs:63` | `.AllowAnonymous()` on a state-mutating bank-facing resubmit; peers use `RequireAuthorization("Integration")`. | Replace with `.RequireAuthorization("Integration")`. ⚠ external-facing — confirm no external caller relies on anonymous. |
| **HIGH** | 221 | `Shared/Shared/Extensions/HangfireExtensions.cs` | Dashboard `Authorize()` returns `true` for all non-localhost hosts; combined with `.AllowAnonymous()` this exposes Hangfire in prod. | Default-deny: require authenticated admin (role/policy) in non-dev. |
| **HIGH** | 146 | `Auth/.../Services/TokenService.cs:158-165` | Refresh-token flow reuses the auth-code path and sets scopes from `request.GetScopes()`; refresh requests omit `scope` → empty scopes & missing permission destinations on refreshed tokens. | Carry forward `principal.GetScopes()` when request scopes are empty before `SetScopes/SetResources/SetDestinations`. |
| **MED** | 139 | `Document/.../DeleteDocument/DeleteDocumentEndpoint.cs:20` | `//.RequireAuthorization("CanWriteDocument")` commented out — authenticated but any user can delete documents. | Uncomment. |
| **MED** | 193 | `Integration/.../GetParameters/GetParametersEndpoint.cs:29` | `//.RequireAuthorization("Integration")` commented out. | Uncomment (⚠ external-facing). |
| **MED** | 193 | `Integration/.../GetQuotationValuers/GetQuotationValuersEndpoint.cs:23` | `//.RequireAuthorization("Integration")` commented out. | Uncomment (⚠ external-facing). |
| **MED** | 140 | `Auth/.../Users/GetUsers/GetUsersEndpoint.cs` | No permission policy — any authenticated user can list users (siblings use `CanManageUsers`). | Add `.RequireAuthorization("CanManageUsers")`. |
| **MED** | 140 | `Auth/.../Users/GetUserById/GetUserByIdEndpoint.cs` | No policy — full user detail (roles/groups) to any authenticated user. | Add `.RequireAuthorization("CanManageUsers")`. |
| **MED** | 140 | `Auth/.../Groups/GetGroups/GetGroupsEndpoint.cs` | No policy — org structure to any authenticated user. | Add `.RequireAuthorization("CanManageGroups")`. |
| **MED** | 205 | `Shared/Shared/Security/DataProtectionExtensions.cs` | DataProtection keyring persisted to DB with no key-at-rest encryption — DB read decrypts antiforgery cookies + OpenIddict reference tokens. | **APPLIED (config-driven):** when `DataProtection:CertificateThumbprint` is set, the keyring is encrypted via `ProtectKeysWithCertificate` (cert loaded from `LocalMachine\My`/`CurrentUser\My`). No-op until ops provisions the cert. **N=2:** the SAME cert (same thumbprint) must be installed on BOTH app servers so each can decrypt the shared keyring. |
| **HIGH?** | 166 | `Auth/.../Menu/GetMyMenu/GetMyMenuQueryHandler.cs` | **Needs judgment.** Caller-supplied `activityId` applies `ActivityMenuOverride` rows that bypass role-based menu visibility/edit checks with no task-context validation. | Validate activity belongs to the user's workflow/task; else fail closed to role permissions. |
| **HIGH?** | 223 | `Common/.../SystemConfiguration/SystemConfigurationEndpoints.cs:72` | **Needs judgment.** `PUT /system-configurations/{key}` mutates global config with only `RequireAuthorization()` (no admin policy). Class doc says intentional. | Add an admin policy unless intentionally open. |
| MED? | 161 | `Auth/.../Services/PermissionResolver.cs:27-37` | N queries per request (one repo call per role). | Batch `GetRolesByNames`. |
| LOW? | 140 | `Auth/.../Controllers/OpenIddictController.cs:89-91` | `GET /connect/logout` is `[AllowAnonymous]+[HttpGet]` → CSRF-style cross-site logout. | POST-only logout w/ anti-forgery, per flow. |
| MED? | 102 | `Auth/.../Auth/Me/MeEndpoint.cs:16` | Returns 401 when an authenticated principal's `sub` isn't a GUID (client_credentials tokens) — valid token gets 401. | Return 403 for non-user principals or gate with a user-subject policy. |
| MED? | 213 | `Appraisal/.../AssignAppraisal/AssignAppraisalCommandHandler.cs` | **Needs judgment.** Resumes a client-supplied `WorkflowInstanceId` without verifying it correlates to `command.AppraisalId`. | Cross-check the instance's appraisalId/correlation, or derive server-side. |
| MED? | 161 | `Workflow/DocumentFollowups/.../SubmitDocumentFollowupCommandHandler.cs:35` | **Needs judgment.** Authorizes against workflow `StartedBy`, but the stated rule is "the task assignee can submit". | Authorize against the current task assignee. |

---

## B. Correctness findings still live on `main`

### B1. `InvalidOperationException` → HTTP 500 (should be 404/400) — recurring theme
`CustomExceptionHandler` maps unknown exceptions to 500; these are missing-resource/validation cases that should map to 404/400.

| PR | File | Should be |
|---|---|---|
| 111 | `LinkAppraisalComparable/LinkAppraisalComparableCommandHandler.cs` | 404 not-found / 400 duplicate |
| 111 | `UnlinkAppraisalComparable/UnlinkAppraisalComparableCommandHandler.cs:20` | 404 |
| 133 | `Appraisal/Domain/Appraisals/Appraisal.cs:381 CopyProperty` | 404 |
| 133 | `Appointments/CreateAppointment/CreateAppointmentCommandHandler.cs:20,28` | 400 |
| 183 | `Project/{Add,Remove,Set,Unset}Project{Tower,Model}Image*CommandHandler.cs` (≈8 handlers) | 404 |
| 219 | `SupportingDataMaintenance/AddSupportingDetailImage`, `RemoveSupportingDetailImage` handlers + `SupportingData(.Detail).cs` RemoveImage | 404 |
| 219 | `Infrastructure/Repositories/SupportingDataRepository.cs` `SupportingStatus.FromString(status)` (invalid `?status=` query) | 400 |
| 125 | `Request/.../GetRequests/GetRequestQueryHandler.cs:15` `RequestStatus.FromString(Status.ToUpperInvariant())` — **also a casing bug: every value 500s** (ToUpper never matches PascalCase) | 400 + fix casing |
| 179 | `Project/UpdateProjectTower/UpdateProjectTowerCommandHandler.cs:14` | 404 |
| 128 | `PricingAnalysis/{SetFinalValue,SaveComparativeAnalysis,UpdateFinalValue}` parent-approach `.First(...)` scan | resolve by `method.ApproachId`, clear error |

### B2. Wrong / dropped data & silent no-ops
| Sev | PR | File | Issue | Fix |
|---|---|---|---|---|
| MED | 161 | `Income/MethodDetails/Method11Detail.cs` | `[JsonPropertyName("totalEnegyCost")]` typo — breaks FE/persisted JSON contract. | Rename to `totalEnergyCost`. |
| MED | 134 | `Appraisals/UpdateBuildingProperty/UpdateBuildingPropertyCommandHandler.cs` | Omitted `ConstructionInspection` (null) **clears** existing inspection — destructive-by-default, contradicts null=no-op. | Treat null as no-op; clear only when `IsUnderConstruction==false`. |
| MED | 116 | `MarketComparables/MarketComparableImage.cs` + `AddMarketComparableImageCommandHandler.cs:38` | `Create` leaves `Id` empty → API returns `Guid.Empty` for the new image. | `Id = Guid.CreateVersion7()` in `Create`. |
| MED | 140 | `Database/Scripts/Views/Workflow/vw_WorkflowSlaSummary.sql:26,33` | Hard `CAST(CorrelationId AS uniqueidentifier)` breaks the whole view if any non-GUID value. | `TRY_CONVERT`. |
| MED | 108 | `GetPropertyGroupById/GetPropertyGroupByIdQueryHandler.cs` | LEFT JOIN can yield null `item`; `item.PropertyId` dereferenced → possible NRE for empty groups. | `if (item is not null && item.PropertyId is not null)`. |
| MED | 133 | `Auth/.../LdapAuthenticationService.cs:100` | `response.Entries[0]` with no empty-result guard → 500 on stale DN. | Guard `Entries.Count==0`. |
| MED | 221 | `Evaluations/Queries/DetectDeliveryTimeQuery.cs:124` | Malformed `ThresholdsJson` silently returns rating 1 (worst) instead of documented fallback. | Return sentinel → hit hardcoded fallback. |
| MED | 184 | `Workflow/Engine/WorkflowEngine.cs:90-95` | Initial transition event published **before** start activity runs; if it throws, consumers advance persisted status to a never-reached state. | Publish after start activity succeeds / same committed tx. |
| MED | 163 | `Workflow/.../ApprovalActivity.cs:398,450` | Empty `committeeCode` still publishes `AppraisalApprovedIntegrationEvent`; consumer stamps idempotently → committee identity permanently lost. | Skip publish + warn when code empty (mirror appraisalId guard). |
| MED | 163 | `Workflow/Config/appraisal-workflow.json` conditions + decision-key parser | Multi-literal conditions (`decision=='proceed' && assignmentType=='External'`) mis-parsed by first/last-quote substring → wrong action→target resolution in `GetActivityDecisions`. | Keep `decision == key` form or parse only the decision literal. |
| MED | 157 | `Workflow/Meetings/Activities/MeetingActivity.cs` | Read-then-insert idempotency; concurrent executions (N=2) can both insert duplicate Queued rows (unique index only covers Assigned). | Filtered unique index on non-Released rows / atomic upsert. |
| MED | 186/188 | `Domain/Services/LeaseholdCalculationService.cs` (`SaveLeaseholdAnalysisCommandHandler.cs:132,141`) | Persist path passes `LandValuePerSqWa` into the `pricePerSqWa` param while preview (line 50) uses `PricePerSqWa`; stored values wrong when rates differ. | Use `PricePerSqWa` consistently or confirm equivalence. |
| LOW | 108 | `Document/.../ImageResizeService.cs` | `Resize()` can return null; `SKImage.FromBitmap(resized)` then throws. | Null-check + fallback. |
| LOW | 101 | `UploadDocument`/`CreateUploadSession`/`UploadDocument` endpoints | Return 200 but declare `.Produces(201)`. | Align Produces or return 201. |
| LOW | 166 | `ActivityOverrides/{Get,Update}ActivityOverridesEndpoint.cs` + `GlobalSearch` | Anonymous-object `BadRequest` while declaring `ProducesProblem(400)` — OpenAPI mismatch. | Return `ProblemDetails`. |
| LOW | 200/108/116/200 | several `*CommandHandler.cs` | Redundant `SaveChangesAsync` inside `ITransactionalCommand` handlers (pipeline already saves). | Remove explicit save. |
| LOW | 101 | `Document/.../UploadDocument/UploadDocumentCommandHandler.cs:53-61` | Checksum duplicate-detection block fully commented out — identical files upload unguarded; dead code. | Remove dead code or gate behind a flag. |
| LOW | 170/170 | `Workflow/.../WorkflowResilienceService.cs:598,619` | Circuit-breaker timing uses `DateTime.Now` (DST/clock sensitive). | `UtcNow` / monotonic clock. |
| LOW | 167 | `Common/.../*DashboardHandler.cs` | `new DateTimeOffset(ApplicationNow)` (Kind=Unspecified) → wrong offset on UTC containers. | Build offset via configured TZ. |
| LOW | 195 | `LeasedBackgroundService` / messaging | (see SonarCloud S6444 — no command timeout) | — |

---

## C. Business-domain mismatches (NEEDS JUDGMENT — confirm intent before changing)

| Sev | PR | File | Mismatch | Note |
|---|---|---|---|---|
| MED | 200 | `Fees/UpdateFee/UpdateFeeRequest.cs` | Non-nullable `BankAbsorbAmount`; PATCH sending only `feePaymentType` deserializes it to `0` → `SetBankAbsorb(0)` **clears existing absorb**. | Conflicts with [[project_fee_payment_type_codes]] absorb invariants. Make nullable + preserve. |
| MED | 184 | `DecisionSummary/SaveDecisionSummary/...:101-120` | Block-insurance branch triggers on **any** `appraisal.Projects` row; LandAndBuilding now shares the `Project` aggregate ([[project_block_to_project_refactor]]) so it wrongly sums `ProjectUnitPrices` instead of building-depreciation. | Filter probe by `ProjectType=Condo`. |
| MED | 141 | `Configurations/AppraisalAggregateConfiguration.cs:52-53` | `FacilityLimit decimal(18,2)` vs Request module `(19,4)` — precision loss on copy; used for fee tiering/routing. | Match `(19,4)` unless 2dp is agreed THB basis. |
| MED | 165 | `Request/.../RequestDocumentAttacher.cs` | `UploadedBy = input.UploadedBy ?? currentUser.Username` — should be the bank **code**, capped nvarchar(10). | Cross-check [[feedback_use_code_not_userid_for_actors]] — but `currentUser.Username` already = code; verify. |
| MED | 140 | `Roles/GetRoles/GetRoleQueryHandler.cs:11,17` | Converts `PageNumber-1` for the query but returns `paginated.PageNumber` (0-based) to a 1-based client. | [[gotcha_pagination_zero_based]] — confirm the contract direction. |
| MED | 180 | `Quotations/CreateQuotation/CreateQuotationCommandHandler.cs` | Only `RmUsername` resolved; `RmUserId` left null. `GetQuotations` filters by `RmUserId` → RM-created quotations invisible in scoped list. | Resolve RM Guid too, or confirm scoping. |
| MED | 105 | `Shared/Shared/DDD/IDomainEvent.cs` | `EventId` is computed (`Guid.CreateVersion7()`) per access → one event instance yields different IDs, breaking correlation/dedup. | Stored value on a base record. (One agent flagged; verify current shape.) |
| MED | 173 | `Auth/.../UpdateUserGroups/UpdateUserGroupsCommandHandler.cs:21` | `command.GroupIds ?? []` → a missing field silently removes all group memberships. | Validate non-null at boundary if clearing is unintended. |
| MED | 193 | `Invoices/CreateInvoice/CreateInvoiceCommandHandler.cs` | No guard for empty `AssignmentIds` → creates a Draft invoice with zero items. | Validate non-empty + de-dup. |
| MED | 197 | `Integration/.../GetAppraisalResult*Endpoint.cs`, `GetAppraisalStatusEndpoint.cs` | v1 routes pluralized (`/appraisal/` → `/appraisals/`) with **no alias** → pinned external clients get 404. The intended change was the response shape only. | Add singular alias or new API version. |
| LOW | 191 | `Configurations/PricingConfiguration.cs` | `NEWSEQUENTIALID()` + `ValueGeneratedNever()` — Guid.Empty risk if a Create omits Id. | Intentional project pattern; confirm all Create paths set Id. |
| LOW | 110 | `AppendixDocument.cs`, others | `Create` omits `Id` (server NEWSEQUENTIALID fallback). | Project pattern; OK unless Id needed pre-save. |
| LOW | 169 | `Workflow/Meetings/CreateMeeting` | `Request.CommitteeId` ignored; handler hard-codes `COMMITTEE_WITH_MEETING`. | Use/validate it or remove from request. |
| LOW | 201 | `Invoices/GetInvoiceList/GetInvoiceListQueryHandler.cs:93` | `GrandItemCount = COUNT(Id)` counts invoices, not line items. | Rename or `SUM(ItemCount)`. |
| LOW | 121 | `Configurations/CondoAppraisalDetailConfiguration.cs:96` | RoofType JSON converter throws on legacy plain-string values. | Migrate or tolerate plain strings. |
| LOW | 103/111 | `DeleteProperty`, `UnlinkAppraisalComparable` | Route param naming / singular-plural inconsistency; `DeleteProperty` also lacks `RequireAuthorization`. | Align routing + add auth. |

---

## D. SonarCloud security hotspots (82 open)

| Rule | Count | Category | Verdict |
|---|---|---|---|
| `csharpsquid:S2077` dynamic SQL | 38 | sql-injection (HIGH) | **False-positive.** Dapper binds all values via `DynamicParameters`; interpolated identifiers come from `switch`/HashSet whitelists or constants. **One defense-in-depth gap:** `DapperPaginationExtensions.QueryPaginatedAsync`/`WithPagination` interpolate `orderBy` unvalidated (safe today: every caller pre-validates). |
| `javascript:S2245` insecure RNG | 26 | weak-crypto | **N/A** — all in `docs/load-test/*.js` (k6 `Math.random`), not shipped. |
| `csharpsquid:S6444` no Regex timeout | 9 | dos | **Fix** — `Regex` calls without `matchTimeout` (ReDoS) in `ScribanTemplateRenderer`, `WorkflowExpressionEvaluator`, `SwitchActivity`, `PublishEventAction`, `WorkflowPersistenceService`, `WorkflowSchemaValidator`, `ActivityProcessAdminEndpoints`. |
| `javascript:S5852` ReDoS regex | 3 | dos | **N/A** — `docs/load-test/*.js`. |
| `csharpsquid:S2245` insecure RNG | 2 | weak-crypto | By-design — workflow `Random()` scripting function (`ExpressionContext.cs:75`, `WorkflowExpressionService.cs:285`), not security-sensitive. |
| `csharpsquid:S5332` http | 2 | encrypt-data | **N/A** — `docs/*`. |
| `csharpsquid:S4790` weak hash | 1 | others | By-design — `DeterministicGuid` SHA-1 for RFC-4122 v5 UUIDs; not a security context. |
| `yaml:S2068` hardcoded password | 1 | auth | Acceptable — `docker-compose.yaml` dev password. Suppress/document. |

---

## E. Recommended fix plan

**Tier 1 — safe, mechanical, no behavior risk (apply now):**
- B1 exception-type corrections (500→404/400) across the ~20 handlers.
- B2 `Method11Detail` JSON typo; `MarketComparableImage.Create` Id; `vw_WorkflowSlaSummary` `TRY_CONVERT`; `GetPropertyGroupById` null guard; LDAP empty-result guard; remove redundant `SaveChangesAsync`; remove/flag dead duplicate-check code.
- D: add `matchTimeout` to the 9 S6444 `Regex` sites; add `orderBy` whitelist guard to `DapperPaginationExtensions`.

**Tier 2 — security, internal endpoints (apply now, low external risk):**
- Hangfire default-deny (non-dev now requires an authenticated user; **per decision, an admin-role/policy gate was NOT added** — left as a TODO at `HangfireExtensions.cs:71`); `TokenService` refresh-scope carry-forward; DataProtection config-driven cert (above).
- **Endpoint auth — APPLIED as "remove bypass, rely on global `RequireAuthenticatedUser` fallback" (per decision 2026-06-11); tightening to a specific permission is deferred:** `DownloadDocument` (removed `.AllowAnonymous()`), `DeleteDocument` (removed the commented `CanWriteDocument` leftover), `GetUsers`/`GetUserById`/`GetGroups` (no specific policy added). These now require **authentication** but not a specific permission — `CanReadDocument`/`CanWriteDocument`/`CanManageUsers`/`CanManageGroups` remain the recommended next-step hardening.

**Tier 3 — confirm before changing (outward-facing or judgment):**
- Integration endpoints `ResubmitRequest` (`AllowAnonymous`), `GetParameters`, `GetQuotationValuers` — flipping auth ON may break external bank callers.
- All Section C domain mismatches (fee absorb clearing, block-insurance probe, FacilityLimit precision, route aliases, etc.).
- `SystemConfiguration` admin policy, `GetMyMenu` override scoping, `/connect/logout` CSRF.

## Verification
- `dotnet build` clean after Tier 1+2.
- Each `RequireAuthorization` add compiles and matches sibling endpoint pattern; policies (`CanReadDocument`/`CanWriteDocument`/`CanManageUsers`/`CanManageGroups`/`Integration`) all registered in `AuthModule.cs`.
- Re-run SonarCloud on the fix branch to confirm S6444 count drops and remaining hotspots are intentional.
