# Data correction: documents + regenerate summary + unified history

Mock: https://claude.ai/artifact/M2CZPj7pPNh6mUeHFbJrWq (FE repo `docs/poc/regenerate-appraisal-summary-mock.html`, untracked)
Branch (both repos): `feat/appraisal/data-correction-documents`
- BE worktree: `~/orca/workspaces/collateral-appraisal-system-api/data-correction-documents`
- FE worktree: `~/orca/workspaces/collateral-appraisal-system-app/data-correction-documents`

## Decisions (user, 2026-09-29)
- Lives inside the existing **Appraisal Data Correction** menu (`/standalone/appraisal-data-correction`), same
  search page, same permission `APPRAISAL_DATA_CORRECTION` / policy `appraisal.data-correction`. No new menu.
- ONE history for property corrections + document corrections + summary regeneration.
- Defaults chosen by Claude (user may overturn): menu name unchanged; no separate "notify LOS" button —
  regenerate is the way to re-notify LOS; scope = valuation documents (VAL_DOC / VAL_REPORT) only;
  Completed appraisals only; delete stays a hard delete, history keeps file name + reason.

## API contract (BE and FE both build against this)

### 1. `POST /appraisals/{appraisalId}/document-corrections`  — policy `appraisal.data-correction`
```json
{
  "reason": "string, required, trimmed non-empty, max 4000",
  "removeId": "guid | null   — AppraisalDocuments.Id to remove",
  "add": {                    "— null or:",
    "documentTypeCode": "D005",
    "documentId": "guid (from POST /documents upload) — name/mime/size are read server-side from it; 404 if missing"
  }
}
```
- add only = attach · removeId only = delete · both = replace. Neither → 400.
- 404 unknown appraisal / removeId not on this appraisal. 400 invalid type code (same rule as AddAppraisalDocument:
  active type in VAL_DOC/VAL_REPORT). 409 `errorCode: APPRAISAL_NOT_COMPLETED` unless Status == Completed.
- UploadedByName is taken from the server-side current user, not the request.
- Response 200 `{ "addedId": "guid | null" }`.
- One audit row per call (see §4). Same transaction as the document change (ITransactionalCommand<IAppraisalUnitOfWork>).
- Publishes DocumentLinkedIntegrationEventV2 / DocumentUnlinkedIntegrationEvent exactly like the existing endpoints.
- NO RejectClosedAppraisalWriteFilter — this is the sanctioned way in.

### 2. `POST /appraisals/{appraisalId}/documents/regenerate-summary` (existing) — optional body `{ "reason": "..." }`
- Auth stays bare `.RequireAuthorization()` (recorded decision, do not change).
- When reason is non-empty, write one audit row (§4) with key `AppraisalSummary` before enqueuing. Body absent → behaves as today.

### 3. `GET /appraisals/{appraisalId}/property-corrections` (existing)
- `appraisalPropertyId` becomes nullable. null = document entry. Everything else unchanged.

### 4. Audit row = existing table `appraisal.AppraisalPropertyCorrectionLogs`
- `AppraisalPropertyId` → nullable (EF migration, shape only, not applied by us).
- Document rows: `AppraisalPropertyId = null`, `PropertyType = "DOCUMENT"`.
- `ChangedFields` keys are document type codes: `{ "D005": { "from": "old.pdf", "to": "new.pdf" } }`
  (attach: from null · delete: to null · replace across two types: two keys).
- Regenerate: `{ "AppraisalSummary": { "from": null, "to": "Regeneration requested" } }`.

### 5. Close the hole
Add `.AddEndpointFilter<RejectClosedAppraisalWriteFilter>()` to AddAppraisalDocument, RemoveAppraisalDocument,
UpdateAppraisalDocumentNotes. Only FE caller is ValuationDocumentChecklist, already read-only on closed appraisals;
the auto-attach job goes through MediatR, not HTTP, so the filter never sees it.

## Todo
### BE
- [x] Log entity nullable property id + `ForDocuments` factory + config + migration (not applied)
- [x] `CorrectAppraisalDocuments` endpoint/command/handler
- [x] Regenerate endpoint optional reason + audit row; refresh its "No UI affordance" doc comment
- [x] `GetPropertyCorrections` nullable id
- [x] Closed filter on the 3 old document endpoints
### FE
- [x] API hooks: correct documents, regenerate summary; nullable `appraisalPropertyId`
- [x] "Valuation documents" entry in `PropertyRail` + `DocumentCorrectionPane`
- [x] Dialogs: attach / replace / delete / regenerate, reason required
- [x] Regenerate polling (5 s, stop on new D042/D043 file or 60 s)
- [x] Unified `CorrectionHistoryPanel` (document rows labelled by type name)
- [x] i18n th + en
### Verify
- [x] BE build + unit/integration tests
- [x] FE `tsc -b --force`, eslint, vitest
- [x] reviewer pass
- [x] Orchestrator checks the diff against this contract

## Review (2026-09-29)

**Built:** Data Correction page gains a "Valuation documents" rail entry → pane to attach / replace / delete
valuation documents and regenerate the appraisal summary, every action with a required reason, all written to the
one correction history (property + document + regenerate rows together).

**BE:** `CorrectAppraisalDocuments` (POST document-corrections, `appraisal.data-correction`, Completed only,
transactional with its audit row); `AppraisalDocumentQueries` shared with AddAppraisalDocument; regenerate takes
optional `{ reason }`; `AppraisalPropertyId` nullable + migration `20260929062100_MakeCorrectionLogPropertyIdNullable`
(NOT applied — run `migrate`); closed filter on the 3 old document endpoints.

**FE:** `DocumentCorrectionPane`, `DocumentActionDialog`, `utils/documentCorrection.ts` (+test), `readApiError.ts`
(moved), unified `CorrectionHistoryPanel`, `ActionDropdown` optional `labels`, `v1.ts` nullable id (hand edit —
regenerate the client schema later), th/en i18n.

**Verification (run by orchestrator):** BE Api build green; unit CorrectAppraisalDocuments 15/15; integration
ClosedAppraisalWriteGuard + PropertyCorrectionAudit 10/10 (Testcontainers); full Appraisal.Tests 482/485 — the 3
FeeAppointmentApprovalDomainTests fail identically on a clean origin/main worktree. FE `tsc -b --force` 0 (probe
confirmed tsc was really checking); eslint 0 new (3 pre-existing in documentShared.tsx); vitest 30 passed.

**Reviewer:** PASS. Fixed after review — W1: attached file name/mime/size now come from `document.Documents`
(404 if missing), so the audit trail cannot name a file the list never shows (+2 tests). S1: ref guard against a
one-tick double submit. Left: S2 (header Attach with zero types does nothing — cosmetic), S3 (no 403 / no-body
regenerate integration test).

**Orchestrator fixes:** FE agent had no shell; its i18n calls missed the `documents.` prefix (33 tsc errors) — fixed.

**Known / trade-offs:** hard delete (history keeps name + reason); an upload that succeeds before a failed
correction leaves an orphan document (same as the existing checklist); Down migration fails once document rows
exist; switching the rail with unsaved property edits discards them (pre-existing). Not committed.

## /code-review rounds (2026-09-29, user: "run until clean")
- R1 BE: replace keeps the removed file's SortOrder; filter 409 wording. FE: refetch error no longer hides the pane;
  retry re-links the same upload; poll deadline survives effect restarts; Attach disabled with no types.
- Orchestrator after R1: regenerate state lifted to the page (leaving the pane no longer re-enables the button → no
  double LOS notify); rail selection moved into `?propertyId=` so the existing UnsavedChangesDialog guards switching;
  request `add` trimmed to `{ documentTypeCode, documentId }` (server reads name/mime/size); shared
  `appraisal/utils/valuationDocuments.ts`; log class doc; test seed enum.
- R2 BE: replace keeps Notes; only image/PDF (stored MIME) accepted; self-replace → 400; regenerate 400s are
  ProblemDetails (`REASON_TOO_LONG`). Orchestrator: present-but-blank reason → 400 `REASON_REQUIRED` (no body still OK).
  FE: poll refetch no longer cancels itself; baseline refreshed on open; no prompt when re-clicking the open entry;
  localized "regeneration requested"; AppendixTab + category constant shared. Orchestrator: regeneration reset on
  appraisal change; block appraisals (no properties) open the documents pane by default.
- R3 BE: clean. FE: poll compares summary file ids, not counts (deleting the old summary mid-job no longer times
  out); upload errors show the hook's specific message.
- R4 FE: clean (rail count hidden until loaded).
- Accepted / declined with reason: reload during a 60 s poll re-enables Regenerate (needs server job state);
  Down migration fails once document rows exist (only fix is deleting audit rows); no endpoint test for the
  regenerate reason branch (needs Hangfire in the fixture); any active DocumentId attachable (no doc→appraisal link).
- Final: BE Api build green, unit 51/51, integration 10/10; FE tsc 0, eslint 0 in changed files, vitest 31 passed.

---
# Phase 2 — "notify source system" is the user's choice (2026-09-29)

User decisions: (1) only appraisals that came from an external system get any notify UI; (2) the user chooses —
a checkbox in the regenerate dialog (default checked) AND a separate "notify" button; (3) wording is
"ระบบต้นทาง (<ExternalSystem>)" / "source system (<ExternalSystem>)", never a hard-coded "LOS" — more systems will come.
Mock v5: same artifact URL. Webhook facts: AppraisalCompletedWebhookConsumer skips when the request has no
ExternalCaseKey/ExternalSystem; LOS only receives VAL_REPORT documents (D001/D042/D043).

## Contract additions
### A. `GET /appraisals/{appraisalId}/external-source` — policy `appraisal.data-correction`
200 `{ "externalSystem": "LOS" | null }` — non-null only when the appraisal's request has BOTH ExternalCaseKey and
ExternalSystem (exactly the condition under which the webhook consumer sends). 404 unknown appraisal.

### B. `POST /appraisals/{appraisalId}/documents/notify-external` — policy `appraisal.data-correction`
Body `{ "reason": "required, trimmed, ≤4000" }`. 409 `APPRAISAL_NOT_COMPLETED` unless Completed.
409 `NO_EXTERNAL_SOURCE` when A would return null. Publishes AppraisalResultReadyIntegrationEvent through the outbox
(AppraisalId, RequestId, CompletedAt = Appraisals.CompletedAt, DocumentReady = a D042/D043 document is attached,
FailureReason null/"No appraisal summary attached") and writes one audit row
`{ "ExternalNotification": { "from": null, "to": "<ExternalSystem>" } }` in the same transaction.
200 `{ "externalSystem": "LOS" }`.

### C. Regenerate — body becomes `{ "reason"?: string, "notifyExternal"?: bool }`
- `notifyExternal` absent/null → true (manual curl + auto-approval behaviour unchanged).
- false → the job attaches the summary but publishes NO AppraisalResultReadyIntegrationEvent (in any path).
- Hangfire: ADD an overload `RunAsync(appraisalId, requestId, completedAt, force, notifyExternal, ct)`; keep the
  existing 5-arg signature (delegates with notifyExternal: true) so jobs already queued still resolve.
- Audit row (when reason given): `AppraisalSummary` key as today, plus `ExternalNotification` → `<ExternalSystem>`
  when notifyExternal is true AND the appraisal has an external source.

### D. FE
- `useGetExternalSource`, `useNotifyExternalSystem`, regenerate sends `notifyExternal`.
- Strip: "📤 แจ้งระบบต้นทาง (X)" button only when externalSystem; regenerate dialog checkbox (default on) only when
  externalSystem, otherwise the line "งานนี้สร้างในระบบเอง ไม่มีระบบต้นทางให้แจ้ง"; new `notify` dialog kind (reason required).
- No "LOS" literal left in appraisalDataCorrection i18n / components.
- History: `ExternalNotification` field → label "แจ้งระบบต้นทาง", value = system name.

## Phase 2 todo
- [x] BE A, B, C + tests
- [x] FE D
- [x] verify (build/tests/tsc/eslint/vitest) → /code-review until clean

## Phase 2 review log (2026-09-29)
- R1 BE: failed silent run rethrows (Hangfire Failed); Notify DocumentReady counts post-completion summaries only.
  FE: regenerate disabled while external-source lookup loads/errors; "no source" copy no longer claims "created here".
  Orchestrator: restored accidentally deleted `.env.example` (untracked `.env` left alone); poll moved into the page
  (`hooks/useRegenerationPoll.ts`) so switching to a property keeps waiting; notify/regenerate history rows render as
  "label: value" (no struck-through from); 5-arg job overload documented as the auto-approval entry point; tests for
  "no appraisal number" and notifyExternal=false (mutation-checked: removing the guard turns it red).
- R2 BE: external source now also requires an active integration.WebhookSubscriptions row (APPRAISAL_COMPLETED or
  catch-all) — otherwise history would record a notification WebhookService drops (+test); silent run that attached
  nothing now fails the job (+test); `ExternalNotificationSkipped` history key when a sourced appraisal is
  deliberately not notified (FE label "ไม่แจ้งระบบต้นทาง"). FE: Regeneration type → utils; shared ACCEPT constant.
- R3: both clean on correctness. Doc/wording drift fixed; pane keyed by appraisalId; orchestrator: notifyExternal=false
  without a reason → 400 REASON_REQUIRED.
- Declined with reason: Notify DocumentReady D042-or-D043 (log line only); concurrent replace → 500 (pre-existing);
  extra external-source query (keeps the "sends" rule in one place); poll baseline refetch not awaited (reason typing
  outlasts it); property-groups error looks like "no properties" (hook has no error state, pre-existing).
- Deploy note: new regenerate jobs use the 6-arg RunAsync — an OLD build sharing Hangfire storage cannot load them.
- Final: BE build green, Appraisal.Tests 497/500 (3 = main baseline), integration 10/10; FE tsc 0, eslint 0 on
  changed files, vitest 34 passed.

---
# Phase 3 — page layout (user chose mock variant A, 2026-09-29)
Mock: https://claude.ai/artifact/YB6LbJrEtBfTxgteSbATZS (FE `docs/poc/data-correction-layout-mock.html`, variant A).
User: "เอาแบบ A เอาแก้ N ด้วย หัวหน้าพอแล้ว". FE only, existing APIs, property form itself unchanged.

- Page header (once, not per pane): appraisal number, customer name, status badge, approved date, source-system chip
  (from external-source; hidden when null), "แก้ไขหลังปิดงาน N ครั้ง · ล่าสุด <date>", and a "ประวัติการแก้ไข (N)" button.
- History = SlideOverPanel from the right (shared/components/SlideOverPanel), opened from the header button and from
  "ดูทั้งหมด →". Unified list with filter ทั้งหมด / ทรัพย์สิน / เอกสาร, grouped by day, each entry's target
  (property name or "เอกสาร") is clickable → selects that rail entry and closes the panel.
- The per-pane "ประวัติการแก้ไข" toggles go away (property editor + documents pane).
- Rail: section "ทรัพย์สิน (n)" with items grouped under their property group; section "เอกสาร" with the documents
  entry. Each item shows a "แก้ N" badge (count of history rows for that property / document rows) and an amber dot
  while that property's form has unsaved changes (dot wins over the badge).
- Property pane: under the title, "แก้ไขหลังปิดงาน N ครั้ง" / "ยังไม่เคยแก้ไขหลังปิดงาน", plus a small "แก้ไขล่าสุด" card
  (latest row for that property) with "ดูทั้งหมด →".
- [x] FE Phase 3   - [x] verify → /code-review until clean

## Phase 3 log (2026-09-29)
- Header: shared SectionHeader (house rule) — number + customer (request.customers[0], like the 360 page); status,
  approval date, source chip, edit count follow it; history button far right. Approval date + source come from
  GET /appraisals/{id}/correction-context (renamed from external-source, now also returns completedAt; + integration
  test running the Dapper query on the real schema).
- User feedback applied: history drawer styled like the mock (kind-coloured dot + label, teal target link, change
  grid with struck "from", "เหตุผล:" line, "HH:mm · user"; filter chips on the drawer's title row via a new optional
  SlideOverPanel `headerActions` prop); one framed block like the mock (header bar with bottom border, white rail with
  right border, grey main area); rail section label + count, dashed group rules, icon tiles, selected accent bar,
  violet "แก้ N" badge, focus-visible rings on rail items / chips / links.
- Review: R1 fixed default-property flip while groups load, false "no corrections" on error (retry), "(0)" while
  loading, duplicated change-row markup; R2 clean (rail count = rendered rows, retry handler).
- Left as is: pane titles (text-lg, pre-existing editor style) are larger than the SectionHeader page title; strip
  date uses the shared documentShared formatter (Western year) like the file rows.

---
# Phase 4 — correction editor = the real property forms, every field editable (PLAN, awaiting decisions)

User (2026-09-29): reuse the real property page form so a change is made in one place; "แก้ได้หมดจริง รวมฟิลด์ราคา";
same branch; fix the two latent bugs in the same work.

## Findings
- FE: every correction form body, LandTitleTable/modal, BoundaryFields and a 3,759-line `configs/fields.ts` are
  COPIES taken 2026-08-23 and already drifted (e.g. landDescription max 100 vs 4000, 10 toggles still checkboxes).
  Real bodies (`appraisal/forms/*DetailForm`, TitleDeedForm, RentalInfoForm) are standalone RHF components — reusable.
  ~5,400 lines deletable. VEH/VES have no real page (keep generated form).
- BE: every real update command is `ITransactionalCommand`, has no status/ownership check, and can be dispatched from a
  transactional outer handler (TransactionalBehavior skips the inner commit). Full GET per type exists for snapshots.
- Traps: update commands are FULL OVERWRITE (omitted scalar → null, omitted ConstructionInspection → cleared,
  IsRentedOut≠true → lease+rental cleared) ⇒ FE must send the complete page payload, never a dirty-only diff.
  LandTitle.Update never changes TitleNumber/TitleType (also true on the real page). Depreciation periods / work
  details / rental entries get new ids every save ⇒ audit diff must key by content, not id. Condo update re-derives
  BuildingInsurancePrice from TODAY's fire-insurance rate. RecomputeAsync overwrites the reviewer's approved triple and
  can publish AppraisalValueChanged to Workflow (already true for today's correction path). Stored pricing
  (PricingFinalValue, FinalAppraisedValue) is never recomputed by property updates; LOS result reads areas LIVE but
  values STORED. Collateral master/engagement are untouched by corrections (as today). PMA update pushes to LOS.

## Plan (after decisions)
0. HTML mock of the correction editor on the real form (reason + price banner + confirm dialog with table diffs).
1. BE `CorrectPropertyData` v2: transactional; snapshot (GET) → dispatch the real Update*Command → save → snapshot →
   content-keyed JSON diff → audit row. Completed-only, permission, reason. Title number/type made updatable in the
   shared update (benefits the real page too). Remove PropertyCorrectionData/CorrectionDiff/ApplyCorrection + tests.
2. FE: correction editor renders the real bodies with the real schemas (incl. createLeaseAgreement*Form → fixes the
   LSB/LSU lease bug and the invisible-required-field bug), sends the real page payload mapper output + reason.
   Delete the copies (fields.ts, forms/*, LandTitleTable/modal, BoundaryFields, toCorrectionRequest, dtoMembers).
   fieldLabels → real labels. Per type: Land → B → LB → U → MAC → LS*. Confirm dialog diff from the server? (see D4)
3. Verify per type: payload equality vs the real page, audit rows, no data wiped (construction, rental), tests.

## Phase 4 decisions (user, 2026-09-29)
- Every field editable, incl. price-feeding ones. Approved values are ALWAYS kept: the correction path must not run
  RecomputeAsync (also fixes the reviewer-figure overwrite + Workflow AppraisalValueChanged side effect).
- Same branch. Fix the invisible-required-field and LSB/LSU lease bugs as part of this.
- Mock first: https://claude.ai/artifact/Y1zy6ZkqgAEnD1XhbvoMKX (FE docs/poc/data-correction-real-form-mock.html).
- Open: condo insurance re-derivation (keep unless its inputs changed vs always re-derive) — explained, awaiting answer;
  "กระทบราคา" tags on fields; reason placement.

## Phase 4 — FINAL design (user approved 2026-09-29)
User: separate the correction command so ValuationAnalyses and other derived values are never touched; no "กระทบราคา"
tags; keep the existing reason section + confirm dialog.

### BE contract
- Extract the "write the payload into the property" part of every real update handler into a shared applier
  (precedent: `LandPmaApplier`): Land, Building, LandAndBuilding, Condo, Machinery, Vehicle, Vessel,
  LeaseAgreement{Land,Building,Condo,LandAndBuilding}. Real handlers = applier + their existing side effects
  (RecomputeAsync, condo insurance derivation) — BEHAVIOUR OF THE REAL PAGES MUST NOT CHANGE (characterise first).
- `PUT /appraisals/{appraisalId}/properties/{propertyId}/data-correction/{suffix}`, policy `appraisal.data-correction`,
  NO RejectClosedAppraisalWriteFilter. `{suffix}` = the real route suffix (land-detail, building-detail,
  land-and-building-detail, condo-detail, machinery-detail, vehicle-detail, vessel-detail,
  lease-agreement-{land,building,condo,land-building}-detail).
  Body `{ "reason": string, "data": <exactly the body the real PUT {suffix} accepts> }`.
  200 `{ "changedFieldCount": n, "changedFields": ["Land.OwnerName", ...] }` · 400 `NO_CHANGES` when the diff is empty ·
  409 `APPRAISAL_NOT_COMPLETED` · 404 unknown appraisal/property · 400 when {suffix} does not match the property type.
- Handler: ITransactionalCommand<IAppraisalUnitOfWork>; Completed only; snapshot BEFORE → applier → flush → snapshot
  AFTER → diff → one AppraisalPropertyCorrectionLog row (existing table/shape). NEVER RecomputeAsync, never touch
  ValuationAnalyses / pricing. Condo BuildingInsurancePrice: keep the stored value unless UsableArea or the
  fire-insurance code changed (then derive as the real handler does).
- Diff paths: `Section.Field` (e.g. `Land.OwnerName`, `Building.TotalBuildingArea`); collections keyed by a stable
  business key, not regenerated ids: `Titles[#<titleNumber>].Rai`, `LandAreaDeductions[<n>].Area`,
  `DepreciationDetails[<n>].Periods[<n>].DepreciationPercent`, … Row added → `{from:null,to:"<row summary>"}`,
  removed → `{from:"<row summary>",to:null}`. Skip ids and purely computed members (rental ScheduleEntries,
  timestamps).
- LandTitle.Update + title sync: allow changing TitleNumber/TitleType of an existing title (fixes the real page too).
- Remove the old PATCH data-correction endpoint, PropertyCorrectionData, CorrectionDiff, ApplyCorrection and their
  tests; keep GET property-corrections unchanged (old rows stay readable).

### FE contract
- Correction editor renders the REAL form bodies (appraisal/forms/*, TitleDeedForm, RentalInfoForm, construction if the
  real page has it) with the REAL schema (createLeaseAgreement*Form for LS*), defaults/toForm exactly as the real page,
  and submits `{ reason, data: <the real page's payload mapper output> }` to the new endpoint.
- NEVER wipe data: whatever the real payload would carry but the correction pane does not render must still be sent
  with its current value (the update is full-overwrite).
- Keep: reason section, confirm dialog (now also listing table row changes), UnsavedChangesDialog, onDirtyChange,
  latest-edit card, history. VEH/VES keep the generated form but post to the new endpoint.
- Delete the copies: configs/fields.ts, forms/*, LandTitleTable, LandTitleInputModal, BoundaryFields,
  toCorrectionRequest(+test), dtoMembers(+test), diffRows (replace with a generic form-values diff), point
  fieldLabels/propertyFieldLabel at appraisal/configs/fields.ts. History labels must handle `Titles[#1234]` keys.

- User addendum: the property pane uses the SAME TABS as the real page (land / lease / rental, construction tab for
  B/U/LB), not one long scroll; reason + action bar stay visible on every tab; switching tabs keeps form state.

### Phase 4 todo
- [x] BE appliers + real handlers refactored, characterisation tests green
- [x] BE correction endpoint + diff + audit + tests; old path removed
- [x] FE editor on real bodies per type; copies deleted; tests
- [ ] verify (build/tests/tsc/eslint/vitest) → /code-review until clean

## Phase 4 BE log (2026-09-29)
- Appliers: `Update*/<X>PropertyApplier.Apply(property, command[, insurancePrice])` (11) + `Shared/LandDetailSync` (titles +
  deductions, one copy for the 4 land-family appliers). Real handlers = lookup + applier + their old side effects.
- Endpoint suffix must equal the property's OWN type (`land-detail` is refused for a lease land property: its command has
  no lease section and would clear the lease).
- Diff paths = domain snapshot sections `Land|Building|Condo|Machinery|Vehicle|Vessel|LeaseAgreement|Rental|ConstructionInspection`
  + domain member names (value objects hoisted: `Land.Province`, `Land.DopaProvince`, `Land.Latitude`). Children:
  `Land.Titles[#<deedNo>].Rai|TitleNumber|…`, `Land.Deductions[<n>].AreaInSqWa`, `Building.DepreciationDetails[<n>].<Field>`,
  `Building.DepreciationDetails[<n>].DepreciationPeriods[<n>].DepreciationPerYear`, `Building.Surfaces[<n>].*`,
  `Condo.AreaDetails[<n>].*`, `Rental.UpFrontEntries[<n>].*`, `ConstructionInspection.WorkDetails[<n>].*`.
  Row added/removed = one entry `{from:null|"<row summary>", to:"<row summary>"|null}` at the row path.
- Shared: `BadRequestException` gained an optional `Code`; the global handler emits it as `errorCode` (NO_CHANGES).

## Phase 4 log (2026-09-29)
- BE: 11 appliers (Land, Building, LandAndBuilding, Condo, Machinery, Vehicle, Vessel, LeaseAgreement×4) +
  `Shared/LandDetailSync`; real handlers keep RecomputeAsync / condo insurance / UpdateAsync (28 characterisation
  tests). `PUT …/data-correction/{suffix}` {reason,data}: transactional, suffix must match the type, snapshot of the
  domain detail before/after, SnapshotDiff (id-paired where stable, position-paired where regenerated, ""/false vs
  null not a change), NO_CHANGES 400 (BadRequestException got an optional Code), never RecomputeAsync; condo insurance
  re-derived only when UsableArea/fire code changed. LandTitle number/type updatable. Old PropertyCorrectionData /
  CorrectionDiff path + tests removed. Paths: `Land.Titles[#1234].Rai`, `Land.Deductions[1].AreaInSqWa` (1-based).
- FE: `appraisal/utils/propertyFormRecipes.ts` (schema/toForm/toPayload per type) used by the Create*Pages and the
  editor; `PropertyTabs` renders the real EditorTabBar + panels (same tabs/labels as the pages); request = full form
  values minus reason (NOT zod output); `formDiff` for the confirm dialog; 17 copy files / 6,295 lines deleted.
  LandTitleTable modal save merges over the stored row (keeps id + unshown fields — also fixes the real pages).
  Editor refetches on open and seeds only from data newer than mount; error states for detail and rail.
- Reviews: FE 5 rounds (data-loss bugs fixed: zod-stripped payload, title id/remark loss, stale cache overwrite,
  phantom dirty, first-seed skip, rail error on background refetch). BE 3 rounds (false diffs, condo check).
- Verification: BE build green; Appraisal.Tests 517/520 (3 = main baseline); Appraisal integration 49/49; full
  integration 38 failures — ALL also fail on a clean origin/main worktree (main: 100 failures), 0 branch-only.
  FE tsc 0; eslint 0 new vs origin/main baseline; vitest 133.
- Open for the user: the REAL property pages still submit zod output (titles recreated each save, undeclared keys
  nulled) — pre-existing on main, not changed here.

## Phase 4.1 — chrome polish around the real form (2026-09-30)

FE only, nothing in the form itself. Checked on the running app (3000/7111, read-only probe, no writes).

- Property pane opens with the property page's own `EditorIdentityCard` + `PropertyTypeChip` (was a bare h2 + grey chip).
- Pane padding moved off the pane wrapper onto the content: the sticky tab bar now reaches the pane's top edge
  (was a ~40px white gap above it), the scrollbar sits on the pane edge, the action bar spans the pane.
- Page header strip: slate gradient -> primary tint (`from-primary/15 to-primary/5`), same family as the form's section bands.
- Rail edit badges follow the history palette: property blue, documents violet (both were violet).
- Reason field label uses `confirmDialog.reasonLabel` (was the English literal "Reason for correction"); gap above the reason section halved.
- Documents pane: title `text-base`, list card `rounded-xl border-gray-200` to match the other cards.
- Tried and dropped: identity card on the documents pane — its skyline sits under the attach button.
