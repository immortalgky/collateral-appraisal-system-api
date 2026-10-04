# Property save: no property reload in RecomputeAsync, no DbSet.Update on tracked appraisal

Branch `immortalgky/property-save-no-reload` (Orca worktree). Trigger: `UpdateLandAndBuildingProperty`
cancelled at the FE's 10 s timeout on SIT; Query Store showed the single-query AppraisalProperty graph
statement (query_id 67231/76356, ~16.6K chars) took 18.8 s to compile.

## Decisions (user, 2026-10-03)
- B: `RecomputeAsync` uses the properties the caller loaded through the repository
  (`GetByIdWithPropertiesAsync`, split query). If they are not loaded it throws, rather than
  reloading them itself or summing an empty list to 0.
- D option 1: remove `UpdateAsync(appraisal)` from the 16 Appraisal-module call sites. Accepted
  consequence: `Appraisals.UpdatedAt` now moves only when the appraisal row itself changes, so
  property/group saves no longer bump it. It is read by `vw_MisCasReport` (UpdatedDate),
  `GetAppraisalStatus` (Integration) and `vw_AppraisalDetail`.

## Todo
- [x] Baseline on origin/main: Appraisal unit 550/553 (3 FeeAppointmentApprovalDomainTests failed
      before any change); Integration 168/214 (46 failed before any change, list in scratchpad
      `base-integ-failed.txt`)
- [x] New integration tests `PropertySaveWriteScopeTests` (4). On old code: LandTitles read 2×.
      With B only: read 1×, Appraisals root UPDATE 1×.
- [x] B: guard + use `appraisal.Properties`; FinalValuesChanged / DeletePropertyGroup / AppraisalCreationService (CI branch) load via repo
- [x] D: remove the 16 `UpdateAsync(appraisal)` calls
- [x] Adapt tests that call `RecomputeAsync` without loaded properties (InsuranceConsistencyTests)
- [x] Gates: build 0 errors, Appraisal unit, full Integration vs baseline
- [x] QA: 48 per-handler persistence tests (AffectedHandlers*Tests), branch vs merge-base 3b8ea886

## Review
- Build 0 errors. Appraisal unit 550/553 (same 3 pre-existing). Integration 184/222, no failure outside
  the baseline list; 8 QuickSearch baseline failures now pass (environment, not this change).
- QA: 52 handler tests; 48 pass on branch AND merge-base; 4 `PreExisting_*` fail on both:
  CreateVehicle/CreateVesselProperty drop OwnerName; AddPropertyToGroup/MovePropertyToGroup throw
  "Property not found" when sent alone (GetByIdAsync/FindAsync doesn't load Properties).
  Guard-only experiment (no caller fixes) fails exactly DeletePropertyGroup, pricing event, CI creation.
- Behaviour: Appraisals.UpdatedAt no longer bumps on property/group saves (accepted). Unloaded
  Properties in RecomputeAsync now throws instead of writing InsuranceValue = 0.
- Fixed in this PR (user decision 1-ข): AddPropertyToGroup/MovePropertyToGroup load via
  GetByIdWithPropertiesAsync (Move broke in cc246ea4, 2026-09-22, when the domain started reading
  _properties for the family-type check; Add has read _properties since 2026-01-07, but the FE never calls it);
  CreateVehicle/CreateVesselProperty pass OwnerName. PreExisting_* tests converted to normal asserts.
- Final: build 0 errors; Appraisal unit 550/553 (same 3); targeted 60/60; full Integration 272 total,
  234 passed, 38 failed, all 38 in the baseline list. No migration.

## Code review (3 rounds)
- R1 fixed: FinalValuesChanged reuses Local when Properties loaded / Added; RecomputeAsync XML docs;
  UpdateAsync RESTORED in the 4 assignment handlers (they only touch AppraisalAssignments, so
  Appraisals.UpdatedAt — LOS GetAppraisalStatus, MIS UpdatedDate — would stop moving on assignment).
- R2 fixed: `?? appraisal` (no silent skip), guard moved before pricing queries, SqlCapture scoped by AsyncLocal.
- R3 fixed: stale comment in PropertyCorrectionAuditTests, SqlCapture ctor lock.
- Declined (user decision / out of scope): internal fallback load in RecomputeAsync, CI IsLoaded=true,
  lighter loader (owned types always load), DeletePropertyGroup/Move/Add load cost, test-helper dedup.
- Follow-up idea: stamp Appraisal.UpdatedAt from the audit interceptor when children change, then drop
  the remaining Update() calls (consumers rewrite the whole Appraisals row from memory = lost-update risk).
- Final gates: build 0 errors; Appraisal unit 550/553 (same 3); Integration 275, 38 failed, all in baseline.
