# Pricing rollup fixes + atomic selection save

Branch scope: Phase 1 (review bug fixes) + Phase 2 (atomic method/approach save, full-stack).
Old select endpoints are KEPT (decision: 2026-07-26).

## Phase 1 — bug fixes (backend only)

- [x] 1. Hypothesis zero guard — `SaveHypothesisAnalysisCommandHandler.cs:75, :97`
      Only the hypothesis handler's deleted `PropagateValue` used `!(method.MethodValue > 0)`;
      the other nine handlers used `.HasValue`. So the guard is restored LOCALLY here, not in
      the domain — a domain-wide `> 0` would stop legitimate zeros from the other nine paths.
- [x] 2. SelectMethod stale value — `PricingAnalysisApproach.cs:107`
      Split the two callers: `SyncValueFromSelectedMethod` keeps its null-skip (recalculation
      path), `SelectMethod` adopts the target method's value verbatim including null (deliberate
      user action). Makes the existing XML docs true again. Only caller: `PricingAnalysis.cs:275`.
- [x] 3. Reporting margin leak — `partials/summary-head.html:14-17`
      Move `pdf-margin-x:5mm` out of the shared partial into the five summary ROOTS so
      `appraisal-book.html` stops inheriting it (book must stay at the 15mm default).
      Roots: appraisal-summary-{block,construction,land-building,condo,machine}.html
- [x] 4. Verify report rendering (headless Chrome, before/after): book back to 15mm,
      five summaries still 5mm.

### Deliberately NOT fixed
- Equality short-circuit (`PricingAnalysis.cs:321`) — KEEP. It is load control for the batched
  save: without it every `UpdateMethodValue` in step 1 fires a full ValuationAnalyses recompute.
- `RecalculateRollup` re-deriving every approach — latent. Needs a manual approach-value, which
  only `PUT /approaches/{id}` can set, and no frontend code calls that endpoint.
- `isBlock: false` (`AppraisalFinalValuesChangedEventHandler.cs:44`) — unconfirmed. Investigate
  against a real block appraisal before changing.

## Phase 2 — atomic method + approach save (full-stack)

Today one Save click = N+1 HTTP requests, N+1 transactions, up to 2 recomputes, and a
partial-failure window (SelectMethod commits, SelectApproach fails -> half-applied selection).

- [ ] 5. Backend: `POST /pricing-analysis/{id}/selection`
      Body: `{ selections: [{ approachId, methodId }], finalApproachId }`
      One command -> one handler -> one transaction -> ONE `AppraisalFinalValuesChangedEvent`.
      Care point: `SelectMethod` and `SelectApproach` both call `SetFinalAppraisedValueInternal`,
      so applying them in sequence would queue duplicate domain events. Apply all method
      selections, then the approach selection, and set the final value ONCE at the end.
- [ ] 6. Frontend: new hook in `api/index.ts`; collapse `saveSummary` steps 2+3
      (`useSelectionActions.ts`) into a single `mutateAsync`. `dirtyMethodApproachTypes` +
      `dirtyApproachSelection` feed the one payload. Remove the now-obsolete ordering comments.
- [ ] 7. Old endpoints stay registered and working (no FE caller after step 6).

## Review

### Phase 1

**1. Hypothesis zero guard** — `SaveHypothesisAnalysisCommandHandler.cs`, both variant branches.
`pricingAnalysis.RecalculateRollup()` is now called only `if (finalValue > 0)`. `method.SetValue(...)`
still runs unconditionally, exactly as before the regression, so only upward propagation is gated.
Scoped to this handler on purpose: a check of every handler's pre-change guard showed nine used
`.HasValue` and only the hypothesis helper used `> 0`, so a domain-wide `> 0` would have suppressed
legitimate zeros from the other nine paths.

**2. SelectMethod stale value** — `PricingAnalysisApproach.SelectMethod` now assigns
`ApproachValue = targetMethod.MethodValue` directly instead of delegating to
`SyncValueFromSelectedMethod()`. The two paths had different needs: the recalculation path must not
let a not-yet-computed method zero a good total (null-skip kept), while a deliberate selection must
not leave the previous method's number behind (verbatim assign, null included). The existing XML
docs on both `SelectMethod` overloads described this behaviour already and are now true again.
Sole caller confirmed: `PricingAnalysis.cs:275`.

**3. Reporting margin** — `pdf-margin-x:5mm` removed from `partials/summary-head.html` (replaced
with a note explaining why it must not live there) and added to the five summary roots:
`appraisal-summary-{block,construction,land-building,condo,machine}.html`.

**4. Verification** — the include graph of every report root was expanded statically and the
renderer's own regex applied to the result:

| effective margin | report |
|---|---|
| 15mm (default) | appointment-letter, **appraisal-book**, meeting-invitation, meeting-minute |
| 5mm | the five appraisal-summary-* roots |

Before the fix `appraisal-book` resolved to 5mm. `dotnet build`: 37 projects, 0 errors.

### Phase 2

**5. Backend** — new `POST /pricing-analysis/{id}/selection` feature folder (`ApplySelection`),
plus `PricingAnalysis.ApplySelection(selections, finalApproachId)` and the
`ApproachMethodSelection` domain record.

The aggregate validates every approach/method up front and only then mutates, so a bad payload
cannot half-apply. Method selections go through the approach-level `SelectMethod`, which raises
nothing; the single `SetFinalAppraisedValueInternal` at the end is the only event-raising call,
so one save produces exactly one `AppraisalFinalValuesChangedEvent`. The "final approach must have
a selected method" invariant is preserved and now satisfiable either by this payload or by a prior
save.

**6. Frontend** — `useApplyPricingSelection` added to `api/index.ts`; `saveSummary` steps 2 and 3
collapsed into one call, so the step numbering shifted (remark is now step 3). Two stale comments
were corrected: the step-0 document comment referred to `selectApproach` as the finalization step,
and the step-1 comment claimed the server "silently skips propagation" when a method value is
missing — after fix 2 it actively clears the approach value, which makes saving values first more
important, not less.

**7. Old endpoints** — `SelectMethod` / `SelectApproach` remain registered and unchanged; they now
have no frontend caller.

**Verification** — `dotnet build`: 37 projects, 0 errors. Frontend `tsc -b`: 518 errors, all
pre-existing baseline (none reference the new symbols — checked by grepping the output for
`applySelection`, `useApplyPricingSelection`, `changedSelections`, `finalApproachId`,
`hasSelectionChange`). `eslint`: 5 remaining errors, all pre-existing `catch (err: any)` blocks.

### Security review
No user input reaches SQL, a shell, or a file path. The new endpoint takes GUIDs only, and both
IDs are resolved *inside* the loaded aggregate — an attacker cannot point a selection at another
analysis's approach or method, since lookups run against `_approaches`/`Methods` of the fetched
aggregate and throw `NotFoundException` otherwise. No secrets or PII added. Authorization matches
the sibling pricing endpoints (none carry an explicit policy — consistent with the existing feature
set, not a change introduced here).

### Not done (deliberate, see above)
Equality short-circuit kept.

### `RecalculateRollup` all-approaches — CLOSED by removing the precondition (2026-07-26)
Domain rule confirmed: an approach value can NEVER be manually input; only a method value can.
The flagged clobbering therefore needs a state the domain does not permit — an ApproachValue that
no method produced. Rather than guard `RecalculateRollup`, the ability to create that state was
removed:

- `ApproachValue` dropped from `UpdateApproachRequest`, `UpdateApproachCommand` and the endpoint's
  command construction. `UpdateApproach` is now weight-only; the endpoint stays registered.
- `UpdateApproachCommandHandler` no longer calls `approach.SetValue(...)` or
  `RollUpFinalFromSelectedApproach()` — nothing it does can change the rollup.
- `PricingAnalysis.RollUpFinalFromSelectedApproach` is now `private`; it was public solely to serve
  that manual path, and `RecalculateRollup` is its only caller.
- `UpdateApproachResult`/`Response` still expose `ApproachValue` as a READ-ONLY echo of the derived
  value — that is fine, it is only the write path that violated the rule.

The sole remaining writer of an approach value is `UpdateFinalValueCommandHandler:50`
(`parentApproach.SetValue(method.MethodValue.Value)`), which derives it from the method. The
invariant now holds by construction rather than by convention.

### `isBlock: false` — CLOSED, finding was wrong (2026-07-26)
Block appraisals do not price through PropertyGroup at all. Block value is the sum of unit prices,
written by `CalculateProjectUnitPrices` when the appraiser saves on the unit-price screen.
Block pricing analyses carry `SubjectType = ProjectModel`, and `SetFinalAppraisedValueInternal`
routes by subject type: `PropertyGroup` raises `AppraisalFinalValuesChangedEvent`, `ProjectModel`
raises `ProjectModelPricingFinalValueChangedEvent`. So the event reaching
`AppraisalFinalValuesChangedEventHandler` can only originate from a PropertyGroup analysis, and
`isBlock: false` is correct there. `RecomputeAsync`'s own comment states the same invariant
("it has no PropertyGroup PAs").

The reviewer's error was equating "a PropertyGroup ROW exists" (true — `AppraisalCreationService`
auto-creates "Group 1" for every appraisal including block) with "a PropertyGroup PRICING ANALYSIS
exists" (false for block — block creates no property rows, so nothing is priced at group level).

Residual condition, if the block flow ever changes: the handler's comment would become wrong the
moment a PropertyGroup pricing analysis can be created on an appraisal that also has a Project row.
The cheap guard would be dropping the `isBlock: false` hint and letting `RecomputeAsync` run its
own detection query — it costs one `db.Projects.AnyAsync` on the pricing-save path.

### Not run
`dotnet test` was not run — per standing preference, builds are used to verify and the test suite
is left to you.
