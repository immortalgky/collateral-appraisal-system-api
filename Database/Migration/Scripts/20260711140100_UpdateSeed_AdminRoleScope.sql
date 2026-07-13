-- ============================================================
-- CA-497: Backfill Scope on the 'Admin' role
-- Schema: auth
-- The Admin role (auth.AspNetRoles) was seeded with Scope = NULL. The
-- Edit-Roles modal treats a NULL Scope as "not assignable", which meant
-- the Admin role could never be deselected/removed from a user via that
-- UI. AuthDataSeed.SeedAdminRoleAsync now sets Scope = 'Bank' for fresh
-- DBs; this script backfills existing data.
-- Idempotent: only touches the row when Scope is still NULL/empty.
-- ============================================================

UPDATE auth.AspNetRoles
SET Scope = N'Bank'
WHERE NormalizedName = N'ADMIN'
  AND (Scope IS NULL OR Scope = N'');

PRINT 'Backfilled Scope = Bank for the Admin role';
GO
