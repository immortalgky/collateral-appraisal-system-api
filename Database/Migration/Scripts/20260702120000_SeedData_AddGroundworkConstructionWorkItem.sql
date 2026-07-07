-- ============================================================
-- Seed: Add missing "Groundwork" work item to Building Structure group
-- Schema: parameter
-- CA-338: parameter.ConstructionWorkGroups 'BuildingStructure' was seeded
--         (20260322205700_SeedData_ConstructionWorkGroups.sql) with only 4
--         items (Pillar, Floor, Stair, RooftopFloor). The authoritative
--         parameter.Parameters group 'BuildingStructure'
--         (20260317002600_SeedData_GeneralParameter.sql) has 5 items:
--         Groundwork (01), Pillar (02), Floor (03), Stair (04),
--         Rooftop Floor (05). Add the missing Groundwork item as the
--         first item (DisplayOrder 1) to match that order.
-- ============================================================

DECLARE @BuildingStructureId UNIQUEIDENTIFIER;
SELECT @BuildingStructureId = Id FROM parameter.ConstructionWorkGroups WHERE Code = N'BuildingStructure';

IF @BuildingStructureId IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM parameter.ConstructionWorkItems
       WHERE ConstructionWorkGroupId = @BuildingStructureId AND Code = N'Groundwork'
   )
BEGIN
    -- Shift existing items down to make room for Groundwork at position 1
    UPDATE parameter.ConstructionWorkItems
    SET DisplayOrder = DisplayOrder + 1
    WHERE ConstructionWorkGroupId = @BuildingStructureId;

    INSERT INTO parameter.ConstructionWorkItems (Id, ConstructionWorkGroupId, Code, NameTh, NameEn, DisplayOrder, IsActive)
    VALUES (NEWID(), @BuildingStructureId, N'Groundwork', N'ฐานราก', N'Groundwork', 1, 1);

    PRINT 'Added Groundwork construction work item to Building Structure group';
END
ELSE
BEGIN
    PRINT 'Groundwork construction work item already present (or Building Structure group missing), skipping...';
END
GO
