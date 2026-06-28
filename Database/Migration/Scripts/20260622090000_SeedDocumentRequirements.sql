-- =============================================================================
-- Seed parameter.DocumentRequirements (by collateral type + by purpose)
--
-- Runs AFTER 20260621090000_SeedDocumentTypesAndPatchCodes.sql, which seeds
-- parameter.DocumentTypes with the D0xx codes (and clears any legacy rows).
--
-- Two halves, from the bank's Document Checklist matrix:
--   * Collateral-type rows: PropertyTypeCode = 33-code CollateralType ('01'..'33'),
--     PurposeCode = NULL. This is the scheme GetRequestDocumentChecklist passes
--     (RequestTitle.CollateralType).
--   * Purpose rows: PropertyTypeCode = NULL, PurposeCode = AppraisalPurpose code.
--     Documents are "pulled from CAS" (previous report) at request time.
--
-- DocumentTypeId is resolved by joining parameter.DocumentTypes on the D-code.
-- Idempotent: only seeds when the table is empty.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM parameter.DocumentRequirements)
BEGIN
    DECLARE @req TABLE
    (
        PropertyTypeCode nvarchar(10)  NULL,
        PurposeCode      nvarchar(10)  NULL,
        DCode            nvarchar(20)  NOT NULL,
        IsRequired       bit           NOT NULL,
        Notes            nvarchar(500) NULL
    );

    -- -------------------------------------------------------------------------
    -- Collateral-type rows (PropertyTypeCode = 33-code, PurposeCode = NULL)
    -- -------------------------------------------------------------------------
    INSERT INTO @req (PropertyTypeCode, PurposeCode, DCode, IsRequired, Notes) VALUES
        -- 01 Land
        (N'01', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        -- 02 Land + Building
        (N'02', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        -- 04 Allocated land (ที่ดินจัดสรร)
        (N'04', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        (N'04', NULL, N'D022', 1, N'ผังโครงการ'),
        -- 06 Building (blueprint)
        (N'06', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        (N'06', NULL, N'D020', 1, N'แบบแปลนอาคาร'),
        -- 07 Building (whole project)
        (N'07', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        (N'07', NULL, N'D020', 1, N'แบบแปลนอาคาร'),
        (N'07', NULL, N'D022', 1, N'ผังโครงการ'),
        -- 08 Condominium unit
        (N'08', NULL, N'D013', 1, N'สำเนาเอกสารสิทธิ์'),
        -- 10 Vehicle
        (N'10', NULL, N'D028', 1, N'คู่มือทะเบียนรถยนต์ (เล่มทะเบียน)'),
        -- 11 Machinery (all optional; condition-dependent)
        (N'11', NULL, N'D029', 0, N'ทะเบียนเครื่องจักร (กรณีติดตั้งแล้วและจดทะเบียน)'),
        (N'11', NULL, N'D031', 0, N'รายละเอียดเครื่องจักร (กรณีติดตั้งแล้วและไม่จดทะเบียน)'),
        (N'11', NULL, N'D030', 0, N'ใบสั่งซื้อ (Invoice) (กรณีอยู่ระหว่างจัดซื้อ)'),
        -- 12 Boat
        (N'12', NULL, N'D032', 1, N'ทะเบียนเรือ'),
        (N'12', NULL, N'D033', 1, N'ทะเบียนประจำเรือ'),
        (N'12', NULL, N'D027', 1, N'แผนที่สังเขป'),
        -- 32 BlockLand (ที่ดินพร้อมสิ่งปลูกสร้าง - Block)
        (N'32', NULL, N'D020', 1, N'แบบแปลนอาคาร'),
        (N'32', NULL, N'D022', 1, N'ผังโครงการ'),
        -- 33 BlockCondo (คอนโด - Block)
        (N'33', NULL, N'D022', 1, N'ผังโครงการ');

    -- -------------------------------------------------------------------------
    -- Purpose rows (PropertyTypeCode = NULL, PurposeCode = AppraisalPurpose code)
    -- Documents pulled from CAS (previous report).
    -- -------------------------------------------------------------------------
    INSERT INTO @req (PropertyTypeCode, PurposeCode, DCode, IsRequired, Notes) VALUES
        -- 03 Review collateral value (ทบทวนจากสินเชื่อ)
        (NULL, N'03', N'D036', 1, N'ใบสรุปประเมินเดิม (ดึงจาก CAS)'),
        -- 05 Property awaiting sale (NPA)
        (NULL, N'05', N'D036', 1, N'ใบสรุปประเมินเดิม (ดึงจาก CAS)'),
        (NULL, N'05', N'D013', 1, N'เอกสารสิทธิ์ (ดึงจาก CAS)'),
        (NULL, N'05', N'D014', 0, N'สัญญาซื้อขาย'),
        -- 06 Inspect construction work (ตรวจสอบงาน)
        (NULL, N'06', N'D036', 1, N'ใบสรุปประเมินก่อนหน้า (ดึงจาก CAS)'),
        -- 11 100% construction inspection (ตรวจสอบงาน)
        (NULL, N'11', N'D036', 1, N'ใบสรุปประเมินก่อนหน้า (ดึงจาก CAS)'),
        -- 12 Credit limit (appeal) (อุธรณ์)
        (NULL, N'12', N'D036', 1, N'ใบสรุปประเมินก่อนหน้า (ดึงจาก CAS)');

    INSERT INTO parameter.DocumentRequirements
        ([Id], [DocumentTypeId], [PropertyTypeCode], [PurposeCode], [IsRequired], [IsActive], [Notes], [CreatedAt], [CreatedBy])
    SELECT NEWID(), dt.[Id], r.PropertyTypeCode, r.PurposeCode, r.IsRequired, 1, r.Notes, SYSUTCDATETIME(), N'SYSTEM'
    FROM @req r
    INNER JOIN parameter.DocumentTypes dt ON dt.[Code] = r.DCode;
END
GO
