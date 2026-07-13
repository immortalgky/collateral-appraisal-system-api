-- ============================================================
-- Remove legacy "User List" and "Role Assignment" menu items
-- Schema: auth
-- These two children of "User Management" were part of the original
-- menu seed (MenuSeedData.cs) but have since been replaced by the
-- current "Users" (main.user-management.users) and "Roles"
-- (main.user-management.roles) entries. Because AuthDataSeed.UpsertTreeAsync
-- is INSERT-ONLY, the old rows linger on already-seeded DBs. This script
-- removes them.
-- Note: auth.MenuItems' PK column is MenuItemId (not Id).
-- Dependents (MenuItemTranslations, ActivityMenuOverrides) are cleared
-- explicitly so the script also works on any DB whose FKs were not created
-- with ON DELETE CASCADE.
-- Idempotent: re-running is a no-op once the rows are gone.
-- ============================================================

DECLARE @Ids TABLE (MenuItemId UNIQUEIDENTIFIER NOT NULL);
INSERT INTO @Ids (MenuItemId)
SELECT m.MenuItemId
FROM auth.MenuItems m
WHERE m.ItemKey IN (N'main.user-management.user-list', N'main.user-management.role-assignment');

DELETE FROM auth.ActivityMenuOverrides
WHERE MenuItemId IN (SELECT MenuItemId FROM @Ids);

DELETE FROM auth.MenuItemTranslations
WHERE MenuItemId IN (SELECT MenuItemId FROM @Ids);

DELETE FROM auth.MenuItems
WHERE MenuItemId IN (SELECT MenuItemId FROM @Ids);

PRINT 'Removed legacy User Management menu items (User List, Role Assignment)';
GO
