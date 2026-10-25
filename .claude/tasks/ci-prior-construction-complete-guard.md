# ตรวจงวดก่อสร้าง: กันเล่มเดิมที่ครบ 100% + ใช้ผลใบทบทวนในสาย

สถานะ: **แผน — ตัดสินแล้ว รอผู้ใช้อนุมัติแผน ยังไม่เริ่มโค้ด** · 2026-10-21

## 1. ระบบปัจจุบัน (ตรวจจากโค้ดแล้ว)

### % ปลูกสร้างเก็บที่ไหน
- **ไม่มีการเก็บสรุประดับเล่ม — คำนวณสดทุกครั้ง** จาก `appraisal.ConstructionInspections` (1 แถว/property)
  + `ConstructionWorkDetails`
  - รายหลัง: `IsFullDetail ? Σ CurrentProportionPct : SummaryCurrentProgressPct`
    (`ConstructionInspection.OverallCurrentProgressPercent`, ไม่ได้ stored)
  - ระดับเล่ม: `ConstructionCurrentValueService` = `Σ(TotalValue × %) / Σ TotalValue`
    (คอนโด TotalValue=0 → ค่าเฉลี่ยธรรมดา) · `IsUnderConstruction = raw% < 100` · ไม่มี inspection → null
- ที่เดียวที่ "แช่แข็ง" ค่าไว้คือ `CollateralEngagement.ConstructionProgressPercent` ตอนปิดงาน
  แต่มีเฉพาะเล่มที่ resolve collateral master ได้ → ใช้เป็นฐาน guard ไม่ได้
- SQL ที่คิด % เอง: vw_MisCasReport, vw_RegulatoryExport, sp_RegulatoryExport (ถ่วงน้ำหนัก) ·
  **vw_RCAS004 ใช้ MIN** (ไม่ตรงกับที่อื่น) · GetAppraisalResult (LOS) รายหลัง

### Flow เลือกเล่มเดิม (purpose 06/11)
- FE: `SearchAppraisalModal` (`GET /appraisals` status=Completed) → `GET /appraisals/{id}?include=request,documents`
  → banner `PriorAppraisalBlock` (เลขเล่ม | มูลค่า | วันที่ | ตรวจงวดครั้งที่ n จาก `/next-inspection-number`)
  — **ไม่มีข้อมูล % ก่อสร้างเลยสักเส้น**
- BE: ทุกช่องทาง (submit หน้าจอ, update request, LOS CreateRequest, block reappraisal) ผ่าน
  `PriorAppraisalSubmissionGuard` จุดเดียว — เช็คแค่ "มีอยู่ + Completed" ของเล่มที่เลือก ไม่ดูสาย
- สร้างเล่ม Progressive: `AppraisalCreationService` copy property + `ConstructionInspection.CopyForNextInspection`
  (current ของเล่มก่อน → previous ของเล่มใหม่, current รีเซ็ต) — **เล่มก่อน 100% ก็ copy ต่อเฉย ๆ**
- ฐานที่ copy: เส้นปกติใช้ **chain tip** (เล่มที่ปิดล่าสุดทั้งสาย), เส้น deferred
  (`EmitAppraisalCreationRequestedStep`) ใช้ **เล่มที่ผู้ใช้เลือก** → ไม่ตรงกัน

### เคส ตรวจงวด → ทบทวน → ตรวจงวด
- ใบทบทวน (03/09 = ReAppraisal) **ไม่ copy อะไรจากเล่มก่อน** — property สร้างจาก title ใหม่ ไม่มีค่าก่อสร้าง/inspection
- จะมี inspection ก็ต่อเมื่อผู้ประเมินติ๊ก "กำลังก่อสร้าง" เองและกรอกใหม่ และช่อง "งวดก่อน" ซ่อนอยู่ → บันทึกเป็น 0
- ใบทบทวนที่ไม่ได้กรอก: LOS ได้ **100%**, vw_RegulatoryExport ได้ **100% / ไม่กำลังก่อสร้าง**, RCAS004 หายจากรายการ
- ตรวจงวดใบถัดไป: tip = ใบทบทวน → ไม่มี inspection ให้ copy → **ใบตรวจงวดไม่มี inspection เลย** ต้องกรอกใหม่หมด
  (ราคาค่าก่อสร้างที่ทบทวนใหม่ copy มาได้ แต่ % งวดก่อนหาย)

## 2. จุดที่ควรปรับปรุง (เรียงตามความรุนแรง)
| # | เรื่อง | ผล |
|---|---|---|
| 1 🔴 | เล่มเดิมครบ 100% แล้วยังตรวจงวดซ้ำได้ (งานนี้) | งวดใหม่ previous 100 / current 0 → ถ้าไม่มีใครแก้ ปิดงานแล้ว engagement/export แช่ 0% "กำลังก่อสร้าง" |
| 2 🔴 | ใบทบทวนอาคารยังไม่เสร็จ รายงานเป็นเสร็จ 100% | LOS + ไฟล์ผู้กำกับผิด |
| 3 🟠 | ตรวจงวดหลังใบทบทวนเสีย % งวดก่อน | ผู้ประเมินต้องกรอกเอง ผิดพลาดง่าย |
| 4 🟠 | เส้น deferred ใช้เล่มที่เลือก ไม่ใช่ chain tip | เล่มใหม่ copy จากคนละฐาน ขึ้นกับว่าไปเส้นไหน |
| 5 🟠 | Server เชื่อ previous% จาก client ไม่เช็ค current ≥ previous / Σ สัดส่วน = 100 | ขัดหลัก BE zero-trust |
| 6 🟡 | TotalValue ที่ copy มาค้างค่าเก่าจนกว่าจะกดบันทึก property | มูลค่าปัจจุบันเพี้ยนชั่วคราว |
| 7 🟡 | chain tip = เล่มใดก็ได้ทุก branch (รวม appeal 12) | ฐาน copy อาจไม่ใช่สายตรง |
| 8 🟡 | RCAS004 ใช้ MIN แต่ที่อื่นถ่วงน้ำหนัก | รายงานกับหน้าจอไม่ตรงกัน |
| 9 ⚪ | comment ค้าง (`AppraisalCreationService.cs:161,696` บอกว่า "excluded" แต่ copy จริง), contract ตายใน Collateral.Contracts | — |

ที่เกี่ยวข้อง (รู้อยู่แล้ว): ใบตรวจงวดที่มีแต่ B ไม่ถึง collateral → regulatory รายงาน 100% (gotcha แยก)

## 3. ตัดสินแล้ว (ผู้ใช้ 2026-10-21)
- Q1 06 + ฐานครบ 100% → **บล็อกเด็ดขาด** (หน้าจอ + LOS 400)
- Q2 11 → **บล็อกเหมือน 06**
- Q3 ใบทบทวนสายยังไม่เสร็จ → **ติ๊ก "กำลังก่อสร้าง" ให้ + โชว์ % งวดก่อนเป็นข้อมูลอ้างอิง** ผู้ประเมินกรอก % + ราคาใหม่
  → ตรวจงวดถัดไปใช้ % ใบทบทวนเป็น "งวดก่อน" · LOS/ผู้กำกับได้ % จริงของใบทบทวน
- Q4 ฐาน = **เล่มที่ผู้ใช้เลือก** (เลิกใช้ chain tip ในเส้นปกติ)
  - **บล็อก "เล่มซ้ำ" ทั้ง 2 แบบ:** (ก) มีคำขอ 06/11 อื่นที่ยังไม่ปิดอยู่ในสายเดียวกัน (ข) เล่มที่เลือกมีงวดถัดไป (06/11) แล้ว
  - **ตรวจงวดซ้อน (ผู้ใช้ 2026-10-21): นับตาม AppraisalType Progressive — 06 กับ 11 เป็นกลุ่มเดียว ค้างได้ทีละใบต่อสาย** ·
    "ค้าง" = submit แล้ว ยังไม่ปิด/ยกเลิก ไม่นับ draft (draft สองใบ → ใบที่ submit ทีหลังโดนบล็อกตอน submit)
  - **คู่ขนาน (ผู้ใช้ 2026-10-21): ใบทบทวน/AppraisalType อื่น + ตรวจงวดวิ่งคู่กันได้ ไม่บล็อก** · "ล่าสุด/ถัดไป" ดูทั้งต้นไม้ตามวันปิดงาน
    ใช้ตัวไล่ต้นไม้เดิม `ResolveLatestInAppraisalChainQueryHandler`
  - ไม่ใช่เล่มล่าสุดแต่ไม่โดนบล็อก (= หลังเล่มที่เลือกมีแค่ใบทบทวน/อุทธรณ์) → **dialog ยืนยัน + ปุ่ม "ใช้เล่มล่าสุด X"**

## 4. แผน

### 4.0 เก็บสรุประดับอาคาร — ใช้ฟิลด์ `Summary*` เดิม (ผู้ใช้ 2026-10-21: ทำรวมในงานนี้)
- ความหมายใหม่: `Summary{Previous,Current}{ProgressPct,Value}` = **ผลสรุประดับ inspection เสมอ** ทั้ง 2 โหมด → ผู้อ่านใช้ฟิลด์เดียว ไม่ต้อง CASE
  - โหมด summary: % = ผู้ใช้คีย์ · Value = server คำนวณ `TotalValue × % ` ปัดบาท AwayFromZero (เลิกเชื่อ 0 จาก FE)
  - โหมด detail: server คำนวณทั้ง % (Σ ProportionPct × ProgressPct / 100) และ Value (Σ เงินรายแถว) ทับทุกครั้งที่บันทึก
  - จุดคำนวณ: aggregate `ConstructionInspection` ที่เดียวกับ `ComputeAllValues` (ครอบคลุม appliers + `CopyForNextInspection`)
- FE ต้องแก้ 1 จุด: mapper โหลด (`mappers.ts:168-171`) เติม summary % เข้าฟอร์ม**เฉพาะโหมด summary** · ไม่งั้น
  `hasConstructionData` เห็นค่าในโหมด detail → ล็อกปุ่มสลับวิธีในใบตรวจงวดค้าง (`ConstructionInspectionTab.tsx:242-246`)
  · ฝั่ง save ไม่ต้องแก้ (detail ไม่ส่ง summary อยู่แล้ว `mappers.ts:700-730`)
- ต้องเช็คตอนทำ: ผู้อ่าน summary* อื่นใน FE (`ConstructionSummaryForm`) แสดงผลถูกเมื่อโหมด detail มีค่า
- เพิ่ม doc comment ที่ entity ว่า Summary* = ผลสรุป inspection (คีย์เองในโหมด summary / คำนวณในโหมด detail)
- backfill แถวเก่า (detail ทั้งหมด + Value ของ summary) = one-time script `Database/Migration/Scripts/` · ไม่มี schema change · ผู้ใช้รัน `migrate`
- ระดับเล่ม **ไม่เก็บ** → view เดียว `vw_AppraisalConstructionProgress` (ถ่วง TotalValue, รวม = 0 → เฉลี่ย)
- **ย้ายผู้อ่านทุกตัวในงานนี้ (ผู้ใช้ 2026-10-21):** `ConstructionCurrentValueService` (CiAggregateSql), `vw_MisCasReport`,
  `vw_RegulatoryExport`, `sp_RegulatoryExport`, `vw_RCAS004_ConstructionInspection` → อ่าน Summary* / view เดียว
  · ไฟล์ธนาคาร: เทียบผลก่อน/หลังต้องตรงกัน (ยกเว้น RCAS004)
  · **RCAS004 เปลี่ยน MIN → ถ่วงน้ำหนัก** (ผู้ใช้ 2026-10-21) ตัวเลขรายงานจะเปลี่ยนโดยตั้งใจ

### 4.1 Query เดียว คิดสด
`GetConstructionChainStatusQuery(pickedAppraisalId)` (Appraisal.Contracts) คืน:
- `construction`: **% ล่าสุดของสาย** = เล่มที่ปิดล่าสุด (CompletedAt) ในทั้งต้นไม้ของสายที่มี inspection → คิด % ด้วย
  `ConstructionCurrentValueService` · `InProgress(pct) | Complete | NoData`
  (ดูทั้งต้นไม้ ไม่ใช่ไล่ย้อนขึ้นอย่างเดียว — เคสคู่ขนาน ใบทบทวนกับ CI#4 เป็นพี่น้องกัน ไล่ย้อนจะได้ % ของ CI#3 ที่เก่ากว่า)
- `sourceAppraisal`: เลข/วันที่/ครั้งที่ ของเล่มที่ให้ % นั้น (ไว้แสดงข้อความ)
- `laterProgressive`: เล่ม 06/11 ในทั้งต้นไม้ที่ **ปิดหลัง** เล่มที่เลือก (รวมกิ่งพี่น้อง) → บล็อก (ข)
- `openProgressiveRequest`: คำขอ 06/11 ที่ submit แล้วยังไม่ปิด/ยกเลิกในสาย → บล็อก (ก)
- `latestAppraisal`: chain tip ถ้า ≠ เล่มที่เลือก → dialog เตือน
- NoData ทั้งสาย (งวดแรกอ้างเล่มสินเชื่อใหม่) → ผ่าน

### 4.2 Guard BE (ความจริงอยู่ที่นี่)
`PriorAppraisalSubmissionGuard` purpose 06/11: Complete → ปฏิเสธ · (ก)/(ข) → ปฏิเสธ · ครอบคลุมหน้าจอ + update + LOS

### 4.3 ฐาน copy = เล่มที่เลือก
`RequestSubmittedIntegrationEventConsumer` (เส้นปกติ + retry) เลิกแทนด้วย chain tip ให้ตรงกับเส้น deferred
(appeal 12 ยังใช้ tip เพื่อ exclude บริษัท — แยกกัน ไม่แตะ)

### 4.4 หน้าจอคำขอ (FE)
1. โมดัลค้นหา: คลิกแถว → เรียก status → Complete/(ก)/(ข) = toast error ไม่ให้เลือก (แบบเดียวกับ "ยังไม่ Completed")
   - "เล่ม X ก่อสร้างครบ 100% แล้ว (ตรวจงวดครั้งที่ n เมื่อ dd/MM/yyyy)"
   - "เล่ม X มีตรวจงวดครั้งถัดไปแล้ว (เล่ม Y) — กรุณาเลือกเล่มล่าสุด"
   - "มีคำขอตรวจงวด R ที่ยังไม่ปิดในสายนี้"
2. ไม่ใช่เล่มล่าสุด → ConfirmDialog warning: "มีเล่ม X (ทบทวน, dd/MM) ใหม่กว่า" [ใช้เล่ม X] [ใช้เล่มที่เลือก]
3. Banner: เพิ่มชิป `ก่อสร้างล่าสุด 72.69% (เล่ม X ครั้งที่ n)`

### 4.5 ❌ ใบทบทวน (03/09) — ไม่ copy (ผู้ใช้ตัดสิน 2026-10-21 "ใบทบทวนไม่ต้องกอปแล้ว") · ข้อความด้านล่างเก็บไว้อ้างอิงเท่านั้น
- ผู้ใช้ 2026-10-21: ผู้ประเมินทบทวน**คีย์ % งานเองในหน้าตรวจงวดแบบเดียวกัน** → ตรวจงวดถัดไป copy จากใบทบทวนได้ (กลไกเดิม `CopyForNextInspection`)
  → dialog คงปุ่มหลัก "ใช้เล่มล่าสุด" · ถ้าใบทบทวนไม่ได้คีย์ → ตรวจงวดไม่ได้ข้อมูลก่อสร้าง (banner A2 บอกที่มา % ไว้แล้ว)
- ผู้ใช้ชี้แจงว่าต้องการ **copy construction detail ทั้งหมด** (รายการงาน/สัดส่วน/% + โครงสร้าง/ราคาอาคาร) ไม่ใช่แค่โชว์ % งวดก่อน
- ข้อเท็จจริง: copy ของ Progressive **clone ทุก property ของเล่มเดิม ไม่สน title ในคำขอ** (`AppraisalCreationService.cs:738-845`, gate `isProgressive` ที่ :147,304,333) → ทำให้ใบทบทวนได้ง่ายสุดโดยขยาย flag · `CopyForNextInspection` เลื่อน current→previous แล้วรีเซ็ต อาจไม่ใช่สิ่งที่ใบทบทวนต้องการ
- คำถามค้าง: ขอบเขต copy (ทั้งชุด / เฉพาะอาคาร+ตรวจงวดจับคู่เลขโฉนด / ทุกใบทบทวน) · % เริ่มต้น (เท่าล่าสุด / ว่าง)
- ข้อความด้านล่างคือข้อเสนอเดิม ยังไม่ final
- ตอนสร้างใบ (AppraisalCreationService): ถ้าสถานะ ณ เล่มเดิม = InProgress → property อาคารที่จับคู่ได้
  ตั้ง `IsUnderConstruction = true` + สร้าง inspection แบบ summary: previous = % ล่าสุด, current ว่าง
- จับคู่ property ด้วย **KEY = ประเภทหลักประกัน + เลขโฉนด** (กติกาเดียวกับ carry-forward เอกสาร) ·
  จับไม่ได้ → ไม่ prefill ผู้ประเมินติ๊กเอง (ข้อจำกัด: title B ไม่ถูกสร้าง property อัตโนมัติ)
- FE: บนใบทบทวนโชว์คอลัมน์ "งวดก่อน" แบบอ่านอย่างเดียวเมื่อมีค่า (ปัจจุบันซ่อนถ้า previous = 0)
- ผล: ใบทบทวนรายงาน % ถูก (แก้ #2) และตรวจงวดถัดไป copy จากใบทบทวนได้เอง (แก้ #3)

### 4.6 ⚠ มูลค่าปัจจุบันค้าง (พบระหว่างวิเคราะห์ — ยังไม่ตัดสิน แยกจากงานนี้ได้)
- ส่วนตรวจงวดอยู่ในฟอร์ม property เดียวกัน: บันทึกแท็บไหนก็ส่ง `TotalValue` ใหม่ (`mappers.ts:698`) → แก้ค่าเสื่อมหน้าอาคาร **ไม่ค้าง**
- 🔴 แก้ "มูลค่าตามวิธี" Building Cost ในหน้า pricing → ไม่แตะ `BuildingCostValue` และ inspection → `TotalValue` + เงินรายบรรทัด **ค้าง** จนกว่าจะบันทึก property ใหม่
- 🟠 กลับกัน: แก้ค่าเสื่อมหน้าอาคาร → pricing ไม่ตาม (seed เฉพาะตอนว่าง/0, `PricingAnalysisApproach.cs:122-150`)
- server ไม่เคยคำนวณ `TotalValue` เอง (รับจาก payload ทุกที่) · `SummaryCurrentValue/PreviousValue` FE ส่ง 0 → ผู้อ่านคิดจาก %
- กระทบ: `ConstructionCurrentValueService` (Decision Summary, engagement CurrentValue), เล่มตรวจงวด, MeetingMinute, vw_MisCasReport, vw_RegulatoryExport · LOS ไม่กระทบ (ส่งแค่ %) · ราคาประเมินไม่ขึ้นกับ %
- **ผู้ใช้ตัดสิน 2026-10-21: ฐาน 100% = ราคาอาคารหน้าทรัพย์สิน (คงกฎ 2026-09-24)** → แก้ pricing ไม่ทำให้ฐานค้างตามนิยาม
  (ส่วนต่างกับราคาประเมินยอมรับ) · ที่ยังควรทำ: ให้ server คำนวณ `TotalValue` เองจาก `BuildingCostValue ?? Σ PriceAfterDepreciation ปัดพัน`
  แทนรับจาก FE (zero-trust) — **รวมในงานนี้แล้ว (BE-1b)**

### 4.7 พ่วง
- แก้ comment ค้าง `AppraisalCreationService.cs:161,696`
- เรื่อง #5–#8 ไม่ทำในงานนี้ (แยกงาน ถ้าผู้ใช้ต้องการ)

## 5. Todo
- [x] Mock หน้าจอ (banner / โมดัล / dialog) — https://claude.ai/artifact/MGNRUd2TBujZ873FHmpbhj
- [ ] เตรียม worktree: BE branch ใหม่จาก origin/main + FE orca workspace (ชื่อ branch เดียวกัน)
- [ ] BE-1 สรุประดับอาคาร: aggregate คำนวณ Summary* ทั้ง 2 โหมด + doc comment + เทส domain
- [ ] BE-1b server คำนวณ `TotalValue` เอง (ผู้ใช้ 2026-10-21: รวม, **ไม่ backfill TotalValue เก่า**) = `BuildingCostValue` ที่คีย์ ?? Σ PriceAfterDepreciation ปัดพัน → ปัดบาท (mirror FE `buildingFinalCostValue`) ใน appliers ที่ดิน+อาคาร / อาคาร / คอนโด / สัญญาเช่า · ไม่เชื่อ payload · คงกติกา 0 = ไม่มีฐาน → ราคาประเมิน · FE ไม่ต้องแก้
- [ ] BE-2 backfill script (`Database/Migration/Scripts/`) + view `vw_AppraisalConstructionProgress`
- [ ] BE-3 ย้ายผู้อ่าน 5 ตัว (RCAS004 → ถ่วง) + เทียบผล Regulatory/MIS ก่อน/หลังบน dev DB (read-only)
- [ ] BE-4 `GetConstructionChainStatusQuery` (ทั้งต้นไม้) + endpoint `GET /appraisals/{id}/construction-chain-status` + เทส
- [ ] BE-5 guard 06/11 ใน `PriorAppraisalSubmissionGuard` (100% / งวดถัดไป / Progressive ค้าง) + เทส หน้าจอ+LOS
- [ ] BE-6 ฐาน copy = เล่มที่เลือก (consumer เส้นปกติ + retry) + แก้ comment ค้าง + เทส
- [ ] FE-1 mapper โหลด summary เฉพาะโหมด summary + เช็ค `ConstructionSummaryForm`
- [ ] FE-2 โมดัลค้นหา: เรียก status → toast บล็อก 3 แบบ + บรรทัดย่อย purpose/ครั้งที่
- [ ] FE-3 dialog ไม่ใช่เล่มล่าสุด + banner ชิป % / ที่มา / แถบเตือน / error จาก submit · i18n th/en
- [ ] ข้อความแจ้ง LOS (400 ใหม่) + แจ้ง DBA: script backfill + view
- [ ] QA ผ่าน subagent: build / tests / probe หน้าจริง (บล็อก write) · /code-review จน clean
- [x] ~~ใบทบทวน copy construction detail~~ — ไม่ทำ (ผู้ใช้ 2026-10-21)

## Review (2026-10-21)
- Local commits (ยังไม่ push): BE 502e095d · FE 5ae42c2c — branch `feat/appraisal/ci-prior-construction-guard`
- BE: build 0 error · unit ผ่านทุกชุด · integration 604/605 (ClosedAppraisalWriteGuardTests timeout 30s ขึ้นกับลำดับรัน ผ่านเมื่อรันเดี่ยว/บน main/ที่ path อื่น)
- FE: tsc 0 · vitest 2947 ผ่าน · eslint ไม่มีใหม่ · prettier clean
- เทียบ MIS บน dev: 105,513 แถว md5 ตรงกัน · Regulatory ระดับแถวพิสูจน์ไม่ได้ (dev ไม่มี HostCollateralLinks) ตรงกันระดับ aggregate
- ⚠ เปลี่ยนพฤติกรรม: เงินโหมดสรุปใน MIS/Regulatory อาจขยับ 1 บาท (สูตรเก่าปัด 2 ชั้น ผิดจากแอป) · previous % ต่างหลักที่ 5 — **ผู้ใช้ยอมรับแล้ว 2026-10-22**
- guard ทำงานที่ submit + LOS create/resubmit(ก่อน submit) — save draft ไม่เช็ค (เดิม)
- DBA: script 20261021120000_Backfill_ConstructionInspectionSummaryFigures → vw_AppraisalConstructionProgress → vw_MisCasReport, vw_RegulatoryExport, sp_RegulatoryExport, vw_RCAS004
- ยังไม่ได้: probe หน้าจริง (ต้องให้ผู้ใช้ migrate + รัน 7111/3000 จาก branch นี้) · /code-review · แจ้ง LOS

### แก้หลังผู้ใช้เทส (2026-10-22)
- แถบเล่มเดิมแสดง % ของเล่มที่เลือกเท่านั้น ไม่มี → "เล่มนี้ไม่มีข้อมูล %" (เดิมเอา % จากเล่มอื่นในสายมาโดยไม่บอก) · บล็อกยังดูทั้งสาย
- [x] BE selected + latestAppraisal.progressPct (be-own-pct) — unit 1036 + guard 119 + integration 15
- [x] FE ใช้ selected, ลบ source line, dialog % ของแต่ละเล่ม, ข้อความบล็อกไม่มีเลขครั้ง (fe-own-pct) — vitest 2951 · vitest.config pin VITE_API_DELAY=0 (.env ของผู้ใช้ทำเทสช้า)
- [x] gates + copy ไฟล์ไป worktree ci-company-and-fee — FE 2980 · BE Appraisal 1093 / Request 413 / Workflow 1155 / integration 610/611 (Puppeteer dispose NRE = infra)

### หลัง submit: ครั้งที่ + % เล่มเดิมหาย (ผู้ใช้ 2026-10-24)
- [x] BE: `GET /requests/{id}` มี `inspectionNumber` (pinned) = `Appraisals.InspectionNumber` ของเล่มคำขอนี้ (ไม่นับยกเลิก, ล่าสุด) — ดึงเฉพาะ 06/11 ที่ submit แล้ว ผ่าน `GetInspectionNumberByRequestQuery` (Appraisal.Contracts). ส่งถึง LOS route ด้วย (เพิ่มฟิลด์อย่างเดียว)
- [x] FE: หลัง submit/อ่านอย่างเดียว แถบเล่มเดิมโชว์ % ของเล่มเดิมเอง + ครั้งที่จาก `inspectionNumber` (null = ไม่โชว์) · ตัด blockReason/latestAppraisal ทิ้ง (คำขอนี้เองนับเป็นคำขอค้าง)
- [x] gate: BE build 0 · Request 420 · Appraisal 1036 · integration 612/613 (ClosedAppraisalWriteGuard flake, รันซ้ำผ่าน) · FE tsc 0 · vitest 2953 · copy ไป ci-company-and-fee แล้ว (vitest 2986, Appraisal 1093)
- [x] probe หน้าจริง (บล็อก non-GET): REQ-105517 ครั้งที่ 3 · 50% / REQ-105515 ครั้งที่ 11 · 10% / REQ-105622 ครั้งที่ 1 · ไม่มีข้อมูล % — ตรง DB + view · draft ที่ไม่ถูกบล็อกไม่มีใน dev data
