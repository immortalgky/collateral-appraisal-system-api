-- ============================================================
-- Repair auth.MenuItems.Path for the appraisal Search menu item
-- Schema: auth
--
-- WHERE THE DRIFT CAME FROM -- worth recording, because two earlier drafts of
-- this header guessed and both guessed wrong.
--
-- main has carried '/appraisals/search' for main.appraisal.search in every
-- commit of MenuSeedData.cs. Exactly two refs in this repository have ever
-- emitted '/appraisals/list' for that key:
--
--   feat/appraisal-permission-overhaul-and-config-menus  (311c68cf, 2026-08-03)
--   wip/snapshot-20260803                                (19ac067f, its snapshot)
--
-- That branch set MenuSeedData.cs:100 to '/appraisals/list' AND shipped
-- 20260803120000_UpdateSeed_AppraisalMenuPermissions.sql, whose part 4 is the
-- exact inverse of the UPDATE below. Its PR (api#357) was CLOSED without
-- merging. It did nonetheless run against at least one database: the local
-- development database journals 20260802120000_SeedData_ConfigMaintenanceMenus.sql,
-- a script that exists only on that branch. 20260803120000 is journaled nowhere,
-- so the inverse UPDATE never ran; the row was seeded from that branch's
-- MenuSeedData.cs instead, and AuthDataSeed.UpsertTreeAsync is INSERT-ONLY, so
-- switching back to main never repaired it.
--
-- Conclusion: the blast radius is any database that was seeded while checked out
-- on that branch. UAT and production, which only ever ran merged code, should
-- already read '/appraisals/search' and this script is a guarded no-op there.
--
-- ⚠ IF THAT BRANCH IS EVER REVIVED, drop or invert its part 4 first. DbUp
--    journals by script name, not by wall-clock order, so merging it after this
--    script has been journaled would run its inverse UPDATE, flip the path back,
--    and this repair -- already recorded as applied -- would never run again.
--    Its MenuSeedData.cs would also reintroduce the bad value on fresh installs.
--
-- WHAT BREAKS WHEN IT DRIFTS -- three symptoms, one cause. All three compare
-- location.pathname to the menu href exactly, and the FE redirect at
-- router.tsx:337 moves the browser to /appraisals/search:
--
--   * Sidebar.tsx:43      -- the item the user just clicked does not light up
--   * Sidebar.tsx:82      -- the parent "Appraisal" group does not read active
--   * useBreadcrumb.ts:65 -- the trail is reset only for a path found in the
--     menu tree, so an unknown path leaves the crumb from the record the user
--     just left ("REQ-105517-2569") on screen after they return to the list.
--     That is the symptom that surfaced this.
--
-- Guarded on the old value so an administrator who already corrected it by hand
-- is not overwritten, and reports its row count so the migration log can tell
-- "repaired a drifted database" from "found nothing to do".
--
-- ⚠ Takes effect on a running node only after recycling: MenuTreeCache holds the
--    whole tree in IMemoryCache with no expiry and invalidates per-process, so a
--    DB-only change leaves warm API nodes serving the old path.
-- ============================================================

-- auth.MenuItems carries a FILTERED index -- IX_MenuItems_Scope_Path,
-- WHERE [Path] IS NOT NULL -- and SQL Server refuses INSERT/UPDATE/DELETE on
-- such a table unless QUOTED_IDENTIFIER and ANSI_NULLS are ON. DbUp already
-- sets them, but sqlcmd defaults QUOTED_IDENTIFIER OFF and
-- deploy/Invoke-SqlDeploy.ps1 does not pass -I, so state them explicitly:
-- production applies a generated plain-SQL bundle by hand (deploy/README.md)
-- and must not depend on the caller's session defaults, nor on an earlier
-- script in the same bundle having leaked them into the session.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

UPDATE auth.MenuItems
SET    Path      = '/appraisals/search',
       -- Stamped so the next person investigating menu drift can tell a repair
       -- from an admin-UI edit instead of repeating this analysis. Matches
       -- 20260729120000_UpdateSeed_RegroupMainMenu.sql, which also stamps
       -- UpdatedAt only -- UpdatedBy is NVARCHAR(10) and holds a user code,
       -- which a migration does not have.
       UpdatedAt = SYSDATETIME()
WHERE  ItemKey = 'main.appraisal.search'
  AND  Path = '/appraisals/list';

IF @@ROWCOUNT = 0
    PRINT 'main.appraisal.search already points at /appraisals/search - nothing to repair.';
ELSE
    PRINT 'main.appraisal.search repaired: /appraisals/list -> /appraisals/search.';
