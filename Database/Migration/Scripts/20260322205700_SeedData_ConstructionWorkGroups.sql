-- ============================================================
-- Seed: Construction Work Groups and Items
-- Schema: parameter
-- ============================================================

-- Skip if already seeded
IF EXISTS (SELECT 1 FROM parameter.ConstructionWorkGroups)
BEGIN
    PRINT 'Construction work groups already seeded, skipping...';
    RETURN;
END

-- ----------------------------------------
-- Group 1: Building Structure
-- ----------------------------------------
DECLARE @BuildingStructureId UNIQUEIDENTIFIER = NEWID();
INSERT INTO parameter.ConstructionWorkGroups (Id, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES (@BuildingStructureId, N'BuildingStructure', N'งานโครงสร้าง', N'Building Structure', 1, 1);

INSERT INTO parameter.ConstructionWorkItems (Id, ConstructionWorkGroupId, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES
    (NEWID(), @BuildingStructureId, N'Pillar',       N'เสา',           N'Pillar',        1, 1),
    (NEWID(), @BuildingStructureId, N'Floor',        N'พื้น',          N'Floor',         2, 1),
    (NEWID(), @BuildingStructureId, N'Stair',        N'บันได',         N'Stair',         3, 1),
    (NEWID(), @BuildingStructureId, N'RooftopFloor', N'พื้นดาดฟ้า',    N'Rooftop Floor', 4, 1);

-- ----------------------------------------
-- Group 2: Architecture
-- ----------------------------------------
DECLARE @ArchitectureId UNIQUEIDENTIFIER = NEWID();
INSERT INTO parameter.ConstructionWorkGroups (Id, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES (@ArchitectureId, N'Architecture', N'งานสถาปัตยกรรม', N'Architecture', 2, 1);

INSERT INTO parameter.ConstructionWorkItems (Id, ConstructionWorkGroupId, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES
    (NEWID(), @ArchitectureId, N'FloorSurface',     N'ผิวพื้น',         N'Floor Surface',    1, 1),
    (NEWID(), @ArchitectureId, N'Wall',             N'ผนัง',            N'Wall',             2, 1),
    (NEWID(), @ArchitectureId, N'Ceiling',          N'ฝ้าเพดาน',        N'Ceiling',          3, 1),
    (NEWID(), @ArchitectureId, N'DoorsAndWindows',  N'ประตู-หน้าต่าง',   N'Doors & Windows',  4, 1),
    (NEWID(), @ArchitectureId, N'SanitaryWare',     N'สุขภัณฑ์',        N'Sanitary Ware',    5, 1),
    (NEWID(), @ArchitectureId, N'Painting',         N'สี',              N'Painting',         6, 1),
    (NEWID(), @ArchitectureId, N'ArchStair',        N'บันได',           N'Stair',            7, 1),
    (NEWID(), @ArchitectureId, N'Miscellaneous',    N'เบ็ดเตล็ด',       N'Miscellaneous',    8, 1);

-- ----------------------------------------
-- Group 3: Building Management System
-- ----------------------------------------
DECLARE @BuildingManagementId UNIQUEIDENTIFIER = NEWID();
INSERT INTO parameter.ConstructionWorkGroups (Id, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES (@BuildingManagementId, N'BuildingManagement', N'งานระบบ', N'Building Management System', 3, 1);

INSERT INTO parameter.ConstructionWorkItems (Id, ConstructionWorkGroupId, Code, NameTh, NameEn, DisplayOrder, IsActive)
VALUES
    (NEWID(), @BuildingManagementId, N'ElectricalSystem',  N'ระบบไฟฟ้า',       N'Electrical System',  1, 1),
    (NEWID(), @BuildingManagementId, N'SanitarySystem',    N'ระบบสุขาภิบาล',    N'Sanitary System',    2, 1),
    (NEWID(), @BuildingManagementId, N'ProtectionSystem',  N'ระบบป้องกัน',      N'Protection System',  3, 1);

PRINT 'Seeded 3 construction work groups with 15 work items';
GO
