-- ============================================================
-- Replace CollateralType parameter group with the full 33-code set
-- ============================================================

DELETE FROM parameter.Parameters WHERE [group] = N'CollateralType';
GO

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    -- 01 Land
    (N'CollateralType', N'TH', N'EN', N'01', N'Land', 1, 1),
    (N'CollateralType', N'TH', N'TH', N'01', N'ที่ดิน', 1, 1),

    -- 02 Land with buildings
    (N'CollateralType', N'TH', N'EN', N'02', N'Land with buildings', 1, 2),
    (N'CollateralType', N'TH', N'TH', N'02', N'ที่ดินพร้อมสิ่งปลูกสร้าง', 1, 2),

    -- 03 Land with buildings (blueprint)
    (N'CollateralType', N'TH', N'EN', N'03', N'Land with buildings (blueprint)', 1, 3),
    (N'CollateralType', N'TH', N'TH', N'03', N'ที่ดินพร้อมสิ่งปลูกสร้าง (แบบแปลน)', 1, 3),

    -- 04 Land allocation (whole project)
    (N'CollateralType', N'TH', N'EN', N'04', N'Land allocation (whole project)', 1, 4),
    (N'CollateralType', N'TH', N'TH', N'04', N'ที่ดินจัดสรร (ทั้งโครงการ)', 1, 4),

    -- 05 Buildings
    (N'CollateralType', N'TH', N'EN', N'05', N'Buildings', 1, 5),
    (N'CollateralType', N'TH', N'TH', N'05', N'สิ่งปลูกสร้าง', 1, 5),

    -- 06 Building (blueprint)
    (N'CollateralType', N'TH', N'EN', N'06', N'Building (blueprint)', 1, 6),
    (N'CollateralType', N'TH', N'TH', N'06', N'สิ่งปลูกสร้าง (แบบแปลน)', 1, 6),

    -- 07 Building (whole project)
    (N'CollateralType', N'TH', N'EN', N'07', N'Building (whole project)', 1, 7),
    (N'CollateralType', N'TH', N'TH', N'07', N'สิ่งปลูกสร้าง (ทั้งโครงการ)', 1, 7),

    -- 08 Apartment / Condominium unit
    (N'CollateralType', N'TH', N'EN', N'08', N'Apartment', 1, 8),
    (N'CollateralType', N'TH', N'TH', N'08', N'อาคารชุด (ห้องชุด)', 1, 8),

    -- 09 Leasehold rights, real estate
    (N'CollateralType', N'TH', N'EN', N'09', N'Leasehold rights, real estate', 1, 9),
    (N'CollateralType', N'TH', N'TH', N'09', N'สิทธิการเช่าอสังหาริมทรัพย์', 1, 9),

    -- 10 Car
    (N'CollateralType', N'TH', N'EN', N'10', N'Car', 1, 10),
    (N'CollateralType', N'TH', N'TH', N'10', N'รถยนต์', 1, 10),

    -- 11 Machinery
    (N'CollateralType', N'TH', N'EN', N'11', N'Machinery', 1, 11),
    (N'CollateralType', N'TH', N'TH', N'11', N'เครื่องจักร', 1, 11),

    -- 12 Ship
    (N'CollateralType', N'TH', N'EN', N'12', N'Ship', 1, 12),
    (N'CollateralType', N'TH', N'TH', N'12', N'เรือ', 1, 12),

    -- 13 Land (Part 1)
    (N'CollateralType', N'TH', N'EN', N'13', N'Land (Part 1)', 1, 13),
    (N'CollateralType', N'TH', N'TH', N'13', N'ที่ดิน (ส่วนที่ 1)', 1, 13),

    -- 14 Land (Part 2)
    (N'CollateralType', N'TH', N'EN', N'14', N'Land (Part 2)', 1, 14),
    (N'CollateralType', N'TH', N'TH', N'14', N'ที่ดิน (ส่วนที่ 2)', 1, 14),

    -- 15 Building (Part 1)
    (N'CollateralType', N'TH', N'EN', N'15', N'Building (Part 1)', 1, 15),
    (N'CollateralType', N'TH', N'TH', N'15', N'สิ่งปลูกสร้าง (ส่วนที่ 1)', 1, 15),

    -- 16 Building (Part 2)
    (N'CollateralType', N'TH', N'EN', N'16', N'Building (Part 2)', 1, 16),
    (N'CollateralType', N'TH', N'TH', N'16', N'สิ่งปลูกสร้าง (ส่วนที่ 2)', 1, 16),

    -- 17 Land (Part 2) alt
    (N'CollateralType', N'TH', N'EN', N'17', N'Land (Part 2)', 1, 17),
    (N'CollateralType', N'TH', N'TH', N'17', N'ที่ดิน (ส่วนที่ 2)', 1, 17),

    -- 18 Building (Part 2) alt
    (N'CollateralType', N'TH', N'EN', N'18', N'Building (Part 2)', 1, 18),
    (N'CollateralType', N'TH', N'TH', N'18', N'สิ่งปลูกสร้าง (ส่วนที่ 2)', 1, 18),

    -- 19 Land (Group 1)
    (N'CollateralType', N'TH', N'EN', N'19', N'Land (Group 1)', 1, 19),
    (N'CollateralType', N'TH', N'TH', N'19', N'ที่ดิน (กลุ่มที่ 1)', 1, 19),

    -- 20 Building (Group 1)
    (N'CollateralType', N'TH', N'EN', N'20', N'Building (Group 1)', 1, 20),
    (N'CollateralType', N'TH', N'TH', N'20', N'สิ่งปลูกสร้าง (กลุ่มที่ 1)', 1, 20),

    -- 21 Land (Group 2)
    (N'CollateralType', N'TH', N'EN', N'21', N'Land (Group 2)', 1, 21),
    (N'CollateralType', N'TH', N'TH', N'21', N'ที่ดิน (กลุ่มที่ 2)', 1, 21),

    -- 22 Building (Group 2)
    (N'CollateralType', N'TH', N'EN', N'22', N'Building (Group 2)', 1, 22),
    (N'CollateralType', N'TH', N'TH', N'22', N'สิ่งปลูกสร้าง (กลุ่มที่ 2)', 1, 22),

    -- 23 Land with buildings (Group 1)
    (N'CollateralType', N'TH', N'EN', N'23', N'Land with buildings (Group 1)', 1, 23),
    (N'CollateralType', N'TH', N'TH', N'23', N'ที่ดินพร้อมสิ่งปลูกสร้าง (กลุ่มที่ 1)', 1, 23),

    -- 24 Land with buildings (Group 2)
    (N'CollateralType', N'TH', N'EN', N'24', N'Land with buildings (Group 2)', 1, 24),
    (N'CollateralType', N'TH', N'TH', N'24', N'ที่ดินพร้อมสิ่งปลูกสร้าง (กลุ่มที่ 2)', 1, 24),

    -- 25 Leasehold rights (land with buildings)
    (N'CollateralType', N'TH', N'EN', N'25', N'Leasehold rights (land with buildings)', 1, 25),
    (N'CollateralType', N'TH', N'TH', N'25', N'สิทธิการเช่า (ที่ดินพร้อมสิ่งปลูกสร้าง)', 1, 25),

    -- 26 Land (Group 3)
    (N'CollateralType', N'TH', N'EN', N'26', N'Land (Group 3)', 1, 26),
    (N'CollateralType', N'TH', N'TH', N'26', N'ที่ดิน (กลุ่มที่ 3)', 1, 26),

    -- 27 Land (Group 4)
    (N'CollateralType', N'TH', N'EN', N'27', N'Land (Group 4)', 1, 27),
    (N'CollateralType', N'TH', N'TH', N'27', N'ที่ดิน (กลุ่มที่ 4)', 1, 27),

    -- 28 Leasehold rights (condominium)
    (N'CollateralType', N'TH', N'EN', N'28', N'Leasehold rights (condominium)', 1, 28),
    (N'CollateralType', N'TH', N'TH', N'28', N'สิทธิการเช่า (อาคารชุด)', 1, 28),

    -- 29 Land lease rights
    (N'CollateralType', N'TH', N'EN', N'29', N'Land lease rights', 1, 29),
    (N'CollateralType', N'TH', N'TH', N'29', N'สิทธิการเช่าที่ดิน', 1, 29),

    -- 30 Leasehold rights
    (N'CollateralType', N'TH', N'EN', N'30', N'Leasehold rights', 1, 30),
    (N'CollateralType', N'TH', N'TH', N'30', N'สิทธิการเช่า', 1, 30),

    -- 31 Lease rights for space within shopping center
    (N'CollateralType', N'TH', N'EN', N'31', N'Lease rights for space within shopping center', 1, 31),
    (N'CollateralType', N'TH', N'TH', N'31', N'สิทธิการเช่าพื้นที่ในศูนย์การค้า', 1, 31),

    -- 32 Land with buildings (BlockLand)
    (N'CollateralType', N'TH', N'EN', N'32', N'Land with buildings (BlockLand)', 1, 32),
    (N'CollateralType', N'TH', N'TH', N'32', N'ที่ดินพร้อมสิ่งปลูกสร้าง (โครงการบ้านจัดสรร)', 1, 32),

    -- 33 Condominium (BlockCondo)
    (N'CollateralType', N'TH', N'EN', N'33', N'Condominium (BlockCondo)', 1, 33),
    (N'CollateralType', N'TH', N'TH', N'33', N'อาคารชุด (โครงการอาคารชุด)', 1, 33);
GO
