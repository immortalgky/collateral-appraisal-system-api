# FSD Template Fidelity Audit — Reporting Module

**Date:** 2026-06-14
**Scope:** Appraisal Summary (5 variants), Appraisal Book (external + internal), Meeting Invitation, Meeting Minute, Appointment/Quotation request.
**Method:** Structural fidelity audit (read-only). Every FSD field / section / table-column was mapped to the template HTML/CSS and the feeding data provider, then flagged **Aligned / Missing / Extra / Mis-ordered / Layout-deviation / Deferred(no data source)**. No rendering was performed — items provable only by rendering are marked **render-only**.

**FSD sources (binary `.docx` in `.claude/docs/`):**
- `LHB FSD Appraisal Template v1.0.docx` — **Chapter 10** (20 Oct 2025). Authoritative for all reports below; contains 43 embedded mockup PNGs (`word/media/image1–43.png`).
- `LHB FSD Common Administration v1.3.docx` / `LHB FSD Appraisal Initiation v1.0.docx` — checked for Appointment/Quotation; not the governing spec (see §7).
- Extracted text + figures (for re-verification): `/tmp/fsd-audit/{ch10_template,common_admin,initiation}/`.

**Implementation root:** `Modules/Reporting/Reporting/Templates/` (Scriban HTML → PuppeteerSharp A4 PDF → PdfSharp merge). Data model `Application/Models/AppraisalSummaryModel.cs`; providers under `Application/Providers/`.

---

## 0. Executive summary

| Report | FSD § (fig) | Verdict | Headline gaps |
|---|---|---|---|
| Summary — Land & Building | 2.1.3.1 (img30) | **Good** | Condition/Remark always render; method checkboxes collapse WQS+SG; committee "property details" reuses header address |
| Summary — Condo | 2.1.3.2 (img31) | **Gaps** | **Missing 4 attribute fields** (Land Condition, City Plan, Gov-assessed value, Utilization) |
| Summary — Machine | 2.1.3.3 (img32) | **Gaps** | **Missing fields 8–10** (Administrative District, Land Office, Old appraisal value) |
| Summary — Construction | 2.1.4 (img33) | **Gaps** | **Missing #20 current update count, #21 installment no.** |
| Summary — Block | 2.1.5 (img34–36) | **Gaps** | **Missing condo "No. of Room" col**; committee block must show "ตามแนบ" + project address, not numeric/header address |
| Book — External cover+letter | 2.1.2.1/.2 (img5–7) | **Gaps** | Signature names null; City Planning Act no source; House No/Moo dropped on land; machine "สำรวจพบ" clause; certification sentence missing |
| Book — External detail sections | 2.1.2.3–.12 (img8–29) | **Major gaps** | **Condo price table absent; machine per-unit detail under-built; SaleGrid subject column empty; CostMachine missing P/N/n/R cols; WQS price-adjust rows absent; building floor cluster absent** |
| Book — Internal (ext+block) | 2.1.6/2.1.7 (img33/34/37) | **Good** | Block book renders extra land/building/construction sections FSD doesn't list |
| Meeting Invitation | 2.1.8 (img38/39) | **Gaps** | **Footer signature roster missing; block agenda shows customer not project name; multi-customer "และ" join missing** |
| Meeting Minute | 2.1.9 (img40–43) | **Gaps** | **Construction progress % missing; block project-name + fixed wording; presenter table stubbed; sig-block header missing** |
| Appointment / Quotation | 2.1.1 (img3/4) | **Major gaps** | **Document-Check back page (field 35) absent; referrer block + registration address never populated (~40% fields blank)** |

**Cross-cutting root causes**
1. **Deferred data sources** — several FSD fields have no DB column, so the template renders blank: `AoName`, `AppraisalCheckerName/Position`, `AppraisalVerifyName/Position`, external `CheckerName/VerifyName/DirectorName`, `CityPlan` (external letter), machine `Country`/`CollateralDetailNarrative`, costmachine `RemainingR`, request referrer block + Address2. These are **data-model gaps, not template bugs**.
2. **Shared `approver-block.html` is Land&Building-shaped** — it hard-binds the committee row to `collateral_address` + numeric value, so Block (FSD wants project address + "ตามแนบ") and condo/machine address formats are wrong in the committee block.
3. **`MeetingAgendaBuilder.ToRow`** emits only customer-name + numeric value — no block (project-name / "ดูตามสรุปราคา") branch and no construction-progress field; and both meeting providers take `TOP 1` customer (drop the "และ" multi-customer join).
4. **Detail-section under-build** — several external book tables omit FSD columns/sub-tables outright (condo price table, machine spec card, costmachine depreciation columns, WQS price-adjust rows, building floor cluster).

> All "pixel"-exact claims (font load, exact spacing, A4 enforcement) are **render-only** and were not visually verified. Recommend a follow-up render pass on Summary-LB, Book-External, and the Appointment form to confirm layout conclusions.

---

## 1. Appraisal Summary — Land & Building / Condo / Machine (FSD §2.1.3)

Templates: `appraisal-summary-land-building.html` → `partials/summary-standard-body.html`; `appraisal-summary-condo.html`; `appraisal-summary-machine.html`; shared `partials/summary-head.html`, `summary-signoff.html`, `summary-styles.html`, `approver-block.html`.

### Land & Building (img30) — **Aligned overall**
- Fields 1–51 essentially all mapped. Deferred (blank, data-source): #4 AO Name, #38/39 Checker, #40/41 Verify, #25 Entry/exit rights.
- **Layout-deviation #18/19 Condition/Remark** (`summary-standard-body.html:49-50`): always rendered; FSD = "if blank, do not display". Wrap in `{{ if model.condition }}` / `{{ if model.remark }}`.
- **Layout-deviation #33 method checkboxes** (`summary-standard-body.html:88-96`): 4 boxes, WQS+SaleGrid collapsed into "Market Comparison"; model flags `is_leasehold`/`is_profit_rent` never rendered → method actually used can go unticked.
- **#44 committee "property details"** (`approver-block.html:12`) reuses `collateral_address` (field 7) rather than a committee-specific property line — confirm with product whether identical.

### Condo (img31) — **Blocking gaps**
- **Missing #27 Land Condition, #29 City Plan, #30 Government Assessed Value, #31 Utilization.** The condo attributes block (`appraisal-summary-condo.html:96-104`) renders only owner/entry-exit/obligation/GPS; all four fields exist on the model and in img31. Add rows in **condo field order** (Obligation 26 → Land Condition 27 → GPS 28 → City Plan 29).
- Condition/Remark always-render (same as LB). Correct coverage-amount wording `ทุนประกันภัยห้องชุด` ✅.

### Machine (img32) — **Blocking gaps**
- **Missing #8 Administrative District, #9 Land Office, #10 Old appraisal value.** Machine info block (`appraisal-summary-machine.html:24-37`) drops the cols-3 group present in LB `summary-standard-body.html:11-15`. They are in the FSD machine field table and img32.
- Correct: blanked Area/Unit + Price (FSD rule), machine subtotal `รวมมูลค่าเครื่องจักร` (#18), wording `ทุนประกันภัยเครื่องจักร`.

---

## 2. Appraisal Summary — Construction & Block (FSD §2.1.4 / §2.1.5)

Templates: `appraisal-summary-construction.html` + `partials/summary-construction-body.html`; `appraisal-summary-block.html` + `partials/summary-block-body.html`.

### Construction (img33)
- **Blocking #20 "Current No. of update construction":** only one `ตรวจครั้งที่` field rendered (mapped to prior #12, `summary-construction-body.html:32`); FSD wants both prior + current.
- **Blocking #21 "No. of installment work" (งวดงานที่):** `InstallmentNumber` exists on model (`AppraisalSummaryModel.cs:151`) but is never emitted.
- Construction-progress %-columns + value-totals table otherwise Aligned. Deferred: #4 AO, #12 inspection round, #22/#24 license/photo checkboxes, #31–34 checker/verify.

### Block (img34–36)
- **Blocking #56 "No. of Room" (จำนวนห้อง)** missing from condo unit table (`summary-block-body.html:134-146`); no `BlockCondoUnitRow` property (`AppraisalSummaryModel.cs:448`).
- **Blocking #33** Block committee value must be literal **"ตามแนบ"**, but `approver-block.html:14-16` renders numeric total + baht-text.
- **Blocking #32** committee "property details" should be **Project Address**; `approver-block.html:12` binds `collateral_address`.
- Building unit table (10 cols, img35) fully Aligned. Method checkboxes same WQS/SG collapse as LB.
- *(Block #32/#33 stem from reusing the LB-shaped `approver-block`; needs a parameterized or block-specific committee block.)*

---

## 3. Appraisal Book — External Cover & Letter (FSD §2.1.2.1 / §2.1.2.2)

Templates: `appraisal-book.html`, `partials/book-cover-external.html`, `book-letter-external.html`, `book-cover-styles.html`. Builder: `Application/Providers/ExternalBookBuilder.cs`.

- **Assembly order Aligned** — `appraisal-book.html:16-40` includes cover → letter → condo → land → building → construction → machine → comparison → wqs/salegrid/costmachine → appendix, matching FSD "Contains:" (lines 314–324). (Dead `<!-- SLOT: appendix -->` at `:40` — inert, remove.)

**Blocking**
1. **Signature names #34/#36/#38 all null** (`ExternalBookBuilder.cs:442-445`) — External Checker / Verify / Authorized Director names + verify-license-no deferred; every external book ships 3 blank signature lines.
2. **City Planning Act #23 no data source** — `book-letter-external.html:114` row exists but builder never sets `CityPlan`; row permanently hidden.
3. **Collateral Location drops House No + Moo on land** (`ExternalBookBuilder.cs:309,311` pass `null`) — FSD #5/#7 format incomplete for Land/Land+Building.
4. **Machine property-type missing "สำรวจพบ {found} เครื่อง"** (`ExternalBookBuilder.cs:384`) — FSD #4/#6 machine format unmet.
5. **Certification sentence absent** — img7's attestation ("…ขอรับรองว่าไม่มีผลประโยชน์…") not in `book-letter-external.html` closing block (`:174-179`).

**Cosmetic / render-only:** logo placeholders are literal text "LOGO" (cover `:7`, letter `:11`); Forced-Sale-Value label hard-codes "70%" (`:154`); positions #35/#37/#39 hard-coded; Rai-Ngan-Wa (#13) + Total Sq.Wa (#14) merged into one line; no `@page size:A4` in CSS (A4 is the renderer default).

---

## 4. Appraisal Book — External detail sections (FSD §2.1.2.3 – §2.1.2.12)

Templates: `partials/ext-section-*.html`; loaders `Application/Providers/Sections/`.

| Section | FSD § (fig) | Verdict |
|---|---|---|
| Condo | 2.1.2.3 (img8/9) | **Blocking: entire per-area price table (#42–58) + Total + Appraisal Date absent** (`ext-section-condo.html` has no table after :35) |
| Land | 2.1.2.4 (img10) | Good; missing #12 Rawang, #13 Check by; Total Rai/Ngan/Wa merged |
| Building | 2.1.2.5 (img11) | **Blocking: floor cluster #22–26 absent**; also #6 Check by, #18 Roof surface, cost-table #40 Depreciation-price column missing |
| Construction Progress | 2.1.2.6 (img12) | **Fully Aligned** (7 cols + per-part + per-building totals) |
| Machine | 2.1.2.7 (img13/14/15) | **Blocking: narrative cover #1–3 absent; per-machine detail wrong shape — missing #28,33–37,39,40,42** |
| Comparison | 2.1.2.8 (img16) | Aligned (dynamic transposed matrix; fidelity = loader data) |
| WQS | 2.1.2.9 (img17/18) | **Blocking: price-adjustment rows #11–18 + ผลรวม/avg/legend absent**; stat block + scatter + subject col OK |
| Sale Grid | 2.1.2.10 (img18) | **Blocking: Subject (SP) column renders empty; factor rows are ordinals not named factors** |
| Cost-Machine | 2.1.2.11 (img19/20) | **Blocking: missing P, N/n/R, Quantity, Country, Market-demand columns; formula text drops P term** |
| Appendix | 2.1.2.12 (img21–29) | Aligned (config-driven N-up grid; fidelity = loader grouping) |

Deferred (loader stubs, no DB column): machine `CollateralDetailNarrative`, machine/costmachine `Country`, costmachine `RemainingR`, salegrid factor names.

---

## 5. Appraisal Book — Internal (FSD §2.1.6 Construction / §2.1.7 Block)

Templates: `appraisal-book.html` (internal branch), `partials/book-cover-internal.html`, `summary-construction-body.html`, `summary-block-body.html`, reused `ext-section-*`, `approver-block.html`, `summary-signoff.html`. Provider: `AppraisalBookDataProvider.cs`.

- **No blocking gaps.** Both internal books assemble FSD-required sections in FSD order; internal cover fields all map; sign-off (3-role staff) + committee block match img33/img34/img30.
- **Moderate (B1):** for **Block** books the template loads land/building/construction/machine sections unconditionally (`AppraisalBookDataProvider.cs:97-101`); FSD §2.1.7 "Contains" lists only Summary-Block → Comparison → Price Analysis → Appendix. Decide whether to suppress these for `body_type=="block"`.
- **Cosmetic:** internal cover logo is literal `LOGO` (`book-cover-internal.html:7`); empty-approver fallback prints blank name cell (`approver-block.html:42-51`).
- **Audit note:** img37 is the **Appendix** figure (zoning map), not the sign-off; the internal sign-off lives inside img33/img34, audited correct.

---

## 6. Meeting Invitation (§2.1.8) & Meeting Minute (§2.1.9)

Templates: `meeting-invitation.html`, `meeting-minute.html`. Providers `MeetingInvitationDataProvider.cs`, `MeetingMinuteDataProvider.cs`, `MeetingAgendaBuilder.cs`.

### Invitation (img38/39) — **Blocking**
1. **#25 Project name for agenda 6 (block)** — renders customer name; `MeetingAgendaBuilder.ToRow:151-165` has no project-name path.
2. **#38–40 footer signature roster** (meeting-no/date header + รายนาม/ลายเซ็น two-column committee table) entirely absent; the committee list (`meeting-invitation.html:268-279`) is name+position only and mis-positioned (above the secretary).
3. **#14/18/28/32 multi-customer "และ" join** — provider takes `TOP 1 Name` (`MeetingInvitationDataProvider.cs:80-84`).
- Cosmetic: closing sentence "จึงเรียนมาเพื่อโปรดเข้าร่วมประชุม…" missing; secretary block uses underline not FSD dash-line + "( name )".

### Minute (img40–43) — **Blocking**
1. **#21 Construction Progress %** (agenda 5) absent — no field in model/builder/template.
2. **#24 Project name for agenda 6** — shows customer name (same `ToRow` root cause).
3. **#25 fixed block value wording** "ราคาประเมิน รายละเอียดดูตาม สรุปราคา" not implemented (renders numeric).
4. **#4.1–4.3 Presenter table** stubbed placeholder (`meeting-minute.html:197-201`); no schema source.
5. **#36/37 signature-block meeting-no/date header** missing.
- Cosmetic: agenda items are a borderless flex list, not the FSD bordered "รายการ | อนุมัติราคาประเมิน (ตามเอกสารแนบ)" table; signature block is a 2-col opinion grid, not FSD 3-col table; default "ไม่มีความเห็นเพิ่มเติม" not auto-filled.

---

## 7. Appointment / Quotation Request (FSD §2.1.1 Survey Appointment Form, img3/img4)

Template: `appointment-quotation-request.html`. Provider: `AppointmentQuotationDataProvider.cs`.
**Mapping confirmed:** this is **Ch.10 §2.1.1** (title `ใบขอนัดสำรวจและประเมินราคา` byte-matches `:6`/`:205`), **not** the Common-Admin Quotation (those are on-screen list/maker-checker screens, not a PDF form). The bilingual name is cosmetic.

> **Correction (2026-06-16, after rendering img3/img4):** Two earlier claims here were wrong.
> (a) The FSD front page has **no property table** — fields 13/14 are free-text `รายละเอียดทรัพย์สิน`
> + `ประเภทหลักประกัน`; the old template's 10-column table was a fabrication (now removed).
> (b) Fields **22–28 are `ที่ตั้งทรัพย์สิน (ตามเขตปกครอง)`** (property location per administrative
> jurisdiction — the second of two *property* addresses), **not** a mailing/registration address.
> The template was rewritten to the FSD's single-bordered-box continuous Thai layout, and the
> img4 document-checklist back page was built. Remaining blank-by-design fields: the requestor org
> block (1–4), the referrer (5–6), the admin-jurisdiction address (22–28, only one address in the
> Request schema), and the checker (33–34, no checker role on requests).

**Blocking**
1. **Field 35 Document-Check tick-list (the back page, img4) entirely missing** — mandatory purpose×collateral-type checklist; the `<!-- SLOT: attachments -->` (`:513`) is unrelated (uploaded PDFs). Largest gap; also needs a 2nd page (`min-height:270mm` is single-page).
2. **Registration address #22–28 never populated** — no `address2_*` in model/provider; block mislabeled "Mailing Address" (`:395-426`).
3. **Referrer block #1–6 all blank** — Division/Department/Line of Work/Cost Center/Referrer/Tel unmapped (`Provider.cs:65-66,155-159`).
4. **Checker name + date #33–34 always blank** ("no checker concept on requests today", `Provider.cs:40`).
5. **Title-deed per-property address columns (#15–21) always blank** — `PropertyRowFlat` address fields never set (`Provider.cs:129-141`); address duplicated into Section 5.

**Cosmetic:** 8 boxed sections vs FSD continuous flow; date format "DD Month YYYY" vs FSD `DD/MM/YYYY` (BE); empty Building Type should be `"-"`; phone format `xxx-xxxxxxx` not enforced; FSD legal disclaimer replaced by generic boilerplate (`:500-503`).

---

## 8. Recommended remediation order

**Tier 1 — pure template fixes (no schema change), high fidelity gain**
- Condo summary: add the 4 missing attribute rows (§1).
- Machine summary: add District / Land Office / Old-value group (§1).
- Construction summary: add current-update-count + installment-no fields (§2).
- Condition/Remark conditional rendering across all summaries (§1).
- Book external detail sections: add condo price table, building floor cluster, WQS price-adjust rows, CostMachine P/N/n/R + Quantity columns, machine per-unit spec card (§4).
- Parameterize `approver-block` for Block ("ตามแนบ" + project address) and fix Block condo "No. of Room" column (§2).
- Meeting: add footer signature roster (invitation), construction-progress % + presenter table (minute), block project-name + fixed wording branch in `MeetingAgendaBuilder.ToRow` (§6).
- Appointment: build the Document-Check back page (§7).

**Tier 2 — needs a data source (model/provider/SQL)**
- Multi-customer "และ" join (meeting providers + appointment view).
- Deferred sign-off identities: AO Name, Checker/Verify (internal summary), external Checker/Verify/Director names.
- External letter City Planning Act; House No/Moo on land; machine "สำรวจพบ"; certification sentence.
- Appointment referrer block + registration address (Address2) + checker.
- Machine/CostMachine `Country`, `RemainingR`, salegrid factor names, machine narrative cover.

**Tier 3 — render-only confirmation pass**
- Confirm Sarabun .ttf deploys, A4 enforcement, exact spacing on Summary-LB, Book-External, Appointment via an actual PDF render.

---

*Generated by structural audit; per-field matrices for each report are available in the audit working notes. Re-verify any single finding by opening the cited FSD figure (`/tmp/fsd-audit/ch10_template/imageNN.png`) beside the cited template `file:line`.*
