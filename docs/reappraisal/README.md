# Periodical Reappraisal (AS400) — How to Test (Step by Step)

End-to-end guide to test the inbound reappraisal flow locally **without an SFTP server**:

```
COLLATREV file (fixed-width 660 chars)  →  ingestion job  →  request.ReappraisalCandidates (staging)
   →  list / filter  →  open candidate + nearby group  →  initiate (creates Requests + group number)  →  delete
```

| File in this folder | Purpose |
|------|---------|
| `generate_collatrev.py` | Generates a valid fixed-width 660-char COLLATREV file (H/D/T records, UTF-8). |
| `AS400_COLLATREV_20260501.txt` | Ready-made sample: 3 detail rows (described below). |

---

## Prerequisites
- .NET 9 SDK, Docker, Python 3 (for the generator).
- Infra running: `docker compose up -d` (SQL Server on `localhost,1433`, sa / `P@ssw0rd`).
- A SQL client (Azure Data Studio / SSMS / `sqlcmd`) and an HTTP client (`curl` / Postman / the `.http` files).

---

## Step 0 — Apply database changes (one-time)

**0a. Tables** — the Request module auto-applies its EF migrations on API startup
(`app.UseMigration<RequestDbContext>()`), so just running the API (Step 3) creates
`request.ReappraisalCandidates` and the `Requests.AppraisalGroupNumber` column. To apply manually instead:
```bash
dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api
```
> Two migrations create the feature: `20260528062402_AddReappraisalCandidateAndGroupNumber` (table +
> `Requests.AppraisalGroupNumber` + unique index) and `20260528062425_AddReappraisalCandidateGeoPoint`
> (persisted `GeoPoint` computed column + spatial index, raw SQL). Both are unapplied; startup will run them.

**0b. SQL view** — `vw_ReappraisalCandidates` is deployed by the Database tool (DbUp), not EF:
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
`reappraisal-ingestion` → **Trigger now** (no need to wait for the monthly cron).
Watch the console for `[REAPPRAISAL-INGEST]` lines.

## Step 5 — Verify staging
```sql
SELECT Status, SourceFileDate, CollateralId, SurveyNumber, ReviewType, ReviewDate, Latitude, Longitude
FROM request.ReappraisalCandidates ORDER BY ReviewType;
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
`appraisalDate` (= ReviewDate), `remainingDay` (row 2 negative = overdue), `channel = "AS400"`.

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
Response: `{ "groupNumber": "68G000001", "createdRequestIds": ["…","…"] }`. Verify:
```sql
-- one Request per candidate, all sharing the group number, Channel = AS400
SELECT Id, RequestNumber, Channel, AppraisalGroupNumber, Status
FROM request.Requests WHERE AppraisalGroupNumber IS NOT NULL;

-- selected candidates are now Consumed
SELECT Id, Status FROM request.ReappraisalCandidates WHERE Id IN ('<ID1>','<ID2>');
```
Re-trigger Step 4 → Consumed rows are **not** resurrected.

## Step 9 — Delete (soft)
```bash
curl -k -X DELETE "https://localhost:7111/reappraisal/candidates/<ID3>"   # → 204
```
The row becomes `Status = Deleted` and drops out of the list (Step 6).

## Step 10 — Negative / robustness tests
Edit a copy of the sample, drop it in the inbox, trigger, and confirm one bad file doesn't block others:
- Trailer count ≠ number of `D` rows, or non-numeric → file fails, error logged, **left in inbox** (not archived).
- Remove the `T` line entirely → `FormatException` (completeness check).
- Truncate a Detail line below 660 chars → `FormatException`.

## Step 11 — Frontend
Log in as **Admin / IntAdmin / RequestMaker** → **Standalone → "Reappraisal (AS400)"** →
list → open a candidate → tick rows → **Initiate** → success popup shows the group number;
**View on Map** opens History Search centered on the candidate; the row's **Delete** removes it.
Confirm the created requests appear in the normal Request listing with the group number.

---

## Reset between runs
```sql
DELETE FROM request.ReappraisalCandidates;
-- optionally remove the test requests created by initiate:
-- DELETE FROM request.Requests WHERE Channel = 'AS400' AND AppraisalGroupNumber IS NOT NULL;
```
```bash
# move the archived file back to re-ingest it
mv Bootstrapper/Api/reappraisal/processed/AS400_COLLATREV_20260501.txt Bootstrapper/Api/reappraisal/inbox/
```

## Troubleshooting
- **No rows after trigger** — check the inbox path (`Bootstrapper/Api/reappraisal/inbox`), the filename
  matches `AS400_COLLATREV_YYYYMMDD.txt`, and the console `[REAPPRAISAL-INGEST]` logs for parse errors.
- **List endpoint 500 / "invalid object name"** — the view isn't deployed; run Step 0b.
- **Menu item not visible** — the logged-in role lacks `REAPPRAISAL_VIEW` (use Admin/IntAdmin/RequestMaker)
  or the seed didn't run; restart the API.
- **All Latitude/Longitude NULL** — row 1's `SurveyNumber` didn't match any `appraisal.Appraisals.AppraisalNumber`
  (Step 1); enrichment is skipped, which is expected for unmatched rows.

## Record format reference (fixed-width 660 chars)
Each record is exactly **660 characters** (Unicode code-points, not bytes — the parser indexes by char,
so Thai text counts as 1 per character). Alpha fields are space-padded left-aligned; numeric fields are
space-padded right-aligned.
- **Header (660 chars):** pos 1 = `H`, pos 2–9 = EffectiveDate (`DDMMYYYY`), pos 10–660 = filler.
- **Trailer (660 chars):** pos 1 = `T`, pos 2–10 = detail row count (9 chars), pos 11–660 = filler.
- **Detail (660 chars):** 1-based positions →
  `1` RecordType `D` · `2` ReviewType · `3–10` ReviewDate(DDMMYYYY) · `11–29` CollateralId · `30–39` SurveyNo ·
  `40–42` CollateralCode · `43–45` CollateralCategory · `46–75` CollateralName · `76–175` CollateralAddress ·
  `176–194` CifNo · `195–214` CifName · `215–224` AoCode · `225–264` AoName · `265–284` TitleNo ·
  `285–300` CurrentValue · `301–308` ValuationDate(DDMMYYYY) · `309` InternalExternal · `310` BusinessSize ·
  `311–350` BusinessSizeDesc · `351–366` MortgageAmount · `367–371` PastDueDay · `372–390` ApplicationNo ·
  `391–393` FacilityCode · `394–412` FacilitySequence · `413–428` CpNumber · `429–431` CarCode ·
  `432–447` FacilityLimit · `448` FlagLessAge4Y · `449` FlagGreaterAge4Y · `450–459` CountAgeingDate ·
  `460–509` CollateralDescription · `510–549` ExternalValuerName · `550–589` InternalValuerName ·
  `590` SllOver100M · `591–640` SllDescription · `641` Stage · `642–651` IBGRetail · `652` Group ·
  `653–660` EffectiveDateAppraisal(DDMMYYYY).

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
- List "Appraisal Date" = the file's `ReviewDate` (AS400-provided; CAS does not compute it).
  "Remaining Days" = ReviewDate − today. `SurveyNo` = our **Appraisal Number** (FSD "Old Appraisal Report No").
