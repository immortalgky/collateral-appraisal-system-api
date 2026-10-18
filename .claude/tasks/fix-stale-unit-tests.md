# Fix stale BE tests (unit + integration)

Branch `fix/tests/stale-tests` off `origin/main` @ `2f346a98` (ported from `fix/tests/stale-unit-tests` @ `ab11dd6b`). Test-only change — no production code touched.

Port notes (2026-10-05, 42 commits of main):
- `BypassAuthenticationHandler`: main added `FAILED_MESSAGE_VIEW/MANAGE`; kept both, plus our `HISTORY_SEARCH_VIEW`.
- `MeetingTests`: our deletion of `ReinstateRoutedBackItem_WhenPresent_FlipsBackToPending_MeetingStaysRoutedBack` is **dropped**.
  Main (`7dfbbf74`) rewrote that same test as `..._WhenPresent_FlipsBackToPending_AndReopensTheMeeting`, matching the
  current `Meeting.ReinstateRoutedBackItem`, so the file is now identical to main.
- Everything else applied cleanly. Full-solution run on the new base: all green, no new failures —
  Shared 88, Common 61, Auth 49, Reporting 54, Notification 28, Appraisal 558, Workflow 1084 (+1 skipped),
  Collateral 179, Request 32, Integration 276.

## Todo

- [x] Reproduce on a clean worktree: 19 unit failures in 3 projects (Appraisal 3, Notification 6, Workflow 10)
- [x] For each, decide: test wrong vs. contract moved (git history of the production code)
- [x] Appraisal — `FeeAppointmentApprovalDomainTests` ×3
- [x] Notification — `EmailTemplateRendererTests` ×6
- [x] Workflow — `PoolTaskAccessTests` ×2, `CommitteeAddConditionTests` ×2, `MeetingTests` ×1,
      `SubmitDocumentFollowupCommandHandlerTests` ×1, `AppraisalCreatedIntegrationEventConsumerTests` ×4
- [x] Decide: server-side trust of quotation email HTML — **accepted as-is** (2026-10-01, option A)
- [x] Timing flakes in `Workflow.Engine.Expression` — **deleted** (user, 2026-10-01)
- [x] Integration suite: 38 of 201 failing → 198/198 (3 obsolete tests deleted)
- [ ] Decide: `DELETE /requests/{id}` response shape (see Open questions)
- [ ] Decide: `POST /requests` returns 500 on a malformed body (see Open questions)

## Review — unit tests

Every failure was the test lagging behind a deliberate production change. None exposed a production bug.

| Tests | What moved | Commit | Fix |
|---|---|---|---|
| Appointment ×3 | Status lifecycle normalised to Appointed/Pending/Cancelled: `Create()` starts `Appointed`, `Approve()` yields `Appointed` | `97545353` (2026-06-08) | Dropped the now-illegal `Approve()` right after `Create()`; expect `Appointed` |
| Email renderer ×6 | `QuotationSent` content is a ready-made HTML fragment built by the frontend (CA-280), no longer plain text | `cb4e669b` (2026-09-08) | Encoding / `<br/>` cases now pin `MeetingInvitation` only; one new test pins that `QuotationSent` emits the fragment as-is |
| Pool task access ×2 | Internal caller (no company) emits `AssigneeCompanyId IS NULL` with no `@PoolCallerCompanyId` parameter | `ff9da728` (2026-04-29) | Company-clause test passes a company id; null-company test asserts the narrower clause |
| Committee condition ×2 | A blank role gets its own `requires a role` message before the `Allowed values` check | `cac9ba51` (2026-08-04) | Split blank/null into its own theory (also covers whitespace) |
| Meeting reinstate ×1 | Reinstating the last routed-back item returns the meeting to `InvitationSent` | `b0cea37a` (2026-07-24) | Deleted: the newer `..._WhenLastRoutedBackItem_ReturnsMeetingToInvitationSent` covers the identical scenario |
| Document follow-up ×1 | Handler pre-checks missing attachments with a friendlier message before the aggregate guard | `3bc24466` (2026-04-20) | Assert the handler's message |
| Appraisal-created consumer ×4 | Work moved inside `strategy.ExecuteAsync` + a transaction | `d319421e` (2026-07-23) | Two test-setup faults: a non-null `MessageId` drives raw SQL the InMemory provider cannot run, and the auto-substituted execution strategy never ran the delegate. `MessageId` is now null (same bypass as `AppointmentDateChangedConsumerTests`) and the unit of work returns the context's real strategy |
| Expression timing ×3 | — | — | Deleted: asserted wall-clock time, thread scheduling and GC memory |

## Review — integration tests

All 38 were stale tests or test-setup bugs; no production bug.

| Tests | Cause | Commit | Fix |
|---|---|---|---|
| Machinery upsert ×8 | `MachineryAppraisalDetail.ClearInapplicableFields` nulls `RegistrationNumber` when `RegistrationStatus` is false; the seed never set the status | `003ac3f0` | Seed helper sets `registrationStatus` |
| PR4_2/6/7b | Property Ids are DB-generated, so `Guid.Empty` until saved; tests grouped before saving | — (test bug) | Save → `AddPropertyToGroup` → save, like production |
| PR4_3, PR6_4 | One collateral per appraisal: land titles across groups collapse into one master + aliases | `5b7ae023` | Assert one IsMaster + alias; one snapshot group |
| PR7_1 | Condo key needs District/SubDistrict (NOT NULL); a primary condo alias is promoted to master | `8dbef78f`, `5b7ae023` | Seed districts; assert promotion (renamed test) |
| PR6_3 | CI fee read from the prior appraisal's `AppraisalFee`, not the engagement | `d7670a5a` | Seed assignment + fee |
| Lookup / GetById / Restore ×3 | Collateral type is a code (`L`), not `Land` | `81b8afcc` | `type=L`, expect `"L"` |
| Snapshot ×2 | `buildingCost` renamed `buildingValue` | `8dbef78f` | Read `buildingValue` |
| PR8_1/3/5 | Building value only for LB; seed set final values before the analysis was saved; `FinalValueOverride` no longer set by `Create` | `1a9c4739`, `0f6a105b` | Assert null for bare land; roll up after save; set override |
| AS400 result export ×6 | Outbound file now starts from appraisals (`vw_CollateralResultExport`), reappraisals only, redeemed excluded | `23f8a70c`, `56d9857d` | Valuer-code tests reseeded via appraisals; 3 tests for the retired engagement-based query deleted |
| AS400 ingest ×2 | Redemption always wins within a file; `Updated` counts HostCollateralLinks rows | `b57ee102` | Rewrote/renamed one test; expect 3 |
| History search / map pins ×5 | Endpoints need permission `HISTORY_SEARCH_VIEW`; the bypass test user never had it, and "NoAuth" tests were authenticated | — (test bug, never passed) | Grant it in `BypassAuthenticationHandler`; shared `AnonymousWebApplicationFactory` for 401 cases |
| Request create/delete/comment ×3 | Request body contract changed; delete returns a bare `bool` | `192d6029`, `7fb25dfb`, `8dbef78f`, `41bb22a2` | Rewrote JSON test data; read `bool` |

Coverage gap left by the deletions: `vw_CollateralResultExport`'s PrevAppraisalId chain walk and
block-unit matching have no integration test.

## Open questions

1. **Quotation email HTML is trusted from the client.** **Decision (2026-10-01): accept.** Admin-only;
   the stored copy is never rendered in the app. Pinned by `QuotationSent_Content_IsEmittedAsHtml_NotEncoded`.
2. **`DELETE /requests/{id}`** returns a bare `true` (`DeleteRequestEndpoint.cs:15`) while `.Produces<DeleteRequestResponse>()`
   advertises `{ isSuccess }` (since `192d6029`). FE never reads the body. Return the object, or declare `bool`.
   `DeleteRequestTests` now follows the current `bool`.
3. **`POST /requests` returns 500 on a malformed body.** Mapster `request.Adapt<CreateRequestCommand>()`
   (`CreateRequestEndpoint.cs:14`) throws NullReferenceException before validation when `creator`/`detail`
   are missing. Low severity: the FE always sends them.
