-- ============================================================
-- Seed fix: Correct "Fire Protection System" label in Building
-- Management System group
-- Schema: parameter
-- CA-339: parameter.ConstructionWorkGroups 'BuildingManagement' item
--         Code = 'ProtectionSystem' was seeded
--         (20260322205700_SeedData_ConstructionWorkGroups.sql) with the
--         wrong label (NameEn = 'Protection System',
--         NameTh = 'ระบบป้องกัน'). The authoritative parameter.Parameters
--         group 'BuildingManagementSystem'
--         (20260317002600_SeedData_GeneralParameter.sql) item 03 is
--         "Fire Protection System". Correct the display labels only —
--         Code and Id are left unchanged so existing per-appraisal
--         ConstructionWorkDetails snapshots (which reference the item by
--         Id/name at creation time) are not disturbed.
-- ============================================================

DECLARE @BuildingManagementId UNIQUEIDENTIFIER;
SELECT @BuildingManagementId = Id FROM parameter.ConstructionWorkGroups WHERE Code = N'BuildingManagement';

IF @BuildingManagementId IS NOT NULL
   AND EXISTS (
       SELECT 1 FROM parameter.ConstructionWorkItems
       WHERE ConstructionWorkGroupId = @BuildingManagementId AND Code = N'ProtectionSystem'
   )
BEGIN
    UPDATE parameter.ConstructionWorkItems
    SET NameEn = N'Fire Protection System',
        NameTh = N'ระบบป้องกันอัคคีภัย'
    WHERE ConstructionWorkGroupId = @BuildingManagementId AND Code = N'ProtectionSystem';

    PRINT 'Corrected Fire Protection System label in Building Management System group';
END
ELSE
BEGIN
    PRINT 'ProtectionSystem construction work item not found (or Building Management group missing), skipping...';
END
GO
