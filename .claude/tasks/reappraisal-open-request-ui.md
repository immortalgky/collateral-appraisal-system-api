# Reappraisal: Open-Request Status UI

## Goal
Surface `hasOpenRequest` / `openRequest*` fields in list + detail pages, with consistent status badge and amber alert banner.

## Todo

- [ ] 1. `types.ts` — add four new fields to `ReappraisalCandidateListItem` and `ReappraisalCandidateDetail`
- [ ] 2. `ReappraisalListPage.tsx` — add `StatusBadge` helper + Status column after Channel column; fix skeleton/colSpan counts
- [ ] 3. `ReappraisalDetailPage.tsx` — add `StatusBadge` helper; amber banner above detail card; disable Initiate button when consumed/in-progress

## Badge rules (codified)
| Condition | Label | Color |
|---|---|---|
| `status === 'Pending' && !hasOpenRequest` | Pending | green |
| `status === 'Consumed'` | Used | gray (prefer over dual-badge even if hasOpenRequest) |
| `status === 'Pending' && hasOpenRequest` | In Progress | amber |

## Open-request link display
When `openRequestNumber != null`, render `→ {openRequestNumber}` (+ optional `Group {openRequestGroupNumber}`) as a small sub-line / tooltip. `data-request-id={openRequestId}` for future navigation.

## Banner (detail page only)
- Show when `status !== 'Pending' || hasOpenRequest === true`
- Consumed + no openRequestNumber: "This candidate has been used to create a reappraisal request."
- Has openRequestNumber: "This candidate is already in progress. Request {openRequestNumber} · Group {openRequestGroupNumber}"
- Disable Initiate button; tooltip "Already in progress"
- Amber tone matching existing success-modal skipped block

## Constraints
- No new components/libs
- Mirror `SourceBadge` pattern for `StatusBadge`
- Use `!= null` not truthy checks on openRequest fields
- Fix skeleton column count if adding a column to list page

## Review
<!-- filled in after implementation -->
