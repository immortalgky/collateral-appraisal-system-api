# Redesign หน้ามอบหมาย (Administration) — ฟอร์ม + ใบสั่งงาน

รวมในงาน ci-company-and-fee (FE worktree `~/orca/workspaces/collateral-appraisal-system-app/ci-company-and-fee`,
BE worktree `~/orca/workspaces/collateral-appraisal-system-api/ci-company-and-fee`). ยังไม่ commit จนผู้ใช้เทส

## ที่มา / สิ่งที่ตัดสินแล้ว (ผู้ใช้ 2026-10-24)

- โครงหน้า = **ฟอร์ม + ใบสั่งงาน** หน้าตาแอปเดิม (DESIGN.md) — เลือกจากรอบ 3 (canon)
- mock อนุมัติแล้ว: artifact MGNRUd2TBujZ873FHmpbhj หน้า "4 · Redesign" (Main / Quotation / Variants / States / Narrow)
- ยอดก่อน VAT / VAT / รวมทั้งสิ้น อยู่ในใบสั่งงานที่เดียว การ์ดค่าธรรมเนียมเหลือแค่รายการ
- ข้อความเตือนช่องไม่ครบ ขึ้นหลังแตะช่องนั้น หรือหลังกด "ไปแก้" (ไม่ขึ้นตอนเปิดหน้า)
- **ไม่มี** ไฮไลต์ "เพิ่งเปลี่ยน" ในใบสั่งงาน
- มุมมองหลังมอบหมาย แสดงผู้มอบหมาย **ทั้งชื่อและรหัสพนักงาน** → ต้องเพิ่มชื่อจาก BE
- ~~ยังไม่มียอดค่าธรรมเนียม = เตือนเหลือง ไม่บล็อกมอบหมาย~~ → **เปลี่ยน 2026-10-25: ไม่มีค่าประเมิน 01 = บล็อกมอบหมาย ทุกประเภท** (FE แดง + BE Assign 400) · ผู้รับงานเพิ่ม 01 หลังมอบหมายไม่ได้ แก้ด้วย route-back ให้ admin
- ตรวจงวดคอนโด/เช่าคอนโด: BE ตรึงฐานเป็น 0 ไม่เชื่อ TotalValue จาก client (2026-10-25)
- หน้า fee ผู้รับงาน: ยอดต้อง > 0 ตรงกับ BE (2026-10-25)
- เล่มตรวจงวดใหม่ (CopyForNextInspection): "งวดนี้" เริ่มต้น = งวดก่อน ทั้งแบบสรุปและแยกรายการ · ไม่ backfill · การ์ดแบบสรุปที่ยังไม่กรอก (null) แสดง "กำลังก่อสร้าง" ไม่มี % (2026-10-25, ผู้ใช้เจอการ์ด 0% ขณะฟอร์มแสดง 60)
- handler ผู้ติดตาม (InternalFollowupAssigned) ย้ายไปใช้ AppraisalAssignmentElection.Current() (2026-10-25)
- แผง Application Details และ sidebar ไม่แตะ
- QuotationSection: ปรับข้างในด้วย (ผู้ใช้เลือก "redesign ข้างในด้วย")
- เกณฑ์คุณภาพ: Linear, Stripe Dashboard, Shopify Admin, Jira
- แนวทาง impeccable: `.impeccable/surfaces/es-appraisal-pages-administrationpage-tsx-10eb57ba.md` (FE) · inventory ทุกฟังก์ชัน: scratchpad `admin-inventory.md`

## ต้องถามผู้ใช้ก่อนเริ่ม

- [x] **Q1 แผง quotation ที่ใช้ร่วม** (ผู้ใช้: ตามที่แนะนำ) — `AdminShortlistPanel` (ใช้ใน QuotationSelectionPage ด้วย) · `ShortlistSentPanel` / `NegotiationPanel` (ใช้ใน QuotationStatusView/QuotationSelectionPage ด้วย) · `QuotationSection` เองใช้ใน `AdminQuotationTaskPage` ด้วย
  แนะนำ: ปรับ **การ์ดสถานะของ QuotationSection** (หัว สถิติ ตารางคำตอบ ปุ่ม) ซึ่งจะเปลี่ยนใน AdminQuotationTaskPage ด้วย แต่ **ไม่แตะ 3 panel ที่ใช้ร่วม** (แค่วางในกรอบใหม่) — กันหน้า quotation อื่นเปลี่ยนตาม
- [x] **Q2 `.impeccable/` + `PRODUCT.md`** (ผู้ใช้: ตามที่แนะนำ) ใน FE — commit เข้า repo หรือ ignore (แนะนำ: commit `PRODUCT.md` + `.impeccable/surfaces/`, ignore `.impeccable/mocks/`)

## คำ "วนเวียน" (ผู้ใช้ขอเปลี่ยน 2026-10-24)
- หน้ามอบหมาย: "วนเวียน" → **"จ่ายงานอัตโนมัติ"** · หัวกล่อง "การจ่ายงานอัตโนมัติ" · ติดตาม: "ระบบจะจ่ายงานให้เจ้าหน้าที่ติดตามอัตโนมัติตามลำดับคิว"
- workflow builder (strategies.roundRobin / selectionMethods.roundrobin): "จ่ายงานตามคิว (Round-robin)"

## แผนแบ่งงาน (ไฟล์ไม่ทับกัน)

### เฟส 1 — ทำคู่ขนาน

**A. BE (dotnet-backend) — ชื่อผู้มอบหมาย**
- [ ] เพิ่ม `assignedByName` (pinned nullable) ใน assignment DTO ที่หน้าอ่าน (`GET` assignment ของ appraisal) — resolve จาก employee code ตาม pattern ที่ BE มีอยู่ (เช่น `GetQuotationByIdQueryHandler` ที่ resolve `*ByName`) · ไม่มีชื่อ = null (FE โชว์รหัสอย่างเดียว)
- [ ] เทส unit/integration + build

**B. FE ใบสั่งงาน + ส่วนประกอบใหม่ (react-expert #1)** — ไฟล์ใหม่ทั้งหมด
- [ ] `components/administration/AssignmentSlip.tsx` — ช่อง ประเภท / ผู้รับ / ติดตาม / ค่าธรรมเนียม (ก่อน VAT, VAT, รวม) / ต้องตรวจก่อนส่ง (✓ / ! เหลือง / × แดง + "ไปแก้") / ปุ่ม มอบหมาย·บันทึกฉบับร่าง·ยกเลิก + เหตุผลเมื่อกดไม่ได้ + unsaved · โหมดอ่านอย่างเดียว (มอบหมายแล้ว · ผู้มอบหมาย ชื่อ + รหัส · เวลา · ค่าธรรมเนียมจริง)
- [ ] `AssignmentSlipBar.tsx` — จอแคบ: แถบติดล่าง (ยอดรวม + ตรวจ x/y + มอบหมาย) กดขยายเป็น bottom sheet (Headless UI Dialog)
- [ ] `SegmentedChoice.tsx` — segmented radio (Headless RadioGroup) + คำอธิบายตัวที่เลือก ใช้กับ ประเภท / วิธี / ติดตาม / ผู้คีย์
- [ ] `slipChecks.ts` — คำนวณรายการตรวจ + เหตุผลปุ่มกดไม่ได้ จาก form state (pure function + unit test)
- [ ] คีย์ i18n ใหม่ en/th/zh (namespace appraisal `administration.slip.*`)

**C. FE QuotationSection (react-expert #2)** — เฉพาะ `QuotationSection.tsx` (+ test ของมัน) ตามคำตอบ Q1
- [ ] การ์ดสถานะใหม่ตาม board Quotation: หัว (เลข + badge + นับถอยหลัง) · สถิติ · ตารางคำตอบ (ไม่ตอบ = "—") · ปุ่มตามสถานะ · Draft edit · ยกเลิก
- [ ] ใช้ใบเสนอราคาเดียวกับที่ล็อกหน้า (แก้ newest vs active ไม่ตรงกัน) — รับ quotation ที่ active มาจากหน้า หรือใช้ `pickActiveQuotation` เดียวกัน
- [ ] ป้ายภาษาอังกฤษตายตัวในอีเมล/entry modal → i18n (คีย์ใหม่)

### เฟส 2 — หลัง B เสร็จ (ไฟล์หน้าเป็นของคนเดียว)

**D. FE หน้า + การ์ด (react-expert #1 ต่อ)**
- [ ] `AdministrationPage.tsx` จัดโครงใหม่: คอลัมน์ฟอร์มซ้าย + ใบสั่งงานขวา (container query: แคบกว่า ~ 64rem → `AssignmentSlipBar`) · เอาแถบปุ่มล่างออก · แถบบริษัทเดิม/ล็อก เป็นแถวบนคอลัมน์ฟอร์ม
- [ ] การ์ดประเภท / รายละเอียด / นอกระบบ ใช้ `SegmentedChoice` · ตัวเลือกที่ disabled + hint เดิม
- [ ] `AssignmentFeeCard.tsx` เอาบรรทัดยอดรวมออก (ย้ายไปใบสั่งงาน, อ่าน query เดียวกัน ไม่ยิงซ้ำ)
- [ ] validation: error บริษัทภายนอก (path `companyId`) ให้ขึ้นจริง · ข้อความหมายเหตุ 4000 · แสดง error หลัง touched หรือหลัง "ไปแก้" (focus + scroll ไปช่อง)
- [ ] `SearchStaffModal` / `SearchCompanyModal` หัวโมดัล + "{n} tasks"/"{n} active" → i18n · "Cancel" → `common` key
- [ ] read-only: ใบสั่งงานโหมดมอบหมายแล้ว ใช้ `assignedByName` + `assignedBy`

### เฟส 3 — QA (test-automator คู่ react-expert ทุกเฟส)
- [ ] unit: `slipChecks` · slip render ทุกสถานะ · SegmentedChoice keyboard · AdministrationPage (ปรับเทสเดิม 11 ตัว) · QuotationSection ทุกสถานะ · AssignmentFeeCard (ไม่มียอดรวมแล้ว)
- [ ] gate: `tsc -b --force` 0 · vitest ทั้งชุด · prettier ไฟล์ที่แก้ · BE build + unit + integration
- [ ] probe หน้าจริงบน 3000/7111 (บล็อก non-GET): จอ 1440 แผงเปิด/ปิด, 1920, 760 — ภาพหน้าจอเทียบ mock · Thai สระบนไม่ถูกตัด
- [ ] impeccable detect บนไฟล์ที่เปลี่ยน → finish reviewer เทียบ direction contract
- [ ] /code-review วนจน clean (`--max-findings all`)

## ความเสี่ยง
- `AdministrationPage.tsx` 1,630 บรรทัด — เขียนโครงใหม่แต่คง logic ฟอร์ม/submit/draft/guard เดิมทุกตัว (ห้ามเปลี่ยนพฤติกรรม API)
- QuotationSection 1,867 บรรทัด ใช้ใน AdminQuotationTaskPage ด้วย → ต้อง probe หน้านั้นด้วย
- ใบสั่งงานต้องอ่าน fee-preview query key เดียวกับการ์ด (ไม่ยิง API ซ้ำ)

## Review (2026-10-25)

**สถานะ:** โค้ดเสร็จ ยังไม่ commit · รอ probe หน้าจริง (3000/7111 ต้องรัน ci-company-and-fee) แล้วผู้ใช้เทส

**เกตล่าสุด**
- FE ci-company-and-fee: tsc 0 · vitest 3274 ผ่าน / 0 ล้ม · prettier สะอาด · mutation check ทุกการแก้ตั้งแต่รอบ 5
- FE ci-prior-construction-guard: tsc 0 · vitest 2963 ผ่าน
- BE ci-company-and-fee: build 0 · Appraisal 1253 · Request 427 · Workflow 1156 (+1 timing flake) · Collateral 149 · integration 629/631 (2 flake ผ่านเมื่อรันเดี่ยว)
- BE porcupinefish (guard): build 0 · Appraisal 1050 · Request 427 · Workflow 1148 · integration 617/618 (flake เดิม)

**/code-review** วน 13 รอบ + รอบยืนยัน ไม่ถึงศูนย์ (`--max-findings all` หยิบเรื่องจุกจิกเสมอ) → หยุดตามเกณฑ์ที่ผู้ใช้เลือก ("ทางที่ 2"): ที่เหลือเป็นเรื่องตัดสินแล้ว / พักไว้ (หัวข้อ ค้าง) / refactor จุกจิก

**ที่เปลี่ยนหลัก**
- หน้ามอบหมาย: ฟอร์ม + ใบสั่งงาน (rail ≥64rem / แถบล่างจอแคบ) · SegmentedChoice · error หลังแตะ/ไปแก้ · dialog/picker ตาม mock · ไม่มี `<form>` (Enter ไม่ submit)
- กฎค่าธรรมเนียม: ไม่มี 01 ที่เรียกเก็บได้ = บล็อกทุกประเภท — BE คำนวณ `hasBillableBase` ที่เดียว (preview + Assign guard) FE ใช้ค่าเดียวกัน
- admin stage: 01 ของระบบที่ซ่อนอยู่ → admin เขียนทับได้ (กันงาน route-back ค้าง) · ข้ามล็อกรอรายการอนุมัติ · หลังมอบหมาย 409 แทน 500
- dialog เปลี่ยนบริษัท ถามทุกครั้งที่เลือกบริษัทอื่นขณะบริษัทเดิมใช้ได้
- ตรวจงวด: งวดใหม่เริ่ม = งวดก่อน · คอนโดฐาน 0 (รวมตอนคัดลอก) · การ์ดแบบสรุปที่ยังไม่กรอกไม่ขึ้น 0% ปลอม
- workflow: บริษัทที่บังคับแต่มอบหมายไม่ได้ → ส่งให้ admin ครั้งเดียวแล้วล้าง force (ไม่วน)

**ก่อน PR ต้องถาม:** role lock admin-stage + gate fee-preview (ช่องใหม่: ช่วงหลังกดมอบหมาย-ก่อน consumer) · deploy note chain ตรวจงวดค้าง · เรื่องค้างด้านล่าง

**ตอน commit:** gitignore `.impeccable/mocks/` (+ questions) · commit PRODUCT.md + `.impeccable/surfaces/` · ไม่ commit การลบ `.env.example`

## ค้าง (ผู้ใช้: "เด่วค่อยทำ" 2026-10-25)
- [ ] `common.vw_CompanyOverview` ช้า ~6-7 วิ CPU ต่อครั้ง (aggregate vw_AppraisalEvaluationList + vw_MonitoringPendingTasks ทุกบริษัท, TRY_CAST กัน predicate) — เรียกจาก GET /companies, /companies/{id} (หน้ามอบหมายเรียกบริษัทเดิมทุกครั้งตั้งแต่รอบ 3), /companies/eligible. ทางแก้: (1) ปรับ view/query ให้กรองก่อน aggregate (แนะนำ) (2) หน้ามอบหมายโหลดชื่อบริษัทเดิมแบบไม่ดึง overview
- [ ] fee-preview endpoint ไม่กันผู้ใช้ภายนอก (ผู้ใช้: ไม่กันตอนนี้) — รวมกับ role lock ที่พักไว้
- [ ] filtered unique index 01 ต่อ fee (ต้อง migration; dev มี 1 fee ซ้ำ System ทั้งคู่ — ก่อนงานนี้)
- [ ] จุดอื่นที่ยังเลือก current assignment ด้วย AssignedAt (ไม่เกี่ยวค่าธรรมเนียม) — ถามผู้ใช้ว่าจะ align ไหม
- [ ] (review รอบ 5) deploy note: chain ตรวจงวดที่ค้างอยู่ก่อน deploy ซึ่งเล่มก่อนไม่ได้คีย์ "ค่าตรวจสอบการก่อสร้าง" → รอบถัดไปจะไม่มี 01 (ตามกติกาไม่ fallback) admin ต้องกำหนดยอดเอง — แจ้งผู้ใช้/ทีมก่อน go-live
- [ ] (review รอบ 5) admin stage กรอก bank absorb ได้ไม่จำกัดเพดาน — ไม่แก้ (safeguard ต้องถามก่อน)
- [ ] (review รอบ 5) refactor RestoreRequestedBankAbsorb 5 จุด → เก็บ requested absorb แยก — ยังไม่ทำ (refactor ใหญ่)
- [ ] (review รอบ 5) submit ตรวจงวดพร้อมกัน 2 request บน chain เดียว ผ่านทั้งคู่ (ไม่มี lock) — ยังไม่ทำ
- [ ] (review รอบ 7) EXTO รอบ 2+ : keyer บันทึกก่อน consumer InternalAssigned ทำงาน (ช่วงไม่กี่วินาที) จะได้ 400 — รับได้ กดใหม่ผ่าน
- [ ] (review รอบ 7) FeeShellFactory / LatestFee fallback อาจเป็นทางตาย (1 appraisal = 1 assignment row) — ไม่ลบเพราะข้อมูลเก่าอาจมีหลาย row
- [ ] (review รอบ 7) Assign guard เรียก fee-preview ทั้งตัว (รวม lookup ชื่อผู้ชนะ) — ยอมรับต้นทุน
- [ ] (2026-10-25) จุดที่ยังแปลง % งวดนี้แบบสรุปที่ null → 0: vw_AppraisalConstructionProgress (บรรทัด 51/56), vw_MisCasReport (152/156), GetDecisionSummaryQueryHandler (573/581) — หลังแก้ copy แล้ว null เกิดเฉพาะเล่มก่อนที่ไม่เคยกรอก · ยังไม่แตะ (view ต้อง migrate)
- [ ] (2026-10-25) คำเตือน "แถวที่คัดลอกมายังเป็น 0" (isPendingProgress) จะไม่ขึ้นอีกเพราะงวดนี้เริ่ม = งวดก่อน — ถ้าอยากได้ตัวเตือน "ยังไม่ได้อัปเดต" ต้องเป็นงานใหม่
