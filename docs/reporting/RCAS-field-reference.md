# RCAS Operational Reports — FSD Field Reference

**Purpose.** One place to see, for every RCAS report (RCAS001–RCAS012), what the FSD requires and
what the system produces today — field by field, filter by filter. Written for business and IT
readers alike. This is the reviewable source of truth; any future report change is checked against it.

**FSD source.** `.claude/docs/LHB FSD Appraisal Report v1.0.docx` ("Chapter 9: Appraisal Report",
v1.0, 06 Nov 2025). Field tables and selection criteria were extracted table-aware via
`docs/reporting/tools/extract-fsd.py` so nothing is paraphrased from memory.

**Implementation.** `Modules/Reporting/Reporting/Application/OperationalReports/*` (column lists,
filters, sort) + `Database/Scripts/Views/Reporting/vw_RCAS*.sql` (data). Verified 2026-07-18 against
the working tree of branch `feat/rcas003-fsd-fields-and-signoff-footer`.

> Supersedes `docs/reporting/RCAS-fsd-column-verification-2026-06-22.md`, which checked columns only
> and was wrong in places (it called RCAS001/002 "aligned"; they are not, and it never checked
> filters).

**Status legend:** ✅ present & correct · ⚠️ present but wrong (order/format/label/blank) ·
❌ missing · ➕ extra (in impl, not in FSD).

---

## Summary — where each report stands

| Report | Title (TH) | Columns | Filters | Sort | Overall |
|---|---|---|---|---|---|
| RCAS001 | เล่มประเมินตามช่วงเวลา/สถานะ/ฝ่ายงาน | ⚠️ missing Running No. | ❌ missing Requestor Dept; ➕2 | ✅ | Materially off |
| RCAS002 | ครบกำหนดทบทวนหลักประกันตามประเภท | ✅ 18/18 | ❌ missing Create Date range | ⚠️ composite not supported | Materially off |
| RCAS003 | สรุปปริมาณงานประจำเดือน | ✅ full match | ❌ missing 4; ➕channel | ✅ | Columns ✅ / filters off |
| RCAS004 | ตรวจงวดงานที่ยังไม่ครบ 100% | ✅ 13/13 | ➕1 | ✅ | Minor gaps |
| RCAS005 | สรุปตาม External Company | ✅ full match | ✅ (➕2) | ⚠️ wrong default | Sort off |
| RCAS006 | สรุปตาม Internal Staff | ⚠️ 2 extra cols | ✅ (➕3) | ⚠️ wrong default | Cols + sort off |
| RCAS007 | สรุป SLA Internal & External | ✅ built (own report) | ✅ | n/a | **Built 2026-07-18** |
| RCAS008 | คุณภาพบริการ External Company | ❌ missing Internal Staff + per-criterion remarks; ⚠️ Chanel | ❌ missing 2 | ⚠️ composite | Materially off |
| RCAS009 | สรุปค่าประเมิน | ⚠️ order (Status, Cost Center); ⚠️ Cost Center blank; ➕Fee Status | ✅ | n/a | Minor gaps |
| RCAS010 | ค่าใช้จ่ายที่ธนาคารจ่าย ประจำเดือน | RM name **added** (grain Channel+RM+AssignType); flat kept (business OK'd) | **5 filters added** | n/a | **Reworked 2026-07-18** |
| RCAS011 | รายละเอียดตาม RM ผู้ดูแลลูกค้า | ✅ full match | ❌ missing 3 (Purpose, AO, Dept) | ✅ | Columns ✅ / filters off |
| RCAS012 | สรุปการติดตามงานบริษัทประเมิน | ✅ built (shares SlaReport) | ✅ SLA>2d added | n/a | **Built 2026-07-18** |

**Deployment note.** RCAS003's `Role` and `Internal Staff` columns depend on two SQL views changed
on this branch (`vw_AppraisalList`, `vw_RCAS_OlaBase`) that must be deployed via DbUp. Until then
those two columns render blank — a deployment step, not a code defect.

---

## RCAS001 — Appraisal books by period / status / department

**Impl:** `Rcas001/Rcas001Report.cs`, `Rcas001Row.cs`, `Rcas001Endpoint.cs`; view
`vw_RCAS001_AppraisalBooks.sql`. FSD Tables 9–10. Format: Excel. Sort (FSD): Appraisal Report Number.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Create Date from…to (default today) | `CreatedFrom/To` | ✅ |
| Appraisal Status (default All) | `Status` | ✅ |
| Requestor Department (default All) | — | ❌ **missing** (view exposes `RequestorDepartment`, just unwired) |
| — | `BankingSegment` | ➕ extra |
| — | `AppraisalNumber` | ➕ extra |

### Fields (FSD order)
| # | Field (EN) | Field (TH) | Rule | Status |
|---|---|---|---|---|
| 1 | Running No. | Running No. | Running Record | ❌ **missing** |
| 2 | Appraisal Create Date | วันสร้างเล่มประเมิน | Display date & time | ✅ |
| 3 | Appraisal Report No | หมายเลขเล่มประเมิน | | ✅ |
| 4 | Customer Name | ชื่อลูกค้า | | ✅ |
| 5 | Appraisal Purpose | วัตถุประสงค์การประเมิน | | ✅ |
| 6 | Apply/Limit Amount | วงเงิน | | ✅ (➕ has a total row not in FSD — harmless) |
| 7 | Collateral Type | ประเภทหลักประกัน | | ✅ |
| 8 | Approach Method | วิธีประเมินราคา | | ✅ |
| 9 | Appraisal Price | ราคาประเมิน | | ✅ |
| 10 | Appraisal Report Status | สถานะเล่มประเมิน | | ✅ |
| 11 | Requestor Code/Name | ผู้ขอประเมิน | | ⚠️ code only, no name |
| 12 | Requestor Department | หน่วยงานผู้ขอประเมิน | | ✅ |
| 13 | Retail / IBG | RB / IBG | RBG / IBG | ✅ |
| 14 | Internal Appraisal Staff Code/Name | ผู้ตรวจเล่ม | | ✅ |
| 15 | Appraisal Company Code/Name | บริษัทประเมิน | | ✅ |
| 16 | Approve Date | วันที่อนุมัติ | Display date & time | ✅ |

**Gaps:** missing `Requestor Department` filter (the report's namesake "ตามฝ่ายงาน") and `Running No.`
field; 2 extra filters.

---

## RCAS002 — Collateral review-due by type (AS400-sourced)

**Impl:** `Rcas002/*`; view `vw_RCAS002_ReappraisalDue.sql` (from `collateral.ReappraisalCandidates`).
FSD Tables 11–12. Format: Excel. Sort (FSD): **Review Type, Remaining Day** (composite).

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Report No (default All) | `AppraisalNumber` | ✅ |
| Customer Name | `CustomerName` | ✅ |
| Create Date from…to (default today) | — | ❌ **missing** (no filter, and no create-date column in the view) |
| Stage (default All) | `Stage` | ✅ |
| Review type (default All) | `ReviewType` | ✅ |

### Fields
All 18 present and in FSD order: Review Type · Stage · Appraisal Report No · Previous Appraisal
Report No (⚠️ always blank — prior cycle not tracked) · Collateral No. · CIF Number · Customer Name ·
Apply/Limit Amount · Collateral Type (⚠️ raw code, unresolved) · Title Deed Number · Retail/IBG ·
Appraisal Company · Internal Appraisal Staff · Old Appraisal Value · Past Due Day · Valuation Date ·
Calculate Next Valuation date · Remaining Days. **Columns ✅.**

**Gaps:** the entire `Create Date from…to` selection criterion is missing at both endpoint and view
level; the FSD composite sort (`Review Type` then `Remaining Day`) cannot be produced — the sort
helper emits one column only.

---

## RCAS003 — Monthly workload summary

**Impl:** `Ola/OlaReport.cs` (`fsdDetail:true`), `OlaReportRow.cs`, `OlaReportsEndpoints.cs`; view
`vw_RCAS_OlaBase.sql`. FSD Tables 13–14. Format: Excel. Sort (FSD): Appraisal Report Number.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Report Number | `appraisalNumber` | ✅ |
| Customer Number | — | ❌ **missing** |
| Purpose (default All) | — | ❌ **missing** |
| AO code (default All) | — | ❌ **missing** |
| Internal Appraisal Staff (default All) | `internalStaff` | ✅ |
| Appraisal Company (default All) | `appraisalCompany` | ✅ |
| External Appraisal Staff (default All) | — | ❌ **missing** |
| Appraisal Create Date from…to (default today) | `createdFrom/To` | ✅ |
| Appraisal Status (default All) | `status` | ✅ |
| — | `channel` | ➕ extra |

### Fields
All 20 data fields present, in FSD order: Appraisal Report Number · Customer Name · Purpose ·
Apply/Limit Amount · Collateral Type · **Appraisal Create Date** (Display Date & Time) · Channel ·
Appraisal Company · **Internal Appraisal Staff** (shows `CODE - First Last`) · **Role** · Appointment
Date · Assign Date · Receive Appraisal Report Date · OLA Appraisal · OLA Internal Staff (Verify) · OLA
Internal Checker · OLA (Staff+Checker) · OLA Internal Verify · OLA Approval · Status. **Columns ✅.**
Footer: Total item ✅, Total limit ✅, **Print Report By** ✅ (defaults to the printing user),
**Approve Report By** ✅ (blank signing line).

**Gaps:** 4 missing filters (Customer Number, Purpose, AO code, External Appraisal Staff); 1 extra
(`channel`). **Note:** the OLA (hrs) values come from `OlaTimingService`, whose activity→segment
mapping is self-flagged in code as "best-effort, SHOULD BE VALIDATED with the business."

---

## RCAS004 — Construction inspection < 100%

**Impl:** `Rcas004/*`; view `vw_RCAS004_ConstructionInspection.sql`. FSD Tables 15–16. Format: Excel.
Sort (FSD): Appraisal Report Number.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Create Date from…to (default today) | `CreatedFrom/To` | ✅ |
| Appraisal Status (default All) | `Status` | ✅ |
| (base) Progressive Inspection < 100% | `WHERE ProgressPct < 100` in view | ✅ |
| — | `AppraisalNumber` | ➕ extra |

### Fields
All 13 present in FSD order: Appraisal Report Number · Customer Name · Purpose · Apply/Limit Amount ·
Collateral Type · Channel · Appraisal Company · Internal Appraisal Staff · Appraisal Value · Previous
Appraisal Report No · Appointment Date · Status · Progressive Construction Inspection %. **Columns ✅.**

**Gaps:** one harmless extra filter. **The only aligned report other than the column-clean OLA set.**

---

## RCAS005 — Per External Appraisal Company

**Impl:** `Ola/OlaReport.cs` (`fsdDetail:true`, scope `External`). FSD Tables 17–18. Format: Excel.
Sort (FSD): **Appraisal company, Appraisal Report Number** (composite).

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| (base) assigned to External company | `AssignmentType='External'` | ✅ |
| Internal Appraisal Staff | `internalStaff` | ✅ |
| Appraisal Company (default All) | `appraisalCompany` | ✅ |
| Appraisal Create Date from…to | `createdFrom/To` | ✅ |
| Status (default All) | `status` | ✅ |
| — | `channel`, `appraisalNumber` | ➕ extra |

### Fields
Same 20-field OLA set as RCAS003, in order. **Columns ✅.** Footer ✅ (labels in FSD: ผู้พิมพ์รายงาน /
ผู้อนุมัติรายงาน).

**Gaps:** default sort is `AppraisalNumber`; FSD wants `Appraisal Company` primary + report-number
secondary — wrong default and composite not supported.

---

## RCAS006 — Per Internal Appraisal Staff

**Impl:** `Ola/OlaReport.cs` (`fsdDetail:true`, scope `Internal`). FSD Tables 19–20. Format:
**Excel, PDF**. Sort (FSD): **Internal Appraisal Staff**.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| (base) assigned to Internal staff | `AssignmentType='Internal'` | ✅ |
| Internal Appraisal Staff | `internalStaff` | ✅ |
| Appraisal Create Date from…to | `createdFrom/To` | ✅ |
| Status (default All) | `status` | ✅ |
| — | `appraisalCompany`, `channel`, `appraisalNumber` | ➕ extra |

### Fields
RCAS006's FSD table is **shorter** — it omits `Receive Appraisal Report Date` and `OLA Internal Staff
(Verify)` (internal work has no company→bank handoff). The impl reuses the full OLA column set, so it
shows **2 extra columns** (Receive Date, OLA Staff/Verify) that render blank for internal books.
Otherwise present and in order. Footer ✅.

**Gaps:** wrong default sort (should be Internal Appraisal Staff); 2 extra columns vs its FSD table.

---

## RCAS007 — SLA summary (Internal & External) — **NOT IMPLEMENTED**

**Impl:** currently **aliased** onto the OLA report (`OlaReportsEndpoints.cs:37-38`, `fsdDetail:false`).
It emits the base OLA columns under RCAS007's title/filename — it is **not** the RCAS007 report. FSD
Tables 21–22. Format: Excel, PDF. Sort (FSD): none.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal report No. | `appraisalNumber` | ✅ |
| Appraisal Purpose (default All) | — | ❌ |
| Customer Name | — | ❌ |
| Internal Appraisal Staff | `internalStaff` | ✅ |
| Appraisal Company (default All) | `appraisalCompany` | ✅ |
| Status (default All) | `status` | ✅ |
| Appraisal Create Date from…to | `createdFrom/To` | ✅ |
| OLA (default All) | — | ❌ |
| — | `channel` | ➕ |

### Fields (FSD Table 22 — 21 fields)
Present via alias: Appraisal Report Number, Customer Name, Appraisal Purpose, Appraisal Company,
Internal Appraisal Staff, Appointment date, Status (7). **Missing (14):** Requestor Code/Name,
Requestor phone number, Requestor Department, Retail/IBG, External Appraisal Staff, Appraisal Company
phone, Internal Appraisal Staff phone, Appraisal fee, Appraisal Number create date, **SLA** (formula
`SLA = SLA setting − (X − Appraisal date)`, 3 cases), Appraisal Value 100%, Current Role, Total
Appraisal fee, Total Appraisal Value. Shows ~11 unrelated OLA columns instead.

**Verdict:** must be built as its own report (view + SLA math). See open questions on the SLA formula.

---

## RCAS008 — External company service quality

**Impl:** `Rcas008/*`; view `vw_RCAS008_ServiceQuality.sql`. FSD Tables 23–24. Format: Excel, PDF.
Sort (FSD): **Appraisal company, Verify date** (composite).

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| status=approved, verify by month | `ApprovedFrom/To` date range | ⚠️ approximated |
| Chanel type (Retail / Wholesale) | `BankingSegment` | ⚠️ semantic mismatch (see below) |
| Purpose | — | ❌ |
| Appraisal type | — | ❌ |
| Appraisal Company | `AppraisalCompany` | ✅ |
| — | `EvaluationStatus`, `AppraisalNumber` | ➕ |

### Fields
| # | Field (EN) | Rule | Status |
|---|---|---|---|
| 1 | Appraisal Report No. | | ✅ |
| 2 | Appraisal Company | | ✅ |
| 3 | Internal Appraisal Staff | | ❌ **missing** (not in view or column list) |
| 4 | Approved Date | | ✅ |
| 5 | Chanel | Wholesale/Retail | ⚠️ labelled "Retail/IBG", sourced from `BankingSegment` |
| 6 | Total score % | | ✅ |
| 7 | Score of Report book quality | | ✅ |
| 8 | Remark | Display for score < 4 | ❌ per-criterion conditional remark not done |
| 9 | Score of Delivery time (SLA) | | ✅ |
| 10 | Remark | Display for score < 4 | ❌ |
| 11 | Score of Preparing personnel | | ✅ |
| 12 | Remark | Display for score < 4 | ❌ |
| 13 | Score of Response time | | ✅ |
| 14 | Remark | Display for score < 4 | ❌ |
| 15 | Score of Coordination | | ✅ |
| — | (single generic Remark) | | ➕ one unconditional remark instead of the four gated ones |
| — | Evaluation Status | | ➕ extra |

**Gaps:** missing Internal Appraisal Staff column; the four per-criterion score<4 remarks replaced by
one generic remark; `Chanel` mislabelled/mis-sourced; missing Purpose + Appraisal type filters;
composite sort unsupported. Per-criterion remarks require an Appraisal-domain schema change.

---

## RCAS009 — Appraisal-fee summary

**Impl:** `Rcas009/*`; view `vw_RCAS009_FeeSummary.sql`. FSD Tables 25–26. Format: Excel, PDF. Sort
(FSD): none.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Create Date from…to | `CreatedFrom/To` | ✅ (soft-default-to-today not applied server-side) |
| Invoice Status (default All) | `FeeStatus` | ✅ (maps to PaymentStatus) |
| Free/Fee type (default All) | `PayType` | ✅ |
| Appraisal Company (default All) | `AppraisalCompany` | ✅ |
| — | `AppraisalNumber` | ➕ |

### Fields
All FSD data fields present. Two are **out of order**: `Appraisal Status` is at impl position 8 but
FSD field 18; `Cost Center` at impl position 15 but FSD field 17. `Cost Center` is hardcoded `NULL`
in the view → **always blank**. `Fee Status` is an extra column (FSD lists only Appraisal Status). The
FSD Total Appraisal fee / VAT / Include VAT are produced as the exporter's column-sum total row.
Footer **Print Report By / Approve Report By** ✅ (added on this branch).

**Gaps:** column order (Status, Cost Center); Cost Center never populated; one extra column.
Filters aligned — this report's filters are **not** a source of complaint.

---

## RCAS010 — Monthly bank-absorbed fee expense — **STRUCTURALLY OFF**

**Impl:** `Rcas010/*`; view `vw_RCAS010_FeeExpenseBase.sql`. FSD Tables 27–28. Format: Excel, PDF.
Sort (FSD): none.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Chanel type (default All) | `Channel` | ✅ |
| Department (default All) | — | ❌ |
| AO Code (default All) | — | ❌ |
| Appraisal Create Date from…to | `CreatedFrom/To` | ✅ |
| Appraisal Status (default All) | — | ❌ |
| Free/Fee type (default All) | — | ❌ |
| Appraisal Company (default All) | — | ❌ |
| — | `AssignType` | ➕ (a report dimension, not an FSD filter) |

### Structure
FSD wants a **nested cross-tab**: rows keyed by `Chanel` + **`RM name`**; columns grouped
**Internal Appraisal** {Total, Customer Paid, Bank absorb} and **External Appraisal** {same}, each ×
{Number of book, Appraisal fee}, plus a **Grand Total** pair. The impl is a **flat** 8-column table
with AssignType as a *row* dimension, **no RM name**, and the Grand Total is a per-column all-rows sum
rather than the FSD's per-row Internal+External pair. `ReportColumn<T>` has a single flat header and
the exporter writes one header row, so the nested grouped header **cannot be rendered** without
exporter work.

**Gaps:** missing RM name; 5 of 7 filters missing; nested layout not representable. Needs a business
decision on nested vs flat before rebuild.

---

## RCAS011 — Detail by RM / AO caretaker

**Impl:** `Ola/OlaReport.cs` (`fsdDetail:true`, no scope). FSD Tables 29–30. Format: Excel. Sort
(FSD): Appraisal Report Number.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| Appraisal Report Number | `appraisalNumber` | ✅ |
| Purpose (default All) | — | ❌ **missing** |
| AO code (default All) | — | ❌ **missing** |
| Department Code (default All) | — | ❌ **missing** |
| Appraisal Create Date from…to | `createdFrom/To` | ✅ |
| Appraisal Status (default All) | `status` | ✅ |
| — | `internalStaff`, `appraisalCompany`, `channel` | ➕ |

### Fields
Same 20-field OLA set as RCAS003, in order. **Columns ✅.** Footer ✅.

**Gaps:** RCAS011 is "detail by RM/AO" yet has **no AO code, Department Code, or Purpose** filter — it
is columnwise identical to RCAS003 with a different title. Its defining filters are exactly the ones
missing.

---

## RCAS012 — Appraisal-company follow-up (SLA > 2 days) — **NOT IMPLEMENTED**

**Impl:** currently **aliased** onto the OLA report (`OlaReportsEndpoints.cs:39-40`, `fsdDetail:false`,
scope `External`). FSD Tables 31–32. Format: Excel, PDF. Sort (FSD): none.

### Selection criteria
| FSD criterion | Impl | Status |
|---|---|---|
| **SLA > 2 Days** (the defining filter) | — | ❌ **missing**; impl instead applies an `External` scope the FSD does not ask for |
| Appraisal report No. | `appraisalNumber` | ✅ |
| Appraisal Company (default All) | `appraisalCompany` | ✅ |
| Status (default All) | `status` | ✅ |
| Appraisal Create Date from…to | `createdFrom/To` | ✅ |
| — | `internalStaff`, `channel` | ➕ |

### Fields (FSD Table 32 — 21 fields)
Present via alias: Appraisal Report Number, Customer Name, Appraisal Purpose, Appraisal Company,
Internal Appraisal Staff, Appraisal date (appointment), Appraisal Report Status (7). **Missing (~15):**
Retail/IBG, Requestor Code/Name + phone + Department, External Appraisal Staff, Appraisal Company
phone, Internal Appraisal Staff phone, Appraisal fee, Appraisal Number create date, **OLA** (same
`setting − elapsed` formula as RCAS007's SLA), Appraisal Value, Current Role, Total Appraisal fee,
Total Appraisal Value.

**Verdict:** must be built (reuses RCAS007's view + the `SLA > 2 days` predicate).

---

## Cross-cutting root causes

1. **Single-column sort.** `ReportFilterSql.OrderBy` returns `"{field} {dir}"` only, so the FSD's
   composite sorts (RCAS002, 005, 006, 008) cannot be expressed. Fix: an overload taking an ordered
   field list, keeping the allow-list (it is the SQL-injection guard).
2. **Flat column headers.** `ReportColumn<T>` carries one `Header`; the exporter writes a single
   header row. RCAS010's nested cross-tab needs multi-level headers.
3. **Shared OLA filter/sort.** RCAS003/005/006/011 share one `OlaFilter` and one sort default, but
   the FSD gives each different criteria and sort sequences. Per-report criteria + sort are needed.
   Filters must bind a **code** passthrough, not the resolved display column (e.g. `Purpose` renders
   `COALESCE(pp.Description, code)`; a filter bound to that never matches — see `OlaReport.cs:67-70`).

## Business decisions (resolved 2026-07-18)

1. **RCAS008 `Chanel` → `BankingSegment`.** Its FSD field is annotated "Wholesale/Retail" = the
   Retail/IBG (BankingSegment) concept, so BankingSegment is correct. Current impl already does this;
   keep it.
2. **RCAS008 remark → show the field, ignore the score<4 condition.** No per-criterion / no schema
   change; the single always-shown remark is accepted. (The missing **Internal Appraisal Staff**
   column is still a real gap to fix.)
3. **RCAS009 `Cost Center` → the REQUESTOR's, via `AspNetUsers.AoCode` → `auth.Officers`
   (`OfficerCode`) → `CostCenterCode` → `auth.CostCenters` (`Code` → `Description`).** Confirmed
   requestor's AoCode. Verified all tables/columns exist. Reuse the `AoCode→Officers→CostCenters`
   chain from the Requestor-Detail feature.
4. **RCAS010 → nest if feasible, else flat is negotiable. `RM name` = requestor's user code + name.**
5. **RCAS007 / RCAS012 SLA — `SLA` and `OLA` are the SAME measure ("OLA" is a typo).** Value is a
   **number of days, 1 day = 8 business hours** (business hours only, via `IBusinessTimeCalculator`:
   `minutes / 60 / 8`, rounded to 2 dp — the exact model already used by `DetectDeliveryTimeQuery`).
   Source `appraisal.AppraisalAssignments.SubmittedAt`, which is stamped on first submission by
   `AppraisalAssignment.MarkUnderReview()` (`AppraisalAssignment.cs:317-325`) for **both** paths:
   external `ext-appraisal-verification → appraisal-book-verification` and internal
   `int-appraisal-execution → int-appraisal-check`.
   - **When `SubmittedAt` is set:** SLA = Appointment date → `SubmittedAt`.
   - **When null (not yet submitted):** SLA = Appointment date → now.
   - **Correction:** an earlier dev-DB check showed 0 internal `SubmittedAt` (0 of 105,321) — that is
     unprocessed load-test seed, **not** the code behaviour. In production internal books stamp
     `SubmittedAt` at execution→check. For internal this appointment→SubmittedAt window equals the
     int-appraisal-execution duration the FSD intends (matches `OlaTimingService`'s internal "OLA
     Appraisal" = appointment → int-execution complete).
   RCAS012 additionally filters `SLA > 2 days`. **Internal start anchor = Appointment date**
   (confirmed — unified with external).
6. **Follow the FSD field label literally — don't substitute `BankingSegment` where the FSD says
   "Channel".** For RCAS003/005/006/011 the FSD field is "Channel", so the real Channel field
   (MANUAL/CLS/LOS/SIBS) is correct — the current impl is right, **no change**. (This reverses an
   earlier "Channel = BankingSegment" reading.) RCAS008 keeps BankingSegment per (1) because its
   field is annotated Wholesale/Retail. The extra `channel` filter on the OLA reports stays (a
   legitimate Channel-field filter).
7. **RCAS007/012 are consumed** — backend menu seed (`MenuSeedData.cs:103,108`) and frontend
   (`operationalReports/routes.ts`, `config/reports.ts`, `api/types.ts`, as "OLA Report Ch.4/Ch.6").
   **So they cannot simply be removed** — de-aliasing must be coordinated with the FE, or (better)
   they should be built as their real reports. Revises the earlier "de-alias now" recommendation.

### All business questions resolved (2026-07-18)
Remaining sign-off items are none — items (3) requestor's AoCode, (5) Appointment-date anchor, and
(6) keep the Channel field are all confirmed above. Specs are build-ready.

## Appendix — implementation file map
| Report | Report/Row/Endpoint | SQL view |
|---|---|---|
| 001 | `OperationalReports/Rcas001/*` | `vw_RCAS001_AppraisalBooks.sql` |
| 002 | `Rcas002/*` | `vw_RCAS002_ReappraisalDue.sql` |
| 003/005/006/011 (+007/012 alias) | `Ola/OlaReport.cs`, `OlaReportRow.cs`, `OlaReportsEndpoints.cs`, `Shared/OlaTimingService.cs` | `vw_RCAS_OlaBase.sql` (on `vw_AppraisalList.sql`) |
| 004 | `Rcas004/*` | `vw_RCAS004_ConstructionInspection.sql` |
| 008 | `Rcas008/*` | `vw_RCAS008_ServiceQuality.sql` |
| 009 | `Rcas009/*` | `vw_RCAS009_FeeSummary.sql` |
| 010 | `Rcas010/*` | `vw_RCAS010_FeeExpenseBase.sql` |

Shared: `Shared/ReportDefinition.cs`, `Shared/ReportFilterSql.cs`, `Shared/IOperationalReportRunner.cs`,
`Infrastructure/Export/TabularExporter.cs`, `Reporting.Contracts/{ReportColumn,FilterCriterion,ReportSignoff}.cs`.
