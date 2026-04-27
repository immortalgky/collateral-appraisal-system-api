# Quotation Detail Enhancement — Plan

## Goal

Close the information gap between the current quotation screens and the three provided mockups, without rebuilding anything that already works. Additions are additive where an existing page exists; new pages only where none does.

## Confirmed scope (from Q&A)

| Item | Decision |
|---|---|
| Maker/Checker 2-person approval (mockup 3) | **YES** — real business requirement |
| Tracking log source (mockup 2) | **New audit table populated from domain events** |
| Draft vs. Submit (mockup 3) | **Both** — `Save` (stays Draft) + `Save & Submit` |
| `NegotiatedDiscount` fee line (mockup 3) | **YES** — backend column + API |
| Mockup 2 admin RFQ overview | **Enhance existing `QuotationSelectionPage`**, do not rebuild |
| Mockup 1 admin single-company detail | **New page** — admin currently has no drill-down |
| "Quotation Revision" button (mockup 1) | = trigger an existing **negotiation round** (no new state machine) |

## Phased breakdown

### Phase 1 — Backend foundation (no UI yet)

**Domain**
- `CompanyQuotationItem`: add `NegotiatedDiscount` (decimal, nullable).
- `CompanyQuotation`: add `TotalNegotiatedDiscount` rollup; recompute `TotalQuotedPrice`/`NetAmount` to honour it.
- `CompanyQuotation`: add `"Draft"` status as a new valid initial state (today it jumps straight to `"Submitted"` in `SubmitQuotationCommandHandler`).
- `CompanyQuotation`: add `"PendingCheckerReview"` sub-state; transitions `Draft → PendingCheckerReview → Submitted`.

**Identity / roles**
- Existing `ExtAdmin` role = **Maker** (no migration). Users already allowed to enter quotations keep their access.
- Add one new role: `ExtAppraisalChecker`. Workflow engine assigns the check task to users holding this role.
- Route guards / policy:
  - `ExtAdmin` (Maker) → can save draft + submit-to-checker.
  - `ExtAppraisalChecker` → can save draft + final-submit.
  - A user with both roles can do either path (edge case, allowed).

**New / refactored commands + endpoints**
- `POST /quotations/{id}/draft` — **SaveCompanyQuotationDraft**: Maker or Checker can call; upsert `CompanyQuotation` in `Draft`. Idempotent on re-save.
- `POST /quotations/{id}/submit-to-checker` — Maker promotes `Draft → PendingCheckerReview`.
- `POST /quotations/{id}/submit` — enhanced to accept either a Draft (backwards-compat path) or a `PendingCheckerReview` (Checker path). Validates role.
- Existing `/decline` unchanged.

**Audit log**
- New table `appraisal.QuotationActivityLog`: `Id`, `QuotationRequestId`, `CompanyQuotationId?`, `ActivityName`, `ActionAt`, `ActionBy`, `ActionByRole`, `Remark?`.
- New handler `QuotationActivityLogger` that subscribes to existing domain events and writes a row:
  - `QuotationRequestCreated` → `Quotation creation`
  - `QuotationInvitationSent` → `Quotation opened` (per company)
  - `CompanyQuotationDrafted` (new event) → (not logged; too noisy)
  - `CompanyQuotationSubmittedToChecker` (new event) → `Submitted to Checker` (per company)
  - `CompanyQuotationSubmitted` (new event) → `Quotation submitted`
  - `CompanyQuotationDeclined` (exists) → `Invitation declined`
  - `QuotationShortlistSubmitted` → `Shortlisted Submission`
  - `QuotationAwarded` → `Award submitted`
  - `QuotationSubmissionsClosedIntegrationEvent` → `Submissions closed`
- `vw_QuotationActivityLog` SQL view (paginated read path via Dapper per project convention).
- EF migration.

**Files touched (Phase 1)**
- `Modules/Appraisal/Appraisal/Domain/Quotations/CompanyQuotation.cs`
- `Modules/Appraisal/Appraisal/Domain/Quotations/CompanyQuotationItem.cs`
- `Modules/Appraisal/Appraisal/Domain/Quotations/QuotationActivityLog.cs` (new)
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/SaveDraftQuotation/` (new)
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/SubmitToChecker/` (new)
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/SubmitQuotation/SubmitQuotationCommandHandler.cs` (enhance)
- `Modules/Appraisal/Appraisal/Application/EventHandlers/QuotationActivityLogger.cs` (new)
- `Modules/Appraisal/Appraisal/Infrastructure/Configurations/*` (new config for log table)
- `Database/Scripts/Views/Appraisal/vw_QuotationActivityLog.sql` (new)
- New EF migration in `Modules/Appraisal/Appraisal/Infrastructure/Migrations/`
- `Modules/Auth/...` — role seeding

### Phase 2 — Mockup 3 (Ext-company Maker/Checker page)

Rework `ExtCompanySubmitQuotationPage` into a **single page with role-aware rendering**, not two duplicated pages (simpler; form is 95% the same).

- Left-rail appraisal selector (replaces the current flat field-array layout).
- Right-pane per selected appraisal:
  - Appraisal Report Information (read-only)
  - **Attach document** table — pulls from existing `SharedDocumentViewer` wired per-appraisal.
  - Fee breakdown rows: `Fee Amount` → `Discount` → **`Discount (Negotiate)`** → `Fee After Discount` → `VAT %` → `Net Amount` (client-computed; server-validated).
  - `Max Appraisal Duration (day)`, `Estimate manday(s)`, per-item Remark.
- Footer: `TOTAL FEE AMOUNT` (sum), `Participating Yes/No`, `Quotation Remark`.
- Buttons, role-gated:
  - Maker: `SAVE QUOTATION` (draft) + `SUBMIT TO CHECKER`
  - Checker: `SAVE QUOTATION` (draft) + `SUBMIT QUOTATION`

**New hooks**
- `useSaveDraftQuotation` / `useSubmitToChecker` / (existing) `useSubmitQuotation`.

**Files touched (Phase 2)**
- `src/features/quotation/pages/ExtCompanySubmitQuotationPage.tsx` (rework)
- `src/features/quotation/components/AppraisalLeftRail.tsx` (new)
- `src/features/quotation/components/QuotationFeeBreakdown.tsx` (new)
- `src/features/quotation/api/quotation.ts` (new hooks)
- `src/features/quotation/schemas/quotation.ts` (extend with `NegotiatedDiscount`, `Draft` status)
- `src/app/router.tsx` (update role guards)

### Phase 3 — Mockup 2 (Admin RFQ overview additions)

**Additive** to existing `QuotationSelectionPage`:

- Add **Appraisal Report Listing** table (Report No, Customer Name, Collateral Detail, AO, Max Appraisal Duration). Driven by existing `Appraisals` collection on the RFQ — likely already loaded; just render.
- Extend **External Appraisal Company Listing** with columns: Created On, Updated On, Updated By, Total Discount, Total Estimate Manday, and per-row 📄 icon linking to Mockup 1 detail.
- Add **Cut-Off Date Time** editable field (bound to existing `DueDate`).
- Add **Quotation Tracking Log** panel backed by Phase 1 log endpoint.
- Status-based button gating:
  - `Sent` → nothing admin-facing (workflow auto / timer)
  - `UnderAdminReview` → `SUBMIT SHORTLISTED COMPANIES`
  - `PendingRmSelection` → admin waits
  - `WinnerTentative` → `CANCEL QUOTATION` + `SUBMIT AWARD COMPANY`

**Files touched (Phase 3)**
- `src/features/quotation/pages/QuotationSelectionPage.tsx` (add sections)
- `src/features/quotation/components/AppraisalReportListingTable.tsx` (new)
- `src/features/quotation/components/CompanyListingTable.tsx` (extract + extend)
- `src/features/quotation/components/QuotationTrackingLog.tsx` (new)
- `src/features/quotation/api/quotation.ts` (add `useQuotationActivityLog`)

### Phase 4 — Mockup 1 (Admin single-company detail)

**New page**: `/quotations/:quotationRequestId/companies/:companyQuotationId` (admin-gated).

- Same left-rail + right-pane layout as Phase 2 but read-only fee values.
- Admin-editable footer: `Quotation Revision Remark` textarea.
- Buttons: `CANCEL` + `QUOTATION REVISION` (calls existing negotiate-round endpoint, passing the remark as the negotiation note).
- Entry points: 📄 icon in Phase 3's company listing table; deep-linkable.

**Files touched (Phase 4)**
- `src/features/quotation/pages/AdminCompanyQuotationDetailPage.tsx` (new)
- `src/app/router.tsx` (add route)
- Reuses Phase 2's `AppraisalLeftRail` + `QuotationFeeBreakdown` components in read-only mode.

## Sequencing + verification

Run phases in order — each can ship independently:

1. Phase 1 unblocks everything else; ship a backend PR with migration + endpoints.
2. Phase 2 depends on Phase 1 (`Draft`/`NegotiatedDiscount`/role split).
3. Phase 3 depends on Phase 1 (activity log endpoint).
4. Phase 4 depends on Phase 2 (shared components) and Phase 3 (entry point).

**Verification per phase**
- Phase 1: integration test hitting `/quotations/{id}/draft`, `/submit-to-checker`, `/submit`. Confirm activity log rows appear. `dotnet build` green.
- Phase 2: manual — Maker logs in, saves draft, submits to checker; Checker logs in, final submits. Confirm status and activity log entries via Seq + DB.
- Phase 3: open an existing `UnderAdminReview` RFQ, verify all new sections render with real data; tracking log shows full history.
- Phase 4: from Phase 3 📄 icon, drill into a company's quotation; trigger `QUOTATION REVISION`; confirm negotiation round opens and remark is captured.

## Confirmed decisions (answered 2026-04-23)

- **Discount (Negotiate) semantics**: additive on top of `Discount`. User types the negotiated discount **amount** (not a total). Formula:
  `FeeAfterDiscount = FeeAmount − Discount − NegotiatedDiscount`
  `VATAmount = FeeAfterDiscount × (VATPercent / 100)`
  `NetAmount = FeeAfterDiscount + VATAmount`
- **Checker role**: `ExtAppraisalChecker` — workflow engine assigns the check step to this role. Maker role stays as today's `ExtAdmin` (existing users keep working; no migration needed).
- **Attach document viewer**: reuses existing `QuotationSharedDocument` collection (confirmed) — no new backend work for document metadata.
- **"Construction Inspection"** in mockup 1 title: ignored for this round (label only).

## Simplicity check

Per project rule 7 — every change targets the minimum files. Shared UI components (`AppraisalLeftRail`, `QuotationFeeBreakdown`) are lifted into `src/features/quotation/components/` so they're written once and reused by mockup 1 + mockup 3. Backend reuses existing event plumbing (outbox + domain events) rather than introducing new infrastructure.
