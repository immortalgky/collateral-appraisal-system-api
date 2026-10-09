# Menu groups without a permission · Webhook secrets encrypted + reveal

Plan: `~/.claude/plans/2-modular-pike.md` (approved 2026-09-25)

## Part 1 — Menu parent visible iff a child is
- [x] `GetMyMenuQueryHandler` — ungated node shows only when ≥1 child is visible; gated nodes unchanged
- [x] `MenuItem` Create/Update + both validators — view permission no longer required
- [x] `MenuSeedData` — 6 groups ungated (master-data, workflow, business-rules, access, system, standalone)
- [x] Script `20260925120000_UpdateSeed_UngateMenuGroups.sql` — clears the gate only while it still equals the seeded value
- [x] `AuthModule` Hangfire comment (no more LOGS_VIEW dependency)
- [x] FE: form makes view permission optional; preview + tree table share `utils/menuVisibility.ts` (same rule as BE)
- [x] Unit test `Tests/Unit/Auth.Tests/Menu/MenuTreeVisibilityTests.cs` (4 cases)

## Part 2 — Webhook secrets
- [x] `Shared/Security/ColumnSecretCipher` — SecretProtector + secrets cert; legacy plaintext readable; no cert = plaintext in Dev only, throws elsewhere
- [x] Domain `WebhookSubscription.Update(...)` replaces UpdateCallbackUrl/UpdateSecretKey; clears the unused auth type's credentials
- [x] Create/Update commands accept AuthType, HttpMethod, TokenEndpoint, ClientId, ClientSecret (+EventType on create); encrypt on save
- [x] `WebhookService` (HMAC) + `LosTokenProvider` (client secret) decrypt on use
- [x] DTO: `SecretLast4` → `HasSecretKey`/`HasClientSecret` + auth fields
- [x] Columns widened to nvarchar(2000) + `WebhookSecretRevealLogs` table — migration `EncryptWebhookSecrets` (files only, not applied)
- [x] Permission `WEBHOOK_SECRET_REVEAL` (seed + script, Admin only) + policy `WebhookSecretReveal`
- [x] `POST /webhook-subscriptions/{id}/secret/reveal` — needs both policies, audit row saved before the value is returned, `Cache-Control: no-store`
- [x] FE: full form (event type, method, auth type, token fields), password inputs, set/not-set badge, audited Reveal/Copy/Hide
- [x] Unit test `Tests/Unit/Shared.Tests/Security/ColumnSecretCipherTests.cs` (4 cases)

## Review
- Build: `dotnet build Bootstrapper/Api` clean, no warnings in touched files. Tests: Auth.Tests 44/44, Shared.Tests 23/23.
- FE: `tsc` 188 errors before and after, none in `menuManagement`/`webhookAdmin`. Remaining eslint errors in
  `webhookSubscriptions.ts` (`any`) and the list page (`active.yes`) are pre-existing on HEAD.
- Not done / needs the user: run `dotnet run --project Database/Database.csproj migrate`; manual test in the app.
- Ops after deploy: re-enter the LOS HMAC secret and the LOS PMA client secret on the screen so the rows become `ENC:v1:`.
  **UAT/prod must have `Secrets:CertificateThumbprint` set** — without it, saving a webhook secret now fails (by design).
- Update is intentionally strict: `AuthType`/`HttpMethod` have no default on PUT, so an old client gets 400 instead of
  silently converting a TokenBearer row to HMAC.
- ~~Known limit: cached bearer token outlives a ClientSecret change~~ — fixed in review round 1 (#3 below).

## Review round 1 fixes (/code-review high, 9 findings — all fixed 2026-09-25)
1. Create/Update webhook commands override `ToString()` → secrets print as `***` (LoggingBehavior logs every request).
   Secrets entered BEFORE this fix are likely already in Seq/app logs — consider rotating the LOS secrets.
2. `WebhookSubscription.Update` refuses to keep the stored ClientSecret when TokenEndpoint/ClientId change
   (DomainException → 400); FE forces re-entry with a note.
3. `LosTokenProvider` cache entry carries a SHA-256 fingerprint of TokenEndpoint/ClientId/ClientSecret/CallbackUrl —
   a changed subscription never reuses an old token, on either node.
4. DTO `SecretKeyEncrypted`/`ClientSecretEncrypted`; list + form show "Set, NOT encrypted — re-enter it" for legacy rows.
5. Validators again require a view gate when `Path` is set (only path-less groups may be ungated); FE form mirrors it.
   `main.standalone` Path → NULL (no `/standalone` route) in seed + script.
6. Activity override preview (`ActivityOverridesPanel`/`ActivityPreviewPane`) uses `visibleMenuIds`;
   `MenuItemAdminDto.viewPermissionCode` typed `string | null`.
7. Reveal: decrypt → save audit → return; no user code = refuse (no `anonymous` rows).
8. One cert lookup: `EncryptedConfigurationExtensions.LoadSecretsCertificate` used by config decryption and ColumnSecretCipher.
9. Stale Hangfire/LOGS_VIEW comment in MenuSeedData fixed.
Tests added: `CreateMenuItemValidatorTests` (BE), `utils/menuVisibility.test.ts` (FE). Auth 46/46, Shared 23/23, vitest 3/3, tsc 188 (baseline).

## Review round 2 fixes (2026-09-26)
- TokenBearer: changing CallbackUrl also requires re-entering ClientSecret (else a fresh bearer token goes to the new host).
- `ColumnSecretCipher` loads the cert lazily — a bad cert fails the one delivery (caught), not DI for every webhook.
- FE create/update/reveal mutations use `gcTime: 0` (no plaintext left in the MutationCache).
- Ungate script is one statement and only touches rows with no Path (or standalone's seeded `/standalone`).
- `ENC:v1:` prefix passed as a Dapper parameter from `SecretProtector.Prefix`; list/detail share `WebhookSubscriptionSql.Columns`.
- Removed dead `resetKey`/`openCount` (Dialog unmounts content on close).
- Passwords redacted in `ChangePasswordCommand`, `ResetPasswordCommand`, `CreateUserCommand` (user decision: per-command,
  not LoggingBehavior); rule noted in `LoggingBehavior`. Test `Auth.Tests/Users/PasswordCommandRedactionTests`.
  Passwords changed/reset/created before this fix are likely in Seq/app logs.
- Not fixed by decision: per-delivery RSA decrypt (caching would hold plaintext); STANDALONE_USE now a no-op (documented
  in the script); activity preview ignoring overrides for ungated groups (appraisal scope has no groups).

## Review round 3 fixes (2026-09-26)
- `ColumnSecretCipher` Lazy uses `PublicationOnly` — a failed cert load is retried, not cached until restart.
- `RegisterUserCommand` (anonymous `/auth/register`, reached via `Adapt`) redacted too; covered by the redaction test.
- "Page needs a view gate" moved into `MenuItem.Create/Update` (one rule for every caller incl. seeder/reorder);
  validator copies removed; test `Auth.Tests/Menu/MenuItemViewGateTests`.
- Update encrypts only the chosen auth type's secret (no 500 over a secret that would be discarded).
- New migration `20260925171228_WidenWebhookSecretColumns` → nvarchar(4000) (EncryptWebhookSecrets was already applied locally).
- FE: revealed plaintext cleared whenever the stored secret stops applying.
- Not changed: preview `canEdit` for items with no edit code — reviewer's claim that MenuTreeTable treats them as read-only
  is wrong (`lockedForRole` requires an edit code); preview and table agree.

## Review round 4 (2026-09-26) — no new serious bug; 8 minor fixed
- Webhook tests `Collateral.Tests/Webhooks/WebhookSubscriptionSecretTests` (Integration unit tests live in Collateral.Tests):
  re-entry guard ×3 targets, keep-on-method-change, new-secret-allows-retarget, command redaction.
- `WebhookConnectionRules.AddTokenBearerRules` shared by create/update validators.
- Cert error message names both thumbprint keys (`EncryptedConfigurationExtensions.CertificateHint`).
- FE: clipboard guarded (http / denied → toast), auth column translated, View Permission asterisk shown only when a Path
  is set and error cleared on Path change, `visibleMenuIds` iterates the Set + preview memoised, locked-field hint covers
  event type too.
- Left for the user: (a) `main.user-management` (a page, `/users`) still gates its children on USER_MANAGE — by the plan's
  decision; (b) reveal-audit IP comes from X-Forwarded-For, which Program.cs trusts from any source (global
  ForwardedHeaders config, also affects AuthAuditWriter).

## Review round 5 (2026-09-26) — no correctness/security issues; 3 cleanups fixed
- `MenuListPage` computes `visibleIds` once (from the saved tree) and passes it to MenuTreeTable + MenuTreePreviewPane —
  one walk, one source, both panes always agree.
- `AddDecryptedSecrets` error reuses `CertificateHint`.

## Review round 6 (2026-09-26) — 3 minor fixed
- `ColumnSecretCipher` keeps only a loaded cert; a missing thumbprint (null) is re-read from live config on the next call
  (Lazy cached the null). Test `A_thumbprint_added_after_startup_is_picked_up_without_a_restart`.
- FE: typing a secret marks it as a replacement, so undoing the endpoint/URL change no longer silently drops it.
- FE: Hide/Replace/stored-change also `reset()` the reveal mutation — no plaintext left in the observer.

## Review round 7 (2026-09-26) — 2 fixed
- Both DbUp scripts now start with `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; GO` (auth.MenuItems has a filtered
  index; prod's sqlcmd bundle runs without -I). Proven locally: script runs under sqlcmd defaults in a rolled-back
  transaction; a control UPDATE without the header fails with Msg 1934.
- FE: typed secret state cleared when the modal closes (the component stays mounted).

## Review round 8 (2026-09-26) — 4 fixed
- `ColumnSecretCipher` resolves the thumbprint per call and caches the cert by thumbprint → rotation needs no recycle
  (old-cert secrets must be re-entered). Tests: rotation, wrapped decrypt failure.
- New `SecretCipherException` (: InvalidOperationException) wraps every cipher failure; `CustomExceptionHandler` answers
  it with a fixed line — thumbprint/store/config keys stay in the server log.
- Webhook list ordered `SystemCode, EventType, Id` (stable paging now that one system has several rows).
- FE: create/update mutations `reset()` on modal close (the observer outlives the modal, so gcTime 0 alone didn't drop
  the typed secret from `variables`).

## Review round 9 (2026-09-26) — 3 fixed, 1 decided
- Cert cache is one `volatile` reference to an immutable record → no torn read under concurrent deliveries.
- Encrypted-flag SQL compares the prefix case-sensitively (`COLLATE Latin1_General_BIN2`), matching
  `SecretProtector.IsProtected` (Ordinal). Verified on the local DB: ENC:v1: → 1, enc:v1: → 0.
- Migration `Down` paths clear secrets that cannot fit the narrower column (they are unusable to the rolled-back
  code anyway) instead of failing on truncation.
- Decided: a replaced cert is not disposed (another thread may still use it) — one handle per rotation.

## Review round 10 (2026-09-26) — 5 fixed
- **Deploy bug:** a blank `Secrets:CertificateThumbprint` (template renders it with an empty variable) now falls back to
  `DataProtection:CertificateThumbprint`, as the deployment docs promise — affected config decryption too.
  Test: `A_blank_secrets_thumbprint_falls_back_to_the_DataProtection_one` (null / "" / spaces).
- Down migration clears only `ENC:v1:` values compared BIN2 (a plaintext "enc:v1:…" survives rollback).
- Encrypted flags: prefix inlined from `SecretProtector.Prefix` into the shared SQL (no runtime parameter to forget).
- Stale comments fixed (LoadSecretsCertificate doc; script says MenuItem.Create/Update, not validators).

## Review round 11 (2026-09-26) — 3 fixed, 1 decided
- `CustomExceptionHandler` returns validation failures as {PropertyName, ErrorMessage, ErrorCode, Severity} only —
  `AttemptedValue` echoed rejected passwords/secrets in 400 bodies (global; FE reads none of the dropped fields).
- FE: "Keep the current secret" backs out of Replace (Save was stuck disabled).
- One cert-load helper `EncryptedConfigurationExtensions.LoadSecretsCertificate(string)` used by both paths.
- Decided: in Development without a cert, secrets are stored plaintext and correctly flagged "NOT encrypted" — dev-only.

## Review round 12 (2026-09-26) — no bugs; 1 missing test added
- `Shared.Tests/Exceptions/CustomExceptionHandlerSecretTests`: a validation 400 never echoes the attempted value; a
  `SecretCipherException` 500 carries only the fixed line. Shared.Tests 31/31.

## Review round 13 (2026-09-26) — no correctness bugs; 5 hardening/cleanup fixed, 1 decided
- Ungate script header: deploy-order/rollback warning + the exact SQL to restore the six seeded gates.
- HMAC: changing CallbackUrl also requires re-entering SecretKey (else signed payloads to a new host allow offline
  brute force). Domain + FE; test `Hmac_callback_change_requires_re_entering_the_secret_key`.
- FE reveal note no longer claims "the audit log" (no screen reads WebhookSecretRevealLogs yet — query it directly).
- Menu tree View column shows a prefix gate as `PREFIX*`, so prefix-gated vs ungated groups are distinguishable.
- `LoadSecretsCertificateByThumbprint` renamed (no same-name overloads for the method-group conversion).
- Decided: FE `menuVisibility.ts` mirrors the BE rule (both unit-tested) instead of a new preview endpoint.

## Review round 14 (2026-09-26) — no bugs; 2 minor fixed
- HasSecretKey/HasClientSecret treat blank as unset (NULLIF(LTRIM(RTRIM())), matching IsNullOrWhiteSpace everywhere else).
- ActivityOverridesPanel builds `menuMeta` recursively (nested appraisal items got no edit code/icon).

## Review round 15 (2026-09-26) — 1 minor fixed
- One "blank secret" rule everywhere: WebhookService + reveal use IsNullOrWhiteSpace; the SQL flags count a secret as set
  only if it has a character other than space/tab/CR/LF (verified on the local DB).

## Review round 16 (2026-09-26) — CLEAN (empty findings list)
Open for the user: (1) main.user-management (/users) still hides its children behind USER_MANAGE;
(2) reveal-audit IP trusts any X-Forwarded-For (global ForwardedHeaders config).
To do by the user: `dotnet run --project Database/Database.csproj migrate` (new migration WidenWebhookSecretColumns +
2 DbUp scripts), restart API, manual test; after deploy re-enter the LOS secrets; consider rotating LOS secrets and
passwords changed before this fix (they may be in Seq/app logs).

## Follow-up (2026-09-26): page-parents become plain groups (user decision)
- Found: Sidebar only expands/collapses a parent (never navigates to its Path); `/users` has no route; breadcrumb map lets
  children overwrite a parent's duplicate path. So the "page-parent keeps its gate" reasoning was wrong.
- Seed: main.user-management, main.oauth, main.collateral-master, main.template-management → Path/gate/edit = null.
- New script `20260926120000_UpdateSeed_UngateMenuParents.sql` (first script already ran locally in its first version,
  so it also finishes main.standalone's Path). Dry-run under sqlcmd defaults in a rolled-back tx: 5 rows as intended.
- Follow-up review 1: FE Sidebar `isChildActive` now checks descendants at any depth (path-less sub-groups kept their
  top-level group from highlighting) and child items are keyed by `itemKey` (two path-less siblings shared key '#');
  script also matches the seeded EditPermissionCode; seed comment says plainly that operational parents
  (requests/tasks/reports/quotation/invoice/meetings) still gate their subtree — left for a separate decision.
- Follow-up review 2: no bugs. Added `Nested_ungated_groups_follow_the_same_rule_at_every_level` (MENU_MANAGE alone
  reaches Menus through two ungated levels; empty OAuth sub-group dropped); `isDescendantActive` moved to module level;
  child key is `itemKey` only. Auth.Tests 49/49.
- Follow-up review 3: main.workflow-builder ungated too (5th nested parent; its children share its gate, so no
  visibility change) — seed + script (dry-run: 6 rows). Script header: ship FE + API before running it. FE: all sidebar
  keys use `itemKey`; active-match helpers moved to `src/shared/components/sidebarActive.ts` (+ test, 2/2) so
  Sidebar.tsx exports components only.
- Follow-up review 4: rollback note says to SET QUOTED_IDENTIFIER ON first; moved docstring corrected; test `Keys` now
  flattens every level via `AllKeys`. Decided: `isDescendantActive` re-walking a few dozen items per navigation is fine.
- Follow-up review 5: CLEAN (no failure scenarios). Cleanups applied anyway: `buildQualifiedHrefs` moved next to its
  consumer in `sidebarActive.ts`; `isHrefActive` query-param rules now tested (vitest 5/5); nested test uses `Keys`.

## Follow-up 2 (2026-09-26): operational parents too (user decision)
- main.request, main.task, main.quotation, main.invoice, main.meetings, main.reports, main.reports.operational → no Path,
  no gate (seed + same un-applied script `20260926120000_UpdateSeed_UngateMenuParents.sql`; dry-run 13 rows).
- Verified: their Paths duplicate the first child's (breadcrumb uses the child) or have no route ('/reports',
  '/reports/operational'); no FE/BE code keys off these parents (Sidebar's TASK_LIST_PATH checks CHILD hrefs).
- Real bug fixed by this: seeded ExtAdmin (QUOTATION_EXT_VIEW/INVOICE_EXT_VIEW without QUOTATION_VIEW/INVOICE_VIEW) and
  ExtAppraisalChecker (QUOTATION_EXT_VIEW) could not see their "External Co. Portal" menus. No seeded role loses anything.
- Follow-up 2 review 1: deploy notes now match deploy/README (bundle before app, app straight after) and state the
  scripts are NOT backward-compatible; new README section "Menu parents ungated" (deploy order, restart, rollback via
  header SQL, admin-facing behaviour change: a parent permission is no longer a section master switch). Both scripts
  stamp UpdatedAt. Dry-runs OK (13 rows / 0 rows on the already-applied first script).
- Follow-up 2 review 2: README section rewritten — no restart of old-build nodes between bundle and app (the cache
  also reloads on first request after boot and on any /admin/menus edit; freeze edits), rollback = header SQL +
  delete both journal rows (dbo.DatabaseMigrationHistory) before re-deploying; accurate list of codes whose meaning
  changed; general "Rollback" section points to it. Script footers no longer say "restart the API". Parents script
  adds a repair UPDATE (any parent with children + Path + no gate → Path NULL). Dry-run: 13 + 0 rows.
- Follow-up 2 review 3: rollback is now a real file `deploy/rollback/Restore_MenuParentGates.sql` (one transaction,
  VALUES-driven, restores only still-ungated seeded rows, then REFUSES + lists any other ungated item, e.g. an
  admin-created group; FOR XML PATH, not STRING_AGG). Tested: forward+rollback round trip restores all; refusal path
  leaves the DB untouched. Commented SQL removed from both headers. Both forward UPDATEs require the row to still have
  children. README: rollback via the file; users must reload open tabs after the SPA deploy.
- Follow-up 2 review 4: has-children guard applies only to rows WITH a Path (fresh DB: 20260729 inserts the admin
  containers before the seeder adds children — the old guard skipped them forever). Publish.ps1 ships
  `deploy/rollback/` as `rollback/` at the zip root (never under db\, so Invoke-SqlDeploy can't run it); README gives
  the exact `sqlcmd ... -b` command. Standalone VALUES row dropped — the repair UPDATE handles it (dry-run: 12 + 1).
- Follow-up 2 review 5: README "Rollback" note now lists BOTH non-backward-compatible changes (menu parents AND
  ENC:v1: webhook secrets — re-enter plaintext after an app rollback, query given); rollback file treats blank gates as
  ungated (restore + refusal); parents-script guard simplified to EXISTS; repair UPDATE pinned to Scope = 0 and prints
  its row count; README sqlcmd uses <database> + SQL-auth variant; Publish summary lists rollback/ only if copied.
  Round trip: forward 12 + 1, rollback 18.
- Follow-up 2 review 6: README gives the exact SQL to write webhook secrets back in plaintext after a rollback (BIN2
  find query + UPDATE per row; the maintenance script is insert-only). Rollback file restores/refuses only rows that
  are still parents, and after COMMIT reports restored gates that no role holds (e.g. STANDALONE_USE removed during
  the release). "Keep in sync" comments tie the three VALUES lists together. Round trip: 12 + 1, restore 18, report empty.
- Follow-up 2 review 7: rollback refuses on ANY ungated item (the old MenuItem.Update rejects one and the old reorder
  sends the whole tree, so a single empty ungated group would break drag-reorder); journal-row deletion now reads
  "before deploying ANY later build" in README, both headers and the rollback file. Round trip OK (18 restored).
- Follow-up 2 review 8: journal-row deletion now says "before deploying again — the same zip or any later build";
  rollback file is ASCII-only (sqlcmd reads it raw, possibly under cp874).
- Follow-up 2 review 9: CLEAN (empty list).
