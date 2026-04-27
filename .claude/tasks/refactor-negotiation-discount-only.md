# Refactor negotiation flow to match company quotation page

## Goal
When admin opens a negotiation round, the external company should see the same fee-breakdown UI they used when first submitting — with the **Negotiated Discount** column unlocked — instead of an Accept / Counter / Reject card. The company adjusts per-item discount, then a single Submit sends the response back to the bank.

## Current state
- `RespondNegotiationPanel` shows three buttons (Accept / Counter / Reject) and a single "Counter Price" total field. Per-item discount is **not** captured here.
- `ExtCompanySubmitQuotationPage` already passes `isNegotiating={!!openNegotiation}` to `QuotationFeeBreakdown`, so the NegotiatedDiscount column would already be editable — **but** `canEdit` currently gates on `quotation.status === 'Sent'`, which is false during `Negotiating`. The form is hidden.
- Backend `RespondNegotiation` takes a single `counterPrice` decimal and stores it on `CompanyQuotation.CurrentNegotiatedPrice`. No per-item negotiated-discount persistence path on this endpoint.
- `SubmitQuotation` endpoint already handles per-item save (incl. `negotiatedDiscount` field).

## Approach (chosen for minimal blast radius)

Reuse the existing **SubmitQuotation** path for per-item persistence and recompute totals server-side. Replace the RespondNegotiationPanel with a small read-only banner that shows the admin's note. The company submits via the existing **Submit Quotation** button — the handler detects the open negotiation and routes to negotiation-response logic, which:
- Updates items (incl. `NegotiatedDiscount`)
- Marks the open negotiation as `Countered`
- Recalculates `CurrentNegotiatedPrice` from the new item totals
- Resumes the workflow with `DecisionTaken="Counter"`

This avoids:
- Building a new endpoint surface
- Diverging two near-identical submit pipelines
- Touching the action-bar for a separate "Submit Negotiation" button

## TODO

### Backend
- [ ] In `CompanyQuotation.RespondNegotiation`, allow `Counter` without an explicit `counterPrice` argument; instead recompute total from items.
- [ ] Add `CompanyQuotation.RespondNegotiationWithItems(...)` that:
  1. Marks negotiation `Countered`
  2. Triggers `RecalculateTotalPrice()` (or equivalent) **after items are updated by caller**
  3. Calls `UpdateNegotiatedPrice(TotalQuotedPrice)`
  4. Sets `Status = "Submitted"` (the per-RFQ workflow re-enters admin review)
- [ ] Extend `SubmitQuotationCommandHandler` (or create a small branch) so when `quotation.Status == "Negotiating"` and there's an open negotiation for this company:
  - Update items in place (replace-all is fine; mirror SaveDraft semantics)
  - Call the new `RespondNegotiationWithItems`
  - Publish `QuotationWorkflowResumeIntegrationEvent` with `ActivityId="ext-respond-negotiation"`, `DecisionTaken="Counter"` (instead of the normal `ext-collect-submissions` "Submit")
- [ ] Decide: keep the old Accept/Reject paths working for now (admin-side may still want a way to short-circuit), or remove. **Recommend**: keep the legacy `RespondNegotiation` endpoint untouched; new item-level path is purely additive.

### Frontend
- [ ] Replace `RespondNegotiationPanel` with a slim read-only `NegotiationNoticeBanner` that shows admin's note + round number + "Adjust the per-item Negotiated Discount below and click Submit Quotation."
- [ ] Update `canEdit` in `ExtCompanySubmitQuotationPage` so that `quotation.status === 'Negotiating'` also allows editing (limited to NegotiatedDiscount — `QuotationFeeBreakdown` already enforces this via `isNegotiating`).
- [ ] When submitting during negotiation, no FE branching needed if the backend handler is overloaded — same `submitQuotation` mutation.
- [ ] Workflow advance: `advanceWorkflowStage` is currently called with `'Submit'` action for `ext-collect-submissions`. During negotiation, the resume event is published by the backend handler itself, so **skip the FE workflow advance** when `openNegotiation` is set.

### Removal / cleanup
- [ ] Delete `RespondNegotiationPanel.tsx` after the new banner is wired in (or keep the file and replace its body — to minimise diffs, replace the body).
- [ ] Remove `useRespondNegotiation` hook usage on the FE (keep the API hook itself; admin path may still reference it).

## Files to touch
**Backend**
- `Modules/Appraisal/Appraisal/Domain/Quotations/CompanyQuotation.cs` — add `RespondNegotiationWithItems`
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/SubmitQuotation/SubmitQuotationCommandHandler.cs` — branch on negotiating state
- `Modules/Appraisal/Appraisal/Domain/Quotations/QuotationRequest.cs` — possibly route the handler call

**Frontend**
- `src/features/quotation/components/RespondNegotiationPanel.tsx` — replace body with banner-only
- `src/features/quotation/pages/ExtCompanySubmitQuotationPage.tsx` — extend `canEdit`, skip workflow advance when negotiating

## Risks / open questions
1. **Status transition** — after Counter, should `CompanyQuotation.Status` go back to `Submitted`, `Tentative`, or stay `Negotiating`? Today `Counter` keeps it `Negotiating` (so admin can open another round). Workflow currently expects: ext-respond-negotiation resume → admin-review-negotiation. Keep `Negotiating` → admin reviews → either accepts (Tentative) or opens another round. **Recommend**: keep status as is post-Counter.
2. **Validity of "no submit when no change"** — should we block submit when no items have a non-null NegotiatedDiscount? Probably yes; otherwise admin sees an unchanged response. Add a small client guard + server validation.
3. **Maker/Checker during negotiation** — current Maker→Checker promotion path also needs to work for the negotiation response, or should the negotiation response go straight from Maker? Need to confirm with user.

## Out of scope
- Admin-side negotiation review UI (separate ticket).
- Multi-round visualisation (history of past rounds is already shown elsewhere).

## Review (post-implementation)

### What changed

**Backend**
- `RespondNegotiationRequest` / `RespondNegotiationCommand` accept an optional `Items` list of `(AppraisalId, NegotiatedDiscount?)`.
- `CompanyQuotation.RespondNegotiation` (and the `QuotationRequest` wrapper) accept an optional `IReadOnlyDictionary<Guid, decimal?> itemDiscounts`. When supplied with verb=`Counter`:
  1. Each item's `NegotiatedDiscount` is updated via `SetNegotiatedDiscount`.
  2. `RecalculateTotalPrice()` is called.
  3. `UpdateNegotiatedPrice(TotalQuotedPrice)` snapshots the new total as `CurrentNegotiatedPrice`.
  Legacy callers passing only `counterPrice` keep working.
- `RespondNegotiationCommandHandler` maps the request items into a dict and threads them into the domain.
- The handler still publishes `QuotationWorkflowResumeIntegrationEvent` with `DecisionTaken = command.Verb` ("Counter" or "Reject"), so the frontend doesn't need to call `advanceWorkflowStage` during negotiation.
- **Bug fix**: `OpenNegotiationRound` was passing `Id` (CompanyQuotationId) as both `companyQuotationId` AND `quotationItemId`, and was passing `currentUser.Username` as `initiatedBy` (which the validator requires to be the literal `"Admin"` or `"Company"`). Fixed to pass `quotationItemId: null`, `initiatedBy: "Admin"`, and properly track the admin's `Guid` as `initiatedByUserId`.
- Signature change: `OpenNegotiationRound(string adminUserId, string)` → `OpenNegotiationRound(Guid? adminUserId, string)` and `QuotationRequest.StartNegotiation` follows. Handler now passes `currentUser.UserId`.

**Frontend**
- `useRespondNegotiation` payload extended with `items?: { appraisalId, negotiatedDiscount }[]`.
- `RespondNegotiationPanel` reduced to a notice banner: shows admin's note + round number + a small "Decline this round" link with confirm step (verb=`Reject`).
- `ExtCompanySubmitQuotationPage`:
  - `canEdit` now also returns true for `quotation.status === 'Negotiating' && openNegotiation && isMaker`. The Negotiated Discount column is the only field unlocked during negotiation (already enforced inside `QuotationFeeBreakdown` by `isNegotiating`).
  - New `handleSubmitNegotiation` collects `items` from the form and calls `useRespondNegotiation` with verb=`Counter`. No checker hop. No `advanceWorkflowStage` (backend publishes the resume event itself).
  - The action bar branches: `openNegotiation` → single "Submit Counter Proposal" button; otherwise unchanged Save/Submit-to-Checker/Submit Quotation.

### Files touched
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/RespondNegotiation/RespondNegotiationRequest.cs`
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/RespondNegotiation/RespondNegotiationCommand.cs`
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/RespondNegotiation/RespondNegotiationEndpoint.cs`
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/RespondNegotiation/RespondNegotiationCommandHandler.cs`
- `Modules/Appraisal/Appraisal/Application/Features/Quotations/OpenNegotiation/OpenNegotiationCommandHandler.cs`
- `Modules/Appraisal/Appraisal/Domain/Quotations/CompanyQuotation.cs`
- `Modules/Appraisal/Appraisal/Domain/Quotations/QuotationRequest.cs`
- `app/src/features/quotation/api/quotation.ts`
- `app/src/features/quotation/components/RespondNegotiationPanel.tsx`
- `app/src/features/quotation/pages/ExtCompanySubmitQuotationPage.tsx`

### Compatibility / data
- Schema unchanged — `QuotationItemId` was already nullable in the EF config and migrations.
- Legacy rows with duplicate `QuotationItemId == CompanyQuotationId` remain. Optional cleanup SQL (run only if desired):
  ```sql
  UPDATE appraisal.QuotationNegotiations
  SET QuotationItemId = NULL
  WHERE QuotationItemId = CompanyQuotationId;
  ```
- Legacy `counterPrice`-only respond path still works — admin's existing single-total path won't break.

### Security review
- Authorization unchanged: `RespondNegotiation` still gated by `QuotationAccessPolicy.EnsureCanSubmitQuotation` and the company-invitation check.
- Items input validated inside the aggregate (`SetNegotiatedDiscount` rejects negative or > FeeAmount−Discount).
- No new endpoint surface; reuse of existing endpoint with optional items is additive.
