# ตรวจงวด: บริษัทเดิม + ค่าตรวจงวด + เห็นค่าธรรมเนียมก่อนจ่ายงาน

สถานะ: **ผู้ใช้คอนเฟิม mock แล้ว (2026-10-21) — stack ต่อจากงาน guard: commit guard ใน local (ไม่ push) แล้วเปิด branch ใหม่ต่อจากนั้น ใน orca workspace ใหม่** · 2026-10-21
เกี่ยวข้อง: `.claude/tasks/ci-prior-construction-complete-guard.md` (BE-6 เปลี่ยนฐาน copy เป็นเล่มที่เลือก)

## 1. ระบบปัจจุบัน (ตรวจจากโค้ดแล้ว)

### 1.1 บริษัทเดิมถูกบังคับเงียบ ๆ
- `ResolveLatestInAppraisalChainQueryHandler` หา company จาก assignment ล่าสุดของ **chain tip** (เล่มที่ปิดล่าสุดทั้งต้นไม้) · internal = null
- `RequestSubmittedIntegrationEventConsumer.cs:104-114` ตั้งตัวแปร workflow `forceCompanyId/Name` ให้ Progressive
- `CompanySelectionActivity.cs:103-140`: `isForced` ชนะ `selectedCompanyId` ของ admin เสมอ แล้ว return ก่อน branch Manual/Quotation
  → assignment method = "Forced" · เทส `CompanySelectionActivityTests.cs:554-572` ล็อกพฤติกรรมนี้ไว้
- บริษัท forced หมด MOU → กลับ admin review แล้วกด EXT ก็ forced ซ้ำ → admin เลือกได้แค่ INT / EXTO
- **หน้า admin ไม่รู้อะไรเลย**: ไม่มี endpoint ไหนส่ง forced company (`assignment-defaults` มีแค่ type/method)
- PMA ก็ผ่าน `company-selection` → โดน forced เหมือนกัน
- หมายเหตุ phase 1: `ExternalCompanyAssignmentEnabled=false` → EXT ในระบบถูกบล็อกอยู่แล้ว (no_match) — อาการที่ผู้ใช้เห็นน่าจะมาจากสภาพแวดล้อมที่เปิด flag

### 1.2 ค่าตรวจงวดรอบถัดไปไม่ถูกใช้
- เก็บที่ `AppraisalFee.ConstructionInspectionFeeAmount` · กรอกที่หน้านัดหมาย/ค่าธรรมเนียม ช่อง "ค่าตรวจสอบการก่อสร้าง"
  (`FeeInformationSection.tsx:550-565`, hint บอกว่าจะใช้เป็นค่าประเมินของการตรวจครั้งถัดไป) · แสดงเมื่อมีอาคารกำลังก่อสร้าง
- `AssignmentFeeService.ResolveChainedInspectionFeeAsync` (:244-293) — **ต้นเหตุ**:
  - เล่มก่อนเป็นเล่มตั้งต้น → ใช้ `ConstructionInspectionFeeAmount` ✅
  - เล่มก่อนเป็นตรวจงวด (ครั้งที่ 2 ขึ้นไป) → ใช้ **Σ FeeItems FeeCode '01' ของเล่มก่อน** (= ค่าประเมินของมันเอง) ❌ ไม่อ่านค่าที่คีย์ไว้
- ใช้กับทุก method (tier / internal / EXTO / **quotation** — ราคา quotation ของ Progressive ถูกทิ้ง)
- ค่าว่าง/0 → ไม่สร้างรายการค่าธรรมเนียมเลย ไม่ fallback
- reassign → สร้าง fee shell ใหม่ copy แค่ payment type/notes/selling price/absorb **ไม่ copy ค่าตรวจงวดที่คีย์ไว้** (:66-73) แล้ว :291 อ่านเฉพาะ fee ของ assignment ล่าสุด → ค่าหาย
- บริษัทกับค่าธรรมเนียมอาจมาจากคนละเล่ม: บริษัทจาก chain tip, ค่าธรรมเนียม/copy จากเล่มที่ส่งเข้ามา (เส้น UI = เล่มที่เลือก)

### 1.3 เห็นค่าธรรมเนียมหลังจ่ายงานเท่านั้น
- fee row สร้างตอนสร้างเล่ม แต่ **ไม่มีรายการ ยอด 0** (`AppraisalCreationService.cs:365-379`)
- ยอดจริงถูกคำนวณตอน assign เท่านั้น (`EnsureAssignmentFeeItemsAsync` ใน handler company/internal/EXTO)
- `GET /appraisals/{id}/fees` ไม่ gate role/status · หน้าค่าธรรมเนียมถูกซ่อน/อ่านอย่างเดียวผ่าน menu override ต่อ activity
- ไม่มี endpoint preview ค่าธรรมเนียม
- เหตุที่คำนวณตอน assign: ยอดขึ้นกับวิธีจ่ายงาน (tier ตามราคาขาย / internal / quotation / ตรวจงวด chain)

## 2. ข้อเสนอ (รอตัดสิน — ดู 4)

### 2.1 บริษัทเดิม
- BE: `assignment-defaults` (หรือ endpoint ใหม่) คืน `previousCompany { companyId, companyName, appraisalNumber, inspectionNumber, completedAt, mouActive }`
  สำหรับ Progressive · แหล่งเดียวกับ fee/copy (ข้อ Q2)
- FE หน้า admin: banner "เคยตรวจงวดโดย บ. X (เล่ม …, ครั้งที่ n)" + preselect บ. X ในโหมด manual
- `CompanySelectionActivity`: admin เลือกเอง (selectedCompanyId มีค่า) → ใช้ของ admin · forced ใช้เมื่อไม่มีคนเลือก (auto routing/round-robin) — แก้เทส :554
- (ถ้าเลือกแบบมี dialog) เลือกบริษัทอื่น → ConfirmDialog ยืนยัน

### 2.2 ค่าตรวจงวด
- `ResolveChainedInspectionFeeAsync`: อ่าน `ConstructionInspectionFeeAmount` ของเล่มก่อน **ทุกประเภท** → ว่าง/0 ค่อย fallback Σ FeeCode '01' (พฤติกรรมเดิม)
- reassign copy `ConstructionInspectionFeeAmount` ไป fee shell ใหม่
- quotation / บริษัทอื่น → ข้อ Q3

### 2.3 เห็นค่าธรรมเนียมก่อนจ่ายงาน
- preview แบบอ่านอย่างเดียว ใช้ logic เดียวกับ `AssignmentFeeService` (ไม่เขียน DB) แสดงบนหน้า admin ตามประเภท/วิธีที่เลือก
  เช่น "ค่าธรรมเนียมที่จะเรียกเก็บ: 3,000 (ค่าตรวจงวดที่ตกลงไว้จากเล่ม …)" / "ตามตารางราคาขาย 1–3 ล้าน: 2,500"
- ยอดจริงยังสร้างตอน assign เหมือนเดิม

## 3. Todo (หลังตัดสิน)
- [ ] BE: previousCompany ใน assignment-defaults + เทส
- [ ] BE: CompanySelectionActivity ลำดับความสำคัญใหม่ + แก้เทส
- [ ] BE: fee chain อ่านค่าที่คีย์ + fallback + copy ตอน reassign + เทส
- [ ] BE: fee preview endpoint + เทส
- [ ] BE: admin เพิ่ม/แก้ค่าธรรมเนียม (Source=Admin, ข้ามอนุมัติ, ล็อกจากผู้ได้งาน, guard 01, สร้าง fee row ถ้าไม่มี, แก้ HasItems) + สิทธิ์ + เทส
- [x] Mock หน้า 2 ใน https://claude.ai/artifact/MGNRUd2TBujZ873FHmpbhj (board 4–6) — รอรีวิว
- [ ] FE: banner/preselect/dialog หน้า admin + fee preview + i18n
- [ ] QA ผ่าน subagent

## 4. ตัดสินแล้ว (ผู้ใช้ 2026-10-21)
- Q1 admin เลือกบริษัทอื่น → **admin ชนะ + dialog ยืนยัน** · บริษัทเดิมถูก preselect · forced เหลือเฉพาะเส้นที่ไม่มีคนเลือก
- Q2 บริษัทเดิม = บริษัทของ **เล่มที่ผู้ใช้เลือก** (แหล่งเดียวกับ copy + ค่าตรวจงวด) — เลิกใช้ chain tip ใน consumer
- Q3 ค่าธรรมเนียม: **quotation → ราคา quotation** · manual/internal/EXTO (แม้เปลี่ยนบริษัท) → ค่าตรวจงวดที่คีย์ไว้ (ว่าง → ค่าประเมินเล่มก่อน)
- Q4 preview ค่าธรรมเนียม **ทุกงาน บนหน้า admin** (อ่านอย่างเดียว ตามประเภท/วิธีที่เลือก + ที่มา)
- Q5 (เพิ่ม 2026-10-21) **admin เพิ่ม/แก้ค่าธรรมเนียมได้บนหน้า admin ก่อนมอบหมาย — ทั้งสองแบบ**: เพิ่มรายการเพิ่มเติม + แก้ยอดค่าประเมินหลัก
  · รายการที่ admin ใส่ = **บริษัท/เจ้าหน้าที่ที่ได้งานแก้ไม่ได้ เห็นอย่างเดียว** (ป้าย "กำหนดโดย admin") · ยังเพิ่มรายการของตัวเองได้ตามเดิม
  · ยอดหลักที่ admin แก้ **ชนะเสมอ** (รวม quotation/ค่าตรวจงวด) · ล้างยอดที่แก้ = กลับไปใช้ค่าที่ระบบคำนวณ
  · รายการของ admin **ไม่ต้องผ่านอนุมัติ** มีผลทันที
  · ข้อเท็จจริง (สำรวจ 2026-10-21): fee row สร้างตอนเปิดเล่ม (ถ้าคำขอมี FeeDto) ผูก assignment ของ AssignAdmin แถวเดียวใช้ต่อหลังมอบหมาย
    → รายการไม่หาย · 🔴 `AssignmentFeeService.cs:83-89` ข้ามทั้งหมดถ้า `HasItems` → ต้องเปลี่ยนเป็น "มี 01 แล้ว"
    · BE ไม่กันลบ 01 (กันแค่ FE) · ไม่มี fee row ถ้าคำขอไม่มี FeeDto → สร้างตอน admin เพิ่ม
    · seed: IntAdmin มีแค่สิทธิ์ view หน้า appointment/administration → เช็ค DB จริง/เพิ่มสิทธิ์
    · ประเภท: TypeOfFee 01 ค่าประเมิน / 02 Inspection fee / 99 อื่น ๆ
  · ออกแบบ: `Source = Admin` บน item (ฟิลด์ Source มีอยู่แล้ว) → BE กันบริษัท/เจ้าหน้าที่แก้/ลบ · admin แก้ยอดหลัก = สร้าง/แก้ item 01 Source=Admin ก่อน assign


### ตัดสินเพิ่ม (ผู้ใช้ 2026-10-22)
- ค่าธรรมเนียม + บริษัทเดิม อ่านจากเล่ม base ที่ผู้ใช้เลือก (Assignments / AppraisalFees) ไม่ใช่ collateral module
- Layout การ์ดค่าธรรมเนียม: จอ ≥ xl → คอลัมน์ขวา 360px sticky · จอแคบ → เต็มความกว้างระหว่างรายละเอียดกับหมายเหตุ (mock board 8) · ปุ่มคง sticky footer เดิม
- ⏸ ล็อก admin-stage ให้เฉพาะผู้ใช้ภายใน (`IsExternal` → 403) — **ผู้ใช้ให้ข้ามไปก่อน** ช่องที่เปิดอยู่: ผู้ใช้ภายนอกที่รู้ appraisalId เรียก add/update/remove ตอน assignment Pending ได้รายการ Admin ข้ามอนุมัติ (และแก้ยอด 01 ซึ่งชนะเสมอ)
- [x] FE responsive layout (fe-fee-layout → test-automator): tsc 0 · eslint ไม่มีใหม่ · vitest 2976 pass · ยังไม่ได้ดูบนเบราว์เซอร์

### ตัดสินเพิ่ม (ผู้ใช้ 2026-10-24/25) — แทนที่ข้อขัดกันด้านบน
- Q3 แก้: เล่มก่อนไม่ได้คีย์ค่าตรวจงวดถัดไป → **ไม่ดึงอะไร** (ไม่ fallback ค่าประเมินเล่มก่อน)
- หลังมอบหมาย ผู้รับงานแก้/เพิ่ม 01 **ไม่ได้เลย** · แก้ค่า fee = route-back ให้ admin
- **ไม่มีค่าประเมิน 01 (> 0) = บล็อกมอบหมาย ทุกประเภท (INT/EXT/EXTO/P)** — FE ใบสั่งงานแดง + BE Assign 400 `FeeBaseMissing` · auto-assign ข้ามพร้อมเหตุผล `feeBaseMissing`
- fee-preview endpoint ไม่กันผู้ใช้ภายนอก — **"ไม่กันตอนนี้"** (รวมกับ role lock ที่พักไว้) · ไม่เช็ค feeId ↔ appraisalId (ตั้งใจ)
- ยอดรายการ fee ต้อง > 0 ทุกคน (FE หน้า fee ผู้รับงานบังคับเหมือนกัน)
- ตรวจงวดคอนโด/เช่าคอนโด: BE ตรึงฐาน TotalValue = 0
- เล่มตรวจงวดใหม่: "งวดนี้" เริ่ม = งวดก่อน ทั้งสองแบบ
- AssignedBy = admin ที่กดมอบหมาย (รวม EXTO; ไม่ใช่ keyer) · ปฏิเสธ/ขอข้อมูลเพิ่ม ไม่บันทึกผู้มอบหมาย
- dialog ยืนยันเปลี่ยนบริษัท: **ถามทุกครั้ง** ที่เลือกบริษัทอื่นขณะบริษัทเดิมยังใช้ได้ (MOU active) ไม่ว่าจะล้าง/สลับประเภทมาก่อนหรือไม่ · หัว "ใช้บริษัทอื่นแทนบริษัทที่เคยตรวจงวด?" (2026-10-25)
- request ไม่มีข้อมูล fee (ไม่มี fee row / แถวว่าง) → **ไม่ใช้แหล่งราคาใดเลย** แม้มีราคา quotation/ค่าตรวจงวดเล่มก่อน · admin กำหนดยอดเอง · auto-assign ข้าม (ผู้ใช้ "คงแบบตอนนี้" 2026-10-25)
- เกณฑ์หยุด /code-review งานนี้ (ผู้ใช้ 2026-10-25 "ทางที่ 2"): รันยืนยันอีกรอบ ถ้าเหลือแต่เรื่องที่ตัดสินแล้ว/พักไว้/refactor จุกจิก → หยุด แล้ว probe หน้าจริงก่อนผู้ใช้เทส

## Review
(2026-10-25) สรุปรวมอยู่ใน `admin-page-redesign.md` หัวข้อ Review — งานบริษัท/ค่าธรรมเนียมกับ redesign หน้ามอบหมายรวมเป็นชุดเดียวกัน ยังไม่ commit · รอ probe + ผู้ใช้เทส
(เติมหลังทำเสร็จ)

### บั๊กจากผู้ใช้เทส (2026-10-22)
- [x] บันทึกโมดัลเพิ่มค่าธรรมเนียมบนหน้ามอบหมาย = ส่งงานออกไปด้วย — React bubble `submit` ข้าม portal ของ HeadlessUI ไปถึง `<form>` ของหน้า. แก้ที่ `src/shared/components/Modal.tsx`: `stopPropagation` บน div เนื้อหา (ไม่ใช่ `DialogPanel` — HeadlessUI ข้าม on* handler เมื่อ `defaultPrevented` ซึ่ง RHF เรียกเสมอ). เทส `Modal.test.tsx` + `AssignmentFeeCard.test.tsx` ล้มเมื่อถอดบรรทัดแก้ · tsc 0 · vitest 2984 ผ่าน

### เปลี่ยนกติกาค่าตรวจงวด (ผู้ใช้ 2026-10-24)
- [x] เล่มก่อนไม่ได้บันทึก "ค่าตรวจสอบการก่อสร้าง" → ไม่ดึงอะไร (เลิก fallback ค่าประเมิน Σ 01) · kind None + appraisalNumber · ตอนมอบหมายไม่สร้าง 01 · admin กำหนดยอดเองได้ · ลบ `PriorAppraisalFee` ทั้ง BE/FE · reword `NoneFromPrior`
  - gate: FE tsc 0 · vitest 2986 · BE build 0 · Appraisal 1093 · Request 420 · Workflow 1155 · integration 615/617 (ClosedAppraisalWriteGuard timeout + Puppeteer dispose NRE — รันแยกผ่าน) · โน้ตเหลืองซ่อนเมื่อ admin กำหนดยอดแล้ว
