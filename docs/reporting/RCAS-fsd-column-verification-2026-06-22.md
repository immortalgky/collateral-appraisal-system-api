# RCAS Operational Reports — FSD Column Verification (2026-06-22)

> **⛔ SUPERSEDED (2026-07-18) by [`RCAS-field-reference.md`](RCAS-field-reference.md).**
> This pass checked **columns only** and was wrong in places (it called RCAS001/002 "aligned" — they
> are not) and never checked selection-criteria/filters or sort order. Use the field reference as the
> source of truth; this file is kept only for history.

**FSD source:** `.claude/docs/LHB FSD Appraisal Report v1.0.docx` — RCAS001–RCAS012 ("Detail of Field" tables).
**Implementation:** `Modules/Reporting/Reporting/Application/OperationalReports/*` (column lists) + `Database/Scripts/Views/Reporting/vw_RCAS*.sql` (data).

## What changed in this pass — code → description

The RCAS views are layered on `appraisal.vw_AppraisalList`, which passes through **raw codes**
(`Purpose='01'`, `PropertyType='LB'`, `ValuationApproach='WQS'`, `FeePaymentType='04'`) and a raw
**GUID** for the assignee. The FSD field tables expect human-readable text. Fixed by resolving each
code via `parameter.Parameters` (`[Group]` + `[Language]='EN'` + `Code`), `COALESCE(desc, rawcode)`
so unmapped/legacy codes still render. This reuses the pattern already in `workflow.vw_TaskMonitor`.

| Column | Source code | Resolved via group | Views touched |
|---|---|---|---|
| Appraisal Purpose | `Appraisals.Purpose` (`01`) | `AppraisalPurpose` | RCAS001, 004, 009, OLA(003/005/006/011) |
| Collateral Type | `AppraisalProperties.PropertyType` (`LB`) | `PropertyType` | RCAS001, 004, 009, OLA |
| Approach Method | `ValuationAnalyses.ValuationApproach` (`WQS`) | `ApproachMethod` | RCAS001 |
| Pay Type | `AppraisalFee.FeePaymentType` (`04`) | `FeePaymentMethod` | RCAS009 |
| Internal Appraisal Staff | `AssigneeUserId` (**GUID**) | `auth.AspNetUsers` → `First Last` | RCAS001, 004, 009, OLA |

Already-readable text passed through unchanged: **Status** (`Pending`/`Completed`…),
**Assign Type** (`Internal`/`External`), **Fee Status** (`NotPaid`/`Partial`/`PendingInvoice`/`Paid`),
**Channel** (`LOS`/`CLS`/`SIBS`/`MANUAL`), **Retail/IBG** (`RETAIL`/`IBG`).

## Column alignment vs FSD

| Report | Verdict | Notes |
|---|---|---|
| **RCAS001** Appraisal books | ✅ Aligned | All 15 FSD fields present, same order. Codes now resolved. |
| **RCAS002** Reappraisal due | ✅ Aligned | All 18 FSD fields present. `CollateralType`/`InternalStaff` come from the AS400-sourced `collateral.ReappraisalCandidates` (already text), not resolved here. |
| **RCAS003/005/006/011** OLA | ✅ Columns closed | Core + OLA segment columns match. `Appraisal Create Date`, `Role`, and the `Print Report By` / `Approve Report By` footer were **added 2026-07-17** (gated to these four via `OlaReport.Create(fsdDetail: true)`, so RCAS007/012 are unaffected). Totals covered via `Total:true` on Apply/Limit Amount. Filter/sort gaps remain — see below. |
| **RCAS004** Construction inspection <100% | ✅ Aligned | All 13 FSD fields present, same order. Codes now resolved. |
| **RCAS008** Service quality | ⚠️ Minor gaps | Score columns match. FSD lists `Internal Appraisal Staff` (impl shows `Retail/IBG` instead) and a per-criterion `Remark` shown only when score < 4 (impl has one combined `Remark`). Impl adds `Evaluation Status` (extra). |
| **RCAS009** Fee summary | ⚠️ Minor gaps | All FSD data fields present. `CostCenter` is `NULL` (not captured — confirm with business). Impl adds `Fee Status` (not in FSD). `Print/Approve Report By` footer not implemented. Codes now resolved. |
| **RCAS010** Bank-absorbed fee | ⚠️ Structural diff | FSD pivots Internal/External as nested column groups per `RM name`; impl uses `AssignType` as a row dimension with Customer-Paid / Bank-Absorb count+fee columns. Data equivalent, layout simplified. No `RM name` / `Grand Total` row label. |
| **RCAS007** SLA Internal&External / **RCAS012** Follow-up | ❌ Not implemented | No endpoint/view yet. (RCAS007 ≈ OLA-style + fees; RCAS012 = SLA>2-day follow-up list.) |

> **⚠️ This audit checked COLUMNS ONLY and is known to be incomplete.** A later pass (2026-07-17)
> found it also missed **selection-criteria (filter) and sort-order** gaps in most reports, and that
> RCAS001/002 are *not* aligned as stated below. Treat the verdicts here as provisional until the
> table-aware re-verification lands. Known additions: RCAS001 missing `Running No.` + `Requestor
> Department` filter; RCAS002 missing the `Create Date` range filter entirely + composite sort;
> RCAS003/011 missing `Customer Number`/`Purpose`/`AO code`/`External Appraisal Staff` filters;
> RCAS009 column **order** differs from the FSD. RCAS007/012 are currently **aliased** to the OLA
> report under their own titles — they return OLA data, not RCAS007/012 data.

## Remaining (non-code) gaps worth a follow-up decision
1. ~~OLA reports: add `Appraisal Create Date`, `Role`, and Print/Approve "Report By" footer.~~
   **Done 2026-07-17.** `Role` is sourced from the staff's Auth role (`auth.AspNetRoles`), and the
   staff is picked by `AssignmentType` (`External` → `InternalAppraiserId`, else `AssigneeUserId`) —
   which also fixed `Internal Staff` being blank on every external assignment.
2. RCAS008: confirm whether `Internal Appraisal Staff` column should replace/precede `Retail/IBG`; per-criterion remark gating (<4).
3. RCAS009: `Cost Center` source; whether `Fee Status` is an accepted addition.
4. RCAS010: confirm the `RM name`-pivot layout is acceptable as the simplified `AssignType` form.
5. RCAS007 & RCAS012 are unbuilt.

## How to verify
1. Re-deploy the repeatable views (user runs the Database project / DbUp — do **not** apply directly).
2. Hit each report endpoint (`/reports/rcas001` … preview) and confirm `Purpose`, `Collateral Type`,
   `Approach Method`, `Pay Type` show labels (e.g. "Request for credit limit", "Land and Building")
   and `Internal Staff` shows a person name, not a GUID.
3. Spot-check an appraisal with multiple property types → `Collateral Type` is a comma-joined list of labels.
