# Periodical Reappraisal (AS400) — How to Test (Step by Step)

End-to-end guide to test the inbound reappraisal flow locally **without an SFTP server**:

```
COLLATREV file (fixed-width: Detail 649 / Header-Trailer 640)  →  ingestion job  →  collateral.ReappraisalCandidates (staging)
   →  list / filter  →  open candidate + nearby group  →  initiate (creates Requests + group number, NOT submitted)
   →  staff submit each request from the request list (candidate becomes Consumed)  →  "not reviewing this round" / restore
```

**One row per book.** AS400 repeats every book still on its due list in every monthly file — it does not
know what CAS has reviewed. A book is `(CollateralId, NormalizedSurveyNumber)` (the survey number with
AS400's block-project `B` prefix dropped). A later file refreshes a Pending / "not reviewing" book, skips
a Consumed one outright, and moves `LastSeenFileDate` forward; the list shows only books on the latest
file. A Consumed book whose reappraisal was cancelled comes back as Pending.

| File in this folder | Purpose |
|------|---------|
| `generate_collatrev.py` | Generates a valid fixed-width COLLATREV file (Detail 649 / Header-Trailer 640, H/D/T records, UTF-8). |
| `AS400_COLLATREV_20260501.txt` | Ready-made sample: 3 detail rows (described below). |

---

## Prerequisites
- .NET 9 SDK, Docker, Python 3 (for the generator).
- Infra running: `docker compose up -d` (SQL Server on `localhost,1433`, sa / `P@ssw0rd`).
- A SQL client (Azure Data Studio / SSMS / `sqlcmd`) and an HTTP client (`curl` / Postman / the `.http` files).

---

## Step 0 — Apply database changes (one-time)

**0. Tables and views** — the API never migrates on startup. The Database tool applies every module's EF
migrations (the table lives in the Collateral module, `collateral.ReappraisalCandidates`; the batch group
number is `request.Requests.GroupTag`) and then the views (`vw_ReappraisalCandidates`) and one-time scripts:
```bash
dotnet run --project Database/Database.csproj migrate
```

**0c. Permission + menu** — seeded automatically on API startup (idempotent). After Step 3 the
`REAPPRAISAL_VIEW` permission, the **Standalone → Reappraisal (AS400)** menu item, and grants to
Admin / IntAdmin / RequestMaker all exist.

---

## Step 1 — (Recommended) pick a real appraisal number
So lat/lon enrichment + 1 km grouping have data to join to, find an existing appraisal number:
```sql
SELECT TOP 5 AppraisalNumber FROM appraisal.Appraisals
WHERE AppraisalNumber IS NOT NULL ORDER BY CreatedOn DESC;
```
You'll pass it as `--survey1` in the next step. (Skip this and row 1 just lists with NULL coords — still valid.)

## Step 2 — Put a COLLATREV file in the inbox
Default source is `Local`; inbox is `Bootstrapper/Api/reappraisal/inbox` (created automatically).
```bash
# generate straight into the inbox, using a real appraisal number for row 1
python3 docs/reappraisal/generate_collatrev.py 20260501 \
  --out Bootstrapper/Api/reappraisal/inbox --survey1 <REAL_APPRAISAL_NUMBER>

# …or copy the ready-made sample as-is
mkdir -p Bootstrapper/Api/reappraisal/inbox
cp docs/reappraisal/AS400_COLLATREV_20260501.txt Bootstrapper/Api/reappraisal/inbox/
```
> To test the real SFTP path instead, set `Reappraisal:FileSource=Sftp` and `Reappraisal:Sftp:*`
> (credentials via user-secrets, never appsettings.json).

## Step 3 — Run the API
```bash
dotnet run --project Bootstrapper/Api
```
API at `https://localhost:7111`. Startup applies migrations (0a) and seeds the permission/menu (0c).

## Step 4 — Trigger ingestion
Open the Hangfire dashboard `https://localhost:7111/hangfire` → **Recurring Jobs** →
`reappraisal-as400` → **Trigger now** (no need to wait for the monthly cron).
Watch the console for `[REAPPRAISAL-AS400]` lines.

## Step 5 — Verify staging
```sql
SELECT Status, SourceFileDate, CollateralId, SurveyNumber, ReviewType, ReviewDate, Latitude, Longitude
FROM collateral.ReappraisalCandidates ORDER BY ReviewType;
```
Expect **3 `Pending` rows**. Row 1 has `Latitude`/`Longitude` populated **iff** its `SurveyNumber` matched an
appraisal; rows 2 & 3 have NULL coords. The file is moved to `Bootstrapper/Api/reappraisal/processed/`.
Re-trigger Step 4 → no duplicates (RowHash dedupe; the file is already archived).

## Step 6 — List API
Endpoints are currently `AllowAnonymous`, so no token is needed. `-k` accepts the dev TLS cert.
```bash
curl -k "https://localhost:7111/reappraisal/candidates"
curl -k "https://localhost:7111/reappraisal/candidates?reviewType=2"
curl -k "https://localhost:7111/reappraisal/candidates?remainingDayTo=0"   # overdue only (row 2)
curl -k "https://localhost:7111/reappraisal/candidates?cifNumber=68057984"
```
Each item: `oldAppraisalReportNumber` (= SurveyNumber), `cifNumber`, `customerName`, `reviewType`,
`appraisalDate` (last appraisal), `reviewDate` (due), `remainingDay` (row 2 negative = overdue), `channel = "AS400"`.

## Step 7 — Candidate detail + nearby group
Take an `id` from Step 6 (row 1):
```bash
curl -k "https://localhost:7111/reappraisal/candidates/<ID>"
curl -k "https://localhost:7111/reappraisal/candidates/<ID>?radiusKm=1"
```
`nearbyGroupCandidates` lists other candidates within the radius — **only** those whose coords were enriched.
With only row 1 enriched, this is empty; to see grouping, give rows 1 & 2 SurveyNos of two appraisals
located <1 km apart, then re-ingest.

## Step 8 — Initiate (create grouped reappraisal requests)
```bash
curl -k -X POST "https://localhost:7111/reappraisal/initiate" \
  -H "Content-Type: application/json" \
  -d '{
        "candidateIds": ["<ID1>", "<ID2>"],
        "requestor": { "userId": "u1", "username": "tester" },
        "creator":   { "userId": "u1", "username": "tester" }
      }'
```
Response: `{ "groupNumber": "68G000001", "createdRequestIds": [], "skipped": [] }`. Requests are created
asynchronously and are **not submitted**. Verify:
```sql
-- one Request per book, all sharing the group number, Channel = SIBS, Status Draft — staff complete it
-- (appointment; for a legacy 99A… book also the property) and submit
SELECT Id, RequestNumber, Channel, GroupTag, Status, ExternalCaseKey
FROM request.Requests WHERE GroupTag = '<groupNumber>';

-- candidates stay Pending (shown "in progress") until each request is submitted
SELECT Id, Status FROM collateral.ReappraisalCandidates WHERE Id IN ('<ID1>','<ID2>');
```
Initiating the same candidates again returns them under `skipped` (`AlreadyInFlight`). Submit a request from
the request list → it goes straight to appraisal-assignment (Channel SIBS skips the initiation check) and
every candidate of that book becomes `Consumed` (a book listed under several collateral is one request). Re-trigger Step 4 → Consumed rows are **not** resurrected or refreshed.

A reappraisal raised by hand from the request page consumes the book too, on submit: purpose `03` (block `09`)
with a prior appraisal whose number is the book. Without that, the book stayed on the to-do list and showed
"ready" again once its reappraisal completed. `20261001120000_DataFix_ConsumeBooksReappraisedByHand.sql`
applies the same rule to books submitted before the change.

The list's third tab, **Processed** (`status=Consumed`), is the whole history of submitted books, on the
latest file or not: one row per book with the reappraisal it produced (newest non-cancelled first, the same
rule as RCAS002), how it was raised (Initiate's group or by hand), submitted and completed dates. A book
whose reappraisal was cancelled stays here, marked cancelled, until the next COLLATREV file reopens it.

## Step 9 — Delete (soft)
```bash
curl -k -X DELETE "https://localhost:7111/reappraisal/candidates/<ID3>"   # → 204
```
The row becomes `Status = Deleted` ("not reviewing this round") and moves to its own tab
(`GET /reappraisal/candidates?status=Deleted`). Later files still refresh it; it stays until restored:
```bash
curl -k -X POST "https://localhost:7111/reappraisal/candidates/<ID3>/restore"   # → 204, back to Pending
```

## Step 10 — Negative / robustness tests
Edit a copy of the sample, drop it in the inbox, trigger, and confirm one bad file doesn't block others:
- Trailer count ≠ number of `D` rows, or non-numeric → file fails, error logged, **left in inbox** (not archived).
- Remove the `T` line entirely → `FormatException` (completeness check).
- Truncate a Detail line below 649 chars → `FormatException`.

## Step 11 — Frontend
Log in as **Admin / IntAdmin / RequestMaker** → **Standalone → "Reappraisal (AS400)"** →
list → open a candidate → tick rows → **Initiate** → success popup shows the group number;
**View on Map** opens History Search centered on the candidate; the row's **Delete** removes it.
Confirm the created requests appear in the normal Request listing with the group number.

---

## Reset between runs
```sql
DELETE FROM collateral.ReappraisalCandidates;
-- optionally remove the test requests created by initiate:
-- DELETE FROM request.Requests WHERE Channel = 'SIBS' AND GroupTag IS NOT NULL;
```
```bash
# move the archived file back to re-ingest it
mv Bootstrapper/Api/reappraisal/processed/AS400_COLLATREV_20260501.txt Bootstrapper/Api/reappraisal/inbox/
```

## Troubleshooting
- **No rows after trigger** — check the inbox path (`Bootstrapper/Api/reappraisal/inbox`), the filename
  matches `AS400_COLLATREV_YYYYMMDD.txt`, and the console `[REAPPRAISAL-AS400]` logs for parse errors.
- **List endpoint 500 / "invalid object name"** — the view isn't deployed; run Step 0b.
- **Menu item not visible** — the logged-in role lacks `REAPPRAISAL_VIEW` (use Admin/IntAdmin/RequestMaker)
  or the seed didn't run; restart the API.
- **All Latitude/Longitude NULL** — row 1's `SurveyNumber` didn't match any `appraisal.Appraisals.AppraisalNumber`
  (Step 1); enrichment is skipped, which is expected for unmatched rows.

## Record format reference (fixed-width: Detail 649 / Header-Trailer 640 chars)
Positions are **Unicode code-points, not bytes** — the parser indexes by char, so Thai text counts as
1 per character. Alpha fields are space-padded left-aligned; numeric fields are space-padded right-aligned.
- **Header (640 chars):** pos 1 = `H`, pos 2–9 = EffectiveDate (`DDMMYYYY`), pos 10–640 = filler.
- **Trailer (640 chars):** pos 1 = `T`, pos 2–10 = detail row count (9 chars), pos 11–640 = filler.
- **Detail (649 chars):** 1-based positions →
  `1` RecordType `D` · `2` ReviewType · `3–10` ReviewDate(DDMMYYYY) · `11–29` CollateralId · `30–39` SurveyNo ·
  `40–42` CollateralCode · `43–47` CollateralCategory · `48–87` CollateralName · `88–207` CollateralAddress ·
  `208–226` CifNo · `227–246` CifName · `247–256` AoCode · `257–276` AoName · `277–296` TitleNo ·
  `297–311` CurrentValue · `312–319` ValuationDate(DDMMYYYY) · `320` InternalExternal · `321` BusinessSize ·
  `322–341` BusinessSizeDesc · `342–356` MortgageAmount · `357–361` PastDueDay · `362–380` ApplicationNo ·
  `381–383` FacilityCode · `384–402` FacilitySequence · `403–418` CpNumber · `419–421` CarCode ·
  `422–436` FacilityLimit · `437` FlagLessAge4Y · `438` FlagGreaterAge4Y · `439–448` CountAgeingDate ·
  `449–498` CollateralDescription · `499–538` ExternalValuerName · `539–578` InternalValuerName ·
  `579` SllOver100M · `580–629` SllDescription · `630` Stage · `631–640` IBGRetail · `641` Group ·
  `642–649` EffectiveDateAppraisal(DDMMYYYY).

> These are the **vendor** field names. Our DB columns use the `*Number` convention (`SurveyNumber`,
> `CifNumber`, `TitleNumber`, `ApplicationNumber`) — see the SQL above.

### Sample rows in `AS400_COLLATREV_20260501.txt`
- **Row 1** — Review Type 1 (Normal), ASCII, **future** ReviewDate (positive Remaining Days);
  `SurveyNo` `68A000001` by default — override with a real appraisal number (Step 1) for geo enrichment.
- **Row 2** — Review Type 2 (Before Stage 3), **Thai** name/address, **past** ReviewDate (overdue),
  `SurveyNo` with no in-system match (lat/lon stays NULL).
- **Row 3** — Review Type 3 (Stage 3), most optional fields blank (null handling), `SllOver100M = Y`.

### Dates
- **Filename** date = `YYYYMMDD`; **in-file** dates (EffectiveDate, ReviewDate, ValuationDate) = `DDMMYYYY`.
- Review due date = the file's `ReviewDate` (AS400-provided — it follows the review type's cycle, so
  Stage 3 falls due sooner; CAS does not recompute it). "Remaining Days" = ReviewDate − today, in the
  list, the detail page, nearby books and RCAS002. "Appraisal Date" is the last appraisal (CAS, else the
  bank's listing for a 99A book, else the file's ValuationDate). `SurveyNo` = our **Appraisal Number** (FSD "Old Appraisal Report No").

## Block-project units

A COLLATREV number that names a **block-project appraisal** — with AS400's `B`, without it, or a unit
ticket CAS issued (8 characters, `U` at position 3) — reviews **one unit** of the project. Each collateral
listing it is a different unit, so these rows (`ReappraisalCandidate.IsBlockUnit`) are reviewed **per
AS400 collateral**, not per book: waiting/in-progress checks, Initiate's dedupe, consumption on submit,
reopening after a cancellation, the processed tab and RCAS002 all match the collateral as well. A ticket
is stored under its project's appraisal number (the prior appraisal stays the project); the ticket stays
in `SurveyNumber` to find the unit.

- **Which unit (CAS):** `collateral.vw_ReappraisalCandidateUnits` — the ticket's units outright, else the
  room/house number read out of the collateral name (`CONDO.<key>`) or the address's leading word, matched
  against `appraisal.vw_ProjectUnitKeys` ranks 0–2 (same reading as the regulatory export). Exactly one
  unit = matched; none or several = project-level only.
- **The request** is filled from CAS only — never the project's request, never AS400 values: collateral
  type 08 (condo) / 02 (land and building) / 01 (land), project name and address, the unit's tower, floor,
  room, registration number or house number and areas; prior appraisal = the project, prior value/date =
  the unit's appraised price and the project's valuation date (kept through saves while the prior appraisal
  is unchanged). Owner, deed and documents are left to staff.
- **AS400's collateral id** is kept on the request as `Request.ReappraisalCollateralId` (a key on the AS400
  side only; not `ExternalCaseKey`, which drives the LOS webhooks). A reappraisal raised by hand from the
  request page points at the project and cannot say which unit it reviewed: it consumes no unit.
