# Meeting Feature — Study & End-to-End Verification Guide

> Companion to the approved design at `~/.claude/plans/study-about-the-meeting-synchronous-quokka.md`.
> This document is meant for reading straight through, then using section 8 as a hands-on test script.

---

## 1. Context

The meeting feature was reshaped to match the real-world secretary workflow. Before this change:

- Only 4 statuses existed: `Draft`, `Scheduled`, `Ended`, `Cancelled`.
- Meeting number was generated late (at send-invitation).
- Committee members had no rotation concept.
- Routeback existed only at the per-item level; meeting status ignored it.
- The UI papered over missing states with compound labels like "Draft — Cut-Off Done".

After this change:

- **6 statuses** with clearer business meaning.
- **Meeting number** generated at creation, driving committee rotation.
- **Odd/even attendance** config per committee member.
- **Routeback** is now a meeting-level state; routed-back items return to the same meeting after rework.
- **Auto-end**: the meeting ends itself once all decision items are released. No "End Meeting" button.
- **Effective-InProgress** is derived from time, never persisted.

---

## 2. What changed at a glance

### Backend (`~/Developer/collateral-appraisal-system-api`)

**Domain**
- `Modules/Workflow/Workflow/Meetings/Domain/MeetingStatus.cs` — enum replaced with `New, InvitationSent, RoutedBack, Ended, Cancelled`.
- `Modules/Workflow/Workflow/Meetings/Domain/Meeting.cs` — `Create` accepts pre-generated meeting number; `End()` deleted; `ReleaseItem` auto-transitions to `Ended`; `RouteBackItem` sets sticky `RoutedBack`; `CutOff` additive/idempotent; all mutations take `now` and gate on `EnsureNotInProgress`; new `ReinstateRoutedBackItem`.
- `Modules/Workflow/Workflow/Meetings/Domain/MeetingItem.cs` — new `Reinstate(now)` method; flips `RoutedBack → Pending`.
- `Modules/Workflow/Workflow/Domain/Committees/CommitteeMember.cs` — new `Attendance: Always | Odd | Even` field; `Create` accepts it.
- `Modules/Workflow/Workflow/Domain/Committees/Committee.cs` — new `GetActiveMembers(meetingSeq)` parity filter; `AddMember` accepts attendance; new `UpdateMember`.

**Activities**
- `Modules/Workflow/Workflow/Meetings/Activities/MeetingActivity.cs` — re-entry path: if an existing `RoutedBack` item matches `(appraisalId, workflowInstanceId)`, reinstate on the same meeting instead of enqueueing a new queue item.

**Feature endpoints (Carter)**
| Endpoint | Route | Change |
|---|---|---|
| CreateMeeting | `POST /meetings` | Generates `MeetingNo` first, snapshots committee with parity filter |
| BulkCreateMeetings | `POST /meetings/bulk` | Loops the generator for N numbers |
| SendInvitation | `POST /meetings/{id}/send-invitation` | No longer generates number |
| CutOffMeeting | `POST /meetings/{id}/cut-off` | Allowed in `New \| InvitationSent`, idempotent |
| CancelMeeting | `POST /meetings/{id}/cancel` | Effective-InProgress gate applied |
| UpdateMeeting / UpdateMeetingAgenda / UpdateMeetingMembers / AddItemsToMeeting / RemoveItemFromMeeting | various | Same gate applied |
| EndMeeting | — | **DELETED** |
| GetMeetings | `GET /meetings` | Filter `status=InProgress` translates to `Status=InvitationSent AND StartAt<=now`; projection computes effective status |
| GetMeetingDetail | `GET /meetings/{id}` | Returns effective status |
| ReleaseMeetingItem | `POST /meetings/{id}/items/{appraisalId}/release` | Auto-transitions meeting to `Ended` when last Decision item released |
| RouteBackMeetingItem | `POST /meetings/{id}/items/{appraisalId}/routeback` | Sets `RoutedBack` (sticky) |

**Committee endpoints** (in `Modules/Workflow/Workflow/Workflow/Features/Committees/`)
| Endpoint | Route | Change |
|---|---|---|
| CreateCommittee | `POST /api/workflows/committees` | Accepts `Attendance` on each member |
| GetCommitteeById | `GET /api/workflows/committees/{id}` | Returns `Attendance` on each member |
| AddCommitteeMember | `POST /api/workflows/committees/{id}/members` | **NEW** |
| UpdateCommitteeMember | `PATCH /api/workflows/committees/{committeeId}/members/{memberId}` | **NEW** — role + attendance + isActive |
| RemoveCommitteeMember | `DELETE /api/workflows/committees/{committeeId}/members/{memberId}` | **NEW** — soft deactivate |

**Infrastructure**
- Migration `20260421125909_AddMeetingLifecycleRefresh.cs` — remaps stored status strings `Draft→New`, `Scheduled→InvitationSent`, handles `RoutedBack→Scheduled` on rollback; adds `workflow.CommitteeMembers.Attendance nvarchar(16)` default `'Always'`.
- SQL views `vw_MeetingRoster.sql`, `vw_MeetingAgenda.sql` — effective-status CASE expression.

**Tests** — `Tests/Unit/Workflow.Tests/`
- `Meetings/MeetingTests.cs` — 55 tests (lifecycle, cut-off, release, routeback, reinstate, InProgress gate)
- `Meetings/CommitteeTests.cs` — 10 tests (rotation, attendance, member CRUD)
- `Meetings/MeetingMemberTests.cs` — 7 tests
- **Total new tests: 72; all pass. Build clean.**

### Frontend (`~/Developer/collateral-appraisal-system-app`)

**Types & constants**
- `src/features/meeting/api/types.ts` — `MeetingStatus = 'New' | 'InvitationSent' | 'InProgress' | 'RoutedBack' | 'Ended' | 'Cancelled'`; `CommitteeMemberAttendance`.
- `src/features/meeting/constants.ts` — status labels, badge variants, `CUT_OFF_ELIGIBLE`, `EDIT_ELIGIBLE`, `CANCEL_ELIGIBLE`, `ITEM_ACTION_ELIGIBLE` sets.

**Components**
- `MeetingStatusBadge.tsx` — direct status→badge map; no compound labels.
- `MeetingNoBadge.tsx` — number always populated.
- `SendInvitationDialog.tsx` — shows pre-assigned number.
- `CutOffReviewDialog.tsx` — enabled in `New | InvitationSent`; copy "pull newly queued items".
- `MeetingFormDialog.tsx` — create success toast shows meeting number.
- `EndMeetingDialog.tsx` — stubbed placeholder (can be deleted).

**Pages**
- `MeetingListPage.tsx` — status filter with 6 values; row actions gated by new status booleans.
- `MeetingDetailPage.tsx` — End Meeting button removed; all gating uses the eligibility sets.
- `MeetingQueuePage.tsx` — filter values updated.

**Committee admin (new)**
- `src/features/committee/pages/CommitteeAdminPage.tsx` — route `/admin/committees`. Lists committees, adds/edits/removes members, sets Attendance.
- `src/features/committee/api/committees.ts`, `api/types.ts` — React Query hooks.

**Hooks**
- `useEndMeeting` — removed.
- `useReleaseMeetingItem`, `useRouteBackMeetingItem` — invalidate list + detail so auto-end/auto-routeback shows without refresh.

---

## 3. The new lifecycle — state machine

### Persisted statuses (enum values stored in DB)

```
New, InvitationSent, RoutedBack, Ended, Cancelled
```

### Effective status (what the UI displays)

Derived at read time from persisted status + `StartAt`:

| Persisted | Condition | Displayed |
|---|---|---|
| `New` | — | `New` |
| `InvitationSent` | `now < StartAt` | `Invitation sent` |
| `InvitationSent` | `now >= StartAt` | `In progress` |
| `RoutedBack` | — | `Routeback` |
| `Ended` | — | `Ended` |
| `Cancelled` | — | `Cancelled` |

The DB column NEVER stores `InProgress`. It is purely a projection over time.

### Transitions

```
  Create
    │
    ▼
  [New] ──── Cancel ────▶ [Cancelled]
    │
    │ SendInvitation (no new number; just marks sent)
    ▼
  [InvitationSent] ──── Cancel ────▶ [Cancelled]
    │
    │ (time reaches StartAt → displayed as "In progress")
    │
    ▼
  (secretary decisions on Decision items):
    ReleaseItem ─── last Decision item released ──▶ [Ended]
    RouteBackItem ─── any Decision item routed back ──▶ [RoutedBack]

  [RoutedBack] (sticky)
    │
    │ Workflow reworks the routed-back appraisal; when it re-enters
    │ MeetingActivity, the item is ReinstatedToPending on this same meeting.
    │
    │ Secretary ReleaseItem on the reinstated item
    │   if all Decision items now Released ──▶ [Ended]
    │   otherwise stays RoutedBack
    ▼
  [Ended]  (terminal)
```

### Key invariants

1. **MeetingNo is assigned at `Create`** and never changes.
2. **Cut-off** can be called multiple times in `New` or `InvitationSent` — always additive (pulls only new queue items, skips duplicates).
3. **Effective-InProgress blocks mutations**: once `Status=InvitationSent AND StartAt<=now`, all cut-off / cancel / edit / member operations throw. Secretary decisions (release/routeback/reinstate) are still allowed — that's the whole point of the meeting happening.
4. **Ended = all Decision items Released.** Acknowledgement items do not gate Ended.
5. **Routeback sticky**: once any Decision item becomes `RoutedBack`, meeting stays `RoutedBack` until every Decision item is Released.
6. **Routed-back item returns to SAME meeting**: when the workflow brings the reworked appraisal back, `MeetingActivity` finds the existing item and flips it `RoutedBack→Pending`. It does NOT create a new queue item or land on a different meeting.

### Committee rotation rule

When `Meeting.SnapshotCommittee(committee, meetingSeq)` runs (inside `CreateMeeting`), only members whose attendance matches `meetingSeq` parity are copied into `MeetingMember`:

- `Attendance = Always` → always included
- `Attendance = Odd` → included when `meetingSeq % 2 == 1` (meetings 1, 3, 5, …)
- `Attendance = Even` → included when `meetingSeq % 2 == 0` (meetings 2, 4, 6, …)

Secretary can still add/remove members manually on the meeting after snapshot.

---

## 4. Backend API contract (cheat sheet)

### Create a meeting

```bash
curl -X POST https://localhost:7111/meetings \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Approval Meeting #1",
    "notes": null,
    "startAt": "2026-04-25T09:00:00Z",
    "endAt":   "2026-04-25T12:00:00Z",
    "location": "Room A"
  }'
# → 201 Created
# {
#   "id": "0196abcd-...",
#   "title": "Approval Meeting #1",
#   "status": "New",
#   "meetingNo": "1/2569"   ← generated immediately
# }
```

### Cut-off (additive, repeatable)

```bash
curl -X POST https://localhost:7111/meetings/{id}/cut-off
# Pulls queued MeetingQueueItems + pending AcknowledgementQueueItems
# into this meeting. Safe to call multiple times. Skips items already on the meeting.
```

### Send invitation (transitions New → InvitationSent)

```bash
curl -X POST https://localhost:7111/meetings/{id}/send-invitation
# MeetingNo is unchanged; InvitationSentAt timestamp updated;
# MeetingInvitationSentDomainEvent fires (email integration still TODO).
```

### List & filter

```bash
# All meetings (paginated)
curl 'https://localhost:7111/meetings?pageNumber=0&pageSize=20'

# Filter by effective status (including InProgress)
curl 'https://localhost:7111/meetings?status=InProgress'
```

### Release / RouteBack / Reinstate (automatic)

```bash
# Secretary releases an appraisal to approvers
curl -X POST https://localhost:7111/meetings/{id}/items/{appraisalId}/release

# Secretary sends one back for rework
curl -X POST https://localhost:7111/meetings/{id}/items/{appraisalId}/routeback \
  -H "Content-Type: application/json" \
  -d '{"reason": "need additional comparables"}'

# Reinstate is NOT a direct endpoint — it fires automatically when the workflow
# re-enters MeetingActivity for a routed-back appraisal.
```

### Committee member admin

```bash
# Add member with attendance
curl -X POST https://localhost:7111/api/workflows/committees/{id}/members \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "u-123",
    "memberName": "Somchai P.",
    "role": "Director",
    "attendance": "Odd"
  }'

# Update role + attendance + active
curl -X PATCH https://localhost:7111/api/workflows/committees/{cId}/members/{mId} \
  -H "Content-Type: application/json" \
  -d '{"role": "Director", "attendance": "Even", "isActive": true}'

# Soft deactivate
curl -X DELETE https://localhost:7111/api/workflows/committees/{cId}/members/{mId}
```

---

## 5. Frontend surface

### Routes
- `/meetings` — list page with status filter.
- `/meetings/queue` — queue of appraisals awaiting meeting assignment.
- `/meetings/:meetingId` — detail page.
- `/admin/committees` — new committee admin.

### Badge colours (constants.ts)
| Status | Variant |
|---|---|
| New | `info` |
| InvitationSent | `primary` |
| InProgress | `warning` |
| RoutedBack | `danger` |
| Ended | `success` |
| Cancelled | `secondary` |

(confirm the exact variants in `MEETING_STATUS_BADGE_VARIANT`; adjust if your design system differs.)

### Action gating at a glance
| Action | Statuses | Notes |
|---|---|---|
| Edit meeting details / schedule / agenda | `New`, `InvitationSent` | Also blocked by effective-InProgress gate on backend |
| Add / remove items (manual) | `New`, `InvitationSent` | Same |
| Cut-off | `New`, `InvitationSent` | |
| Send invitation | `New` (with ≥1 item) | |
| Cancel | `New`, `InvitationSent` | |
| Release / RouteBack item | `InvitationSent`, `InProgress`, `RoutedBack` | Ended and Cancelled excluded |

---

## 6. Database changes

### Migration: `20260421125909_AddMeetingLifecycleRefresh`

**Up**
1. Adds `workflow.CommitteeMembers.Attendance nvarchar(16) NOT NULL DEFAULT 'Always'`.
2. Remaps `workflow.Meetings.Status` string values: `Draft→New`, `Scheduled→InvitationSent`.

**Down**
1. Remaps back: `New→Draft`, `InvitationSent→Scheduled`, `RoutedBack→Scheduled` (best-effort).
2. Drops `Attendance` column.

⚠ **Rollback caveat**: rows that became `RoutedBack` after Up will be mapped to `Scheduled` on Down — the previous app version has no knowledge of routebacks, so the per-item decision history remains on `MeetingItems` but the meeting itself appears "scheduled and open".

### SQL views
- `vw_MeetingRoster.sql` — computes effective status (`InProgress` when `StartAt <= SYSUTCDATETIME()`).
- `vw_MeetingAgenda.sql` — same.

---

## 7. Known gaps & deferred items

| Item | Severity | Owner decision needed |
|---|---|---|
| `MeetingItem.Reinstate` overwrites `DecisionBy/At/Reason` on the item row, losing the routeback audit trail. Event log still has `MeetingItemRoutedBackDomainEvent`. | Low | Add `LastRoutedBack{At,By,Reason}` columns if per-item audit surfacing matters |
| `GetMeetingDetailEndpoint.cs` uses inline `DateTime.UtcNow` instead of `IDateTimeProvider.ApplicationNow` | Very low | Consistency cleanup — 1-line fix |
| `MeetingActivity` publishes `AppraisalAwaitingMeetingEvent` with no in-repo consumers | Unknown | Either wire the Notification handler or delete the event |
| Email template for invitation | Explicit TODO | Wire when Notification module is ready |
| `EndMeetingDialog.tsx` placeholder file | Cosmetic | Delete |
| `AddMember`, `RemoveMember`, `ChangeMemberPosition`, `SetAgenda`, `UpdateDetails`, `RemoveItem` share the `EnsureNotInProgress` helper but have no direct InProgress-gate test (covered indirectly via 4 other tests) | Low | Optional: add tests if paranoid |
| Frontend `pnpm run build` has 27 pre-existing TS errors in `src/shared/` — none in meeting/committee | External | Pre-existing baseline, not caused by this change |

---

## 8. End-to-end test script

Run these scenarios against a fresh dev environment. Each scenario lists expected observables.

### Preconditions

```bash
# Infra
cd ~/Developer/collateral-appraisal-system-api
docker compose up -d

# Apply migrations
dotnet ef database update \
  --project Modules/Workflow/Workflow \
  --startup-project Bootstrapper/Api

# Start backend
dotnet run --project Bootstrapper/Api

# Start frontend
cd ~/Developer/collateral-appraisal-system-app
pnpm install        # if first run
pnpm dev
```

Open http://localhost:5173 (or whichever Vite port). Sign in as a user with the `MeetingAdmin` role.

Open Seq at http://localhost:5341 for logs.

### Scenario A: happy path — create, cut-off, invite, auto-end

1. **Create a meeting** via the UI ("New Meeting" on the list page).
   - Set `StartAt` to tomorrow 09:00, `EndAt` to tomorrow 12:00.
   - Set the title.
   - **Expect**: toast shows the new meeting number (format `N/BE-year`, e.g. `1/2569`). Badge shows `New`.
2. **Seed the queue** with at least one appraisal that's been forwarded to `MeetingActivity`. Run any flow in the Appraisal module that fires `MeetingActivity` (submit an appraisal that hits the 3-tier approval meeting branch).
3. **Cut-off** from the list page row action.
   - **Expect**: dialog previews the items grouped by appraisal type; on confirm, meeting `cutOffAt` populated; status still `New`; list of items visible on detail page.
4. **Send invitation**.
   - **Expect**: status flips to `Invitation sent`. `MeetingNo` unchanged. `invitationSentAt` populated.
5. **Release each Decision item** one by one (from the detail page).
   - **Expect**: after releasing the last item, meeting status auto-flips to `Ended` without any manual click.

### Scenario B: cut-off twice

1. Create a meeting; leave it in `New`.
2. Cut-off once (item A pulled in).
3. Seed another item (item B).
4. Cut-off again.
   - **Expect**: item B added; item A unchanged (no duplication); `cutOffAt` updated to the later timestamp.
5. Send invitation.
6. Seed a third item (item C).
7. Cut-off again.
   - **Expect**: item C added; meeting still `Invitation sent`.
8. **Backdate** `StartAt` in the DB to 5 minutes ago (or just wait until tomorrow 09:00).
9. Refresh the list.
   - **Expect**: badge flips to `In progress` without any code path changing the DB row.
10. Try to cut-off from the API directly:
    ```bash
    curl -X POST https://localhost:7111/meetings/{id}/cut-off
    ```
    - **Expect**: `400` or equivalent error: "Cannot perform this operation once the meeting has started".

### Scenario C: routeback → rework → reinstate → end

1. Run scenario A through step 4 (meeting is `Invitation sent` with items A, B, C).
2. Backdate `StartAt` so effective status is `In progress`.
3. On detail page, **route back item A** with reason "need more data".
   - **Expect**: badge flips to `Routeback`. Item A row shows `RoutedBack`. Workflow for appraisal A resumes and heads back to the rework step.
4. Release items B and C.
   - **Expect**: meeting stays `Routeback` (not Ended, because A is still RoutedBack).
5. Complete the rework on appraisal A upstream. The workflow re-executes `MeetingActivity`.
6. Refresh the meeting detail.
   - **Expect**: item A's decision flipped back to `Pending`. Meeting still `Routeback`. (The `_reinstatedOnMeetingId` output key indicates the re-entry landed on this meeting.)
7. Release item A.
   - **Expect**: meeting auto-transitions to `Ended`.

### Scenario D: odd/even rotation

1. Open `/admin/committees`.
2. Open the committee that meetings snapshot from (default code `COMMITTEE_WITH_MEETING`).
3. Add two members with the same role `Director`:
   - Alice — attendance `Odd`
   - Bob — attendance `Even`
4. Create a meeting — note the meeting number sequence. Say this one is `N/2569`.
5. Open detail and check members list.
   - **Expect**: if N is odd → Alice present, Bob absent. If N is even → Bob present, Alice absent. Any `Always` directors appear regardless.
6. Create another meeting; the sequence should flip parity.
   - **Expect**: the other one of Alice/Bob appears.

### Scenario E: cancel

1. Create a meeting in `New`.
2. Cancel with reason "postponed".
   - **Expect**: status `Cancelled`, reason stored, `MeetingCancelledDomainEvent` fired.
3. Create another meeting; send invitation.
4. Cancel before `StartAt`.
   - **Expect**: same behaviour. Status `Cancelled`.
5. Backdate `StartAt` so effective status becomes `In progress`.
6. Attempt to cancel via API.
   - **Expect**: rejected with "Cannot perform this operation once the meeting has started".

### Scenario F: filter sanity

1. Create three meetings, each with `StartAt` tomorrow 09:00.
2. Send invitation on all three.
3. Backdate two of them to 1 hour ago.
4. On the list page:
   - Filter by `Invitation sent` → only the future one appears.
   - Filter by `In progress` → only the two backdated ones appear.
   - No filter → all three appear, two badged `In progress`, one `Invitation sent`.

### Scenario G: migration rollback sanity (optional / staging only)

1. On a UAT DB snapshot, record a few `Meetings` rows with statuses `Draft`, `Scheduled`, `Ended`.
2. Apply migration `AddMeetingLifecycleRefresh` (Up).
3. Verify rows now have `New`, `InvitationSent`, `Ended`; committee members have `Attendance = 'Always'`.
4. Route back one item to produce a `RoutedBack` meeting.
5. Roll back the migration (Down).
6. Verify:
   - `New → Draft`, `InvitationSent → Scheduled`, `RoutedBack → Scheduled`.
   - `CommitteeMembers.Attendance` column dropped.
   - Old app version can read without errors.

---

## 9. Verification checklist

Before merging:

**Backend**
- [ ] `dotnet build collateral-appraisal-system-api.sln` succeeds.
- [ ] `dotnet test Tests/Unit/Workflow.Tests` — 72 new tests all pass. Only pre-existing 8–9 unrelated failures remain.
- [ ] `dotnet ef migrations script` produces reasonable SQL for `AddMeetingLifecycleRefresh`.
- [ ] Scenario A (happy path) works end-to-end on dev.
- [ ] Scenario B (multi-cut-off) works.
- [ ] Scenario C (routeback → reinstate → end) works.
- [ ] Scenario D (rotation) works.
- [ ] Scenario E (cancel gating) works.
- [ ] Scenario F (filter sanity) works.

**Frontend**
- [ ] `pnpm run build` no new TS errors in `src/features/meeting` or `src/features/committee`.
- [ ] Meeting list badge colours match the design system.
- [ ] Committee admin page at `/admin/committees` loads and allows add/edit/remove with attendance.
- [ ] End Meeting button is absent everywhere.
- [ ] Meeting number appears immediately after creation.

**Production readiness**
- [ ] Email integration for `MeetingInvitationSentDomainEvent` wired (TODO).
- [ ] Decide on Reinstate audit-trail preservation.
- [ ] Decide on fate of `AppraisalAwaitingMeetingEvent` (wire or delete).
- [ ] Delete `EndMeetingDialog.tsx` placeholder (cosmetic).

---

## 10. Rollback plan

If production reveals a blocker after deployment:

1. **Migration rollback**: `dotnet ef database update <PreviousMigrationName> --project Modules/Workflow/Workflow --startup-project Bootstrapper/Api`. This remaps statuses back and drops the `Attendance` column. `RoutedBack` rows are best-effort mapped to `Scheduled`.
2. **App rollback**: redeploy the previous container. The old app expects enum values `Draft/Scheduled/Ended/Cancelled` — the Down migration ensures those are the only values in the table.
3. **Data inspection before rollback**: run
   ```sql
   SELECT Status, COUNT(*) FROM workflow.Meetings GROUP BY Status;
   SELECT Attendance, COUNT(*) FROM workflow.CommitteeMembers GROUP BY Attendance;
   ```
   Confirm nothing unexpected before reverting.

---

## 11. Reviewer verdicts across all 4 passes

1. Pass 1 — READY WITH FIXES (filter leak, Down migration gap).
2. Pass 2 (after fixes) — fixes confirmed; re-entry path new.
3. Pass 3 (after re-entry) — READY WITH FIXES (effective-InProgress gate warning).
4. Pass 4 (final, after gate) — **READY WITH MINOR CLEANUP**. All critical fixes in; remaining items are audit-trail preservation, inline `DateTime.UtcNow` in detail endpoint, placeholder file deletion — none block merge.

---

## 12. Test count summary

| Test file | New tests |
|---|---|
| `MeetingTests.cs` | 55 |
| `CommitteeTests.cs` | 10 |
| `MeetingMemberTests.cs` | 7 |
| **Total new tests** | **72** |

All green. Pre-existing failures (8–9 across `CompanySelectionActivity`, `RoutingActivity`, `AppraisalCreated` consumer, `DocumentFollowups`) are unrelated and unchanged.

---

## 13. Files modified — canonical list

### Backend (`~/Developer/collateral-appraisal-system-api`)

```
Database/Scripts/Views/Workflow/vw_MeetingAgenda.sql
Database/Scripts/Views/Workflow/vw_MeetingRoster.sql

Modules/Workflow/Workflow/Data/Configurations/CommitteeMemberConfiguration.cs
Modules/Workflow/Workflow/Domain/Committees/Committee.cs
Modules/Workflow/Workflow/Domain/Committees/CommitteeMember.cs

Modules/Workflow/Workflow/Meetings/Domain/Meeting.cs
Modules/Workflow/Workflow/Meetings/Domain/MeetingItem.cs
Modules/Workflow/Workflow/Meetings/Domain/MeetingStatus.cs

Modules/Workflow/Workflow/Meetings/Activities/MeetingActivity.cs

Modules/Workflow/Workflow/Meetings/Features/AddItemsToMeeting/AddItemsToMeetingEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/BulkCreateMeetings/BulkCreateMeetingsEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/CancelMeeting/CancelMeetingEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/CreateMeeting/CreateMeetingEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/CutOffMeeting/CutOffMeetingEndpoint.cs          (minor)
Modules/Workflow/Workflow/Meetings/Features/EndMeeting/                                     (DELETED folder)
Modules/Workflow/Workflow/Meetings/Features/GetMeetingDetail/GetMeetingDetailEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/GetMeetings/GetMeetingsEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/RemoveItemFromMeeting/RemoveItemFromMeetingEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/SendInvitation/SendInvitationEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/UpdateMeeting/UpdateMeetingEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/UpdateMeetingAgenda/UpdateMeetingAgendaEndpoint.cs
Modules/Workflow/Workflow/Meetings/Features/UpdateMeetingMembers/UpdateMeetingMembersEndpoint.cs

Modules/Workflow/Workflow/Workflow/Features/Committees/CreateCommittee/CreateCommitteeEndpoint.cs
Modules/Workflow/Workflow/Workflow/Features/Committees/GetCommitteeById/GetCommitteeByIdEndpoint.cs
Modules/Workflow/Workflow/Workflow/Features/Committees/AddCommitteeMember/                  (NEW)
Modules/Workflow/Workflow/Workflow/Features/Committees/UpdateCommitteeMember/               (NEW)
Modules/Workflow/Workflow/Workflow/Features/Committees/RemoveCommitteeMember/               (NEW)

Modules/Workflow/Workflow/Infrastructure/Migrations/20260421125909_AddMeetingLifecycleRefresh.cs  (NEW)

Tests/Unit/Workflow.Tests/Meetings/MeetingTests.cs
Tests/Unit/Workflow.Tests/Meetings/CommitteeTests.cs                                        (NEW)
Tests/Unit/Workflow.Tests/Meetings/MeetingMemberTests.cs                                    (likely NEW)
```

### Frontend (`~/Developer/collateral-appraisal-system-app`)

```
src/app/router.tsx                                                                          (added /admin/committees)

src/features/meeting/api/types.ts
src/features/meeting/api/meetings.ts
src/features/meeting/constants.ts
src/features/meeting/schemas/meeting.ts

src/features/meeting/components/MeetingStatusBadge.tsx
src/features/meeting/components/MeetingNoBadge.tsx
src/features/meeting/components/SendInvitationDialog.tsx
src/features/meeting/components/CutOffReviewDialog.tsx
src/features/meeting/components/MeetingFormDialog.tsx
src/features/meeting/components/EndMeetingDialog.tsx                                        (stubbed placeholder)
src/features/meeting/components/BulkCreateMeetingsDialog.tsx                                (minor copy)

src/features/meeting/pages/MeetingListPage.tsx
src/features/meeting/pages/MeetingDetailPage.tsx
src/features/meeting/pages/MeetingQueuePage.tsx

src/features/committee/api/types.ts                                                         (NEW)
src/features/committee/api/committees.ts                                                    (NEW)
src/features/committee/pages/CommitteeAdminPage.tsx                                         (NEW)
```

---

## 14. Quick FAQ

**Q: Why is InProgress not a real DB value?**
A: Nothing needs to happen at the exact moment `StartAt` passes. No domain event, no side effect. Persisting would require a cron job to flip rows — pure operational cost for zero business benefit. Deriving at read time is free.

**Q: What happens to the meeting number if a meeting is cancelled?**
A: The number is "wasted" — the sequence has a gap. This matches the existing running-number pattern in the Appraisal / Request modules. Numbering is about ordering, not density.

**Q: Can two meetings share the same committee snapshot?**
A: No. Each meeting runs `SnapshotCommittee` once at creation. After that, the `MeetingMembers` on the meeting are independent of the Committee (they can be added/removed manually).

**Q: What if I want a member to alternate by day of week instead of meeting number parity?**
A: Not supported today. The `Attendance` enum is `Always | Odd | Even` keyed on the running-number seq. Changing this requires a schema extension.

**Q: If an appraisal is routed back three times from the same meeting, does it keep re-entering?**
A: Yes. Each rework cycle triggers `MeetingActivity` which finds the existing item (still in `RoutedBack` until reinstated) and flips it back to `Pending`. The secretary can route it back again, which puts it back in `RoutedBack`, and the cycle repeats until either all items are released (`→ Ended`) or the meeting is cancelled.
