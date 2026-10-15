# Credit appraisal tracking — test checklist

Nothing is committed. Both repos have uncommitted work; the frontend tree is shared with other
sessions, so check `git status` before assuming a file belongs to this change.

## Before you start

```bash
# 1. Apply the two new migration scripts (they are journalled; `migrate` runs them once)
dotnet run --project Database/Database.csproj migrate

# 2. RESTART the API. MenuTreeCache has no TTL, so the new menu node will not appear otherwise.
dotnet run --project Bootstrapper/Api

# 3. Affected users must RE-LOGIN — permissions ride in the access token as a claim.
```

⚠ `20260914090100_Revoke_...` removes `APPRAISAL_VIEW` from the **Inquiry** and **Report** roles
(~1,043 + 203 users on the dev database). On a shared database that changes what other people see.

## Accounts you need

| Audience | Permissions | How to get one |
|---|---|---|
| **Credit** (the new audience) | `APPRAISAL_TRACKING_VIEW`, no `APPRAISAL_VIEW` | any user in role `Inquiry` or `Report` after the scripts run |
| **Appraisal team** | both codes | `IntAppraisalStaff`, `IntAppraisalChecker`, … |
| **External firm** | both codes + a `company_id` claim | `ExtAppraisalStaff` of a valuation company |
| **Neither** | no appraisal code | `RequestChecker` |

For API-only testing, `X-Dev-Auth: dev-bypass` is a superuser. To exercise the CREDIT responses
locally, comment out the `APPRAISAL_VIEW` claim in `DevAuthenticationHandler` and keep the tracking
one — that is exactly the shape the revoke script leaves a credit user in.

---

## 1. Permissions and menu

- [ ] Credit user sees ONE appraisal menu entry: **ค้นหา/ติดตามงานประเมิน** → `/appraisals/search`
- [ ] The old **Appraisal** menu group (Search / My Appraisals / Pending Review) is gone for everyone
- [ ] Appraisal-team roles still reach the appraisal list from the menu (the grant script's step 2b
      gives `APPRAISAL_TRACKING_VIEW` to every role that already held `APPRAISAL_VIEW`)
- [ ] Credit user typing `/appraisals/{id}` directly → redirected to `/`
- [ ] **RequestChecker** (holds neither code) opens a pool task someone else is working on →
      lands on the appraisal workspace, NOT on `/`
      *(regression found in review; the guard denies the tracking audience, it does not allow-list)*

## 2. The list page

- [ ] Credit: appraised value is blank on every row that is not `Completed`
- [ ] Credit: appraised value shows on `Completed` rows
- [ ] Credit: **no Export button**; calling `/appraisals/export` directly → 403 and the toast shows
      the server's own sentence, not a generic line
- [ ] Appraisal team: value and Export unchanged
- [ ] External firm: still sees only its own company's appraisals (scoping is on the list/search/
      export, and was NOT changed by this work)
- [ ] Quick search (global search bar) as credit → opens the slide-over on the list page, not the
      appraisal workspace
- [ ] Quick search as appraisal team → opens the appraisal workspace as before
- [ ] Open the slide-over, refresh the page (`?appraisal={id}` in the URL) → the panel reopens on
      the same appraisal, and the parameter is then stripped

## 3. The slide-over — value release rule

| Caller | Status | Expect |
|---|---|---|
| Credit | InProgress | amber lock, "ยังไม่เปิดเผยราคาประเมิน", no documents |
| Credit | Completed | three figures + document list |
| Credit | Cancelled | grey ban icon, "คำขอนี้ถูกยกเลิก", no documents |
| Appraisal team | InProgress | figures shown, documents shown |
| Appraisal team | Cancelled | cancelled notice where the figures are, **documents still shown** |

- [ ] Open DevTools → `/appraisals/{id}/brief` response for a locked appraisal carries **no**
      `appraisalValue` and **no** `documentId` values at all (not merely hidden in the UI)

## 4. The slide-over — content

- [ ] **Header**: appraisal number, status badge, customer, co-borrower count, due date
- [ ] Due date in red with "(เลยกำหนด)" when past — but **not** on a cancelled appraisal
- [ ] **ข้อมูลคำขอ**: เลขที่ประเมินครั้งก่อน → **วงเงินขอกู้** → วันนัดหมาย → ผู้ประเมิน → ที่ตั้ง
- [ ] **Current holder card**: name + `(login)`, step, held-for, phone (plain text), email (link),
      Teams link
- [ ] Holder card shows **—**, not "ยังไม่ได้มอบหมาย", while the brief is still loading
      *(throttle the network to Slow 3G to see it)*
- [ ] A task waiting in a pool names the pool (e.g. `IntAdmin`) with a **group icon**, not initials
- [ ] **ติดต่อฝ่ายประเมิน**: IntAdmin group members, each `ชื่อ (login)` + department
- [ ] **Collateral**: grouped by type, land area summed across every title, building type + floors,
      machine names, condo room + floor, "อื่นๆ" shows the remark
- [ ] Condo-only group renders `ห้อง 1205 ชั้น 12 · จังหวัด` — with the separator, and the line is
      not dropped when the sub-district is empty
- [ ] **Block appraisal** (condo project / housing estate): project summary instead of an item list
- [ ] **3D scene**: auto-fits, no rotation, labels legible, at least 3 buildings drawn when present
- [ ] **Progress rail**: line centred on the circles; a completed step says "เสร็จแล้ว" (never
      "ยังไม่ถึง"); a cancelled step says "ยกเลิกแล้ว"
- [ ] **Timeline**: forward vs returned distinguishable; external firm's steps folded into one
      expandable entry (collapsed by default); inside the fold, the DATE appears on the first row
      and whenever the day changes
- [ ] Every person in the timeline shows `ชื่อ (login)`; firms and pools show no login
- [ ] Switch language en / th / zh → every string translates, including the whole tracking panel

## 5. Documents

- [ ] Completed appraisal: D043 (สรุปผลการประเมิน) and D001 (เล่มรายงาน) lead the list
- [ ] View opens in a tab; Download saves the file
- [ ] **Click Download on both primary documents in quick succession → BOTH files save**
      *(this was broken: one shared mutation cancelled the first save silently)*
- [ ] A failed download shows a toast

## 6. History Search (regression watch)

- [ ] Credit user: History Search still works and pins still open
- [ ] Credit user: the **"เปิดใบประเมิน"** button in the pin drawer is **hidden**
      *(it used to open a new tab that bounced to the dashboard with no message)*
- [ ] Appraisal team: the button is still there and still opens the workspace

## 7. External valuation firm (regression watch)

- [ ] A firm that has been **invited to quote** can still open its own appraisals
- [ ] A firm can still open an appraisal it is **assigned** to
- [ ] Nothing a firm could reach before this change is now refused

## 8. Error states

- [ ] Block `/appraisals/{id}/brief` in DevTools → the panel still renders the progress rail and the
      timeline, with an amber strip explaining the failure; the header shows neither figures nor a
      lock claim
- [ ] Open the panel for an id that does not exist → "ไม่พบใบประเมินนี้", not "ไม่มีสิทธิ์"
- [ ] A caller holding neither appraisal code hitting the brief by URL → "ไม่มีสิทธิ์ดูรายละเอียดใบนี้"

## 9. Rollback

`20260914090100` prints the exact `INSERT` that restores `APPRAISAL_VIEW` to Inquiry and Report.
Run it, restart the API, have the users re-login.
