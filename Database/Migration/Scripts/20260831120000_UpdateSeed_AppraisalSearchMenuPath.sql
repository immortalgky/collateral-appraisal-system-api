-- ============================================================
-- Repair auth.MenuItems.Path for the appraisal Search menu item
-- Schema: auth
--
-- WHY, precisely -- an earlier draft of this script told the story backwards and
-- it is worth not repeating: MenuSeedData.cs has carried '/appraisals/search'
-- for main.appraisal.search in EVERY commit on main since e61712f3. The seeder
-- has never emitted '/appraisals/list'. So a database seeded from main is
-- already correct and this script is a no-op there, by design.
--
-- It exists because the local development database was nevertheless found
-- holding '/appraisals/list'. auth.MenuItems has no audit columns, so the origin
-- cannot be recovered -- a hand edit in /admin/menus, or a deploy from a branch
-- that carried the older value, are both plausible. Any environment that drifted
-- the same way is silently broken in three places, because all three compare
-- location.pathname to the menu href exactly and the FE redirect moves the
-- browser to /appraisals/search:
--
--   * Sidebar.tsx:43 -- the menu item the user just clicked does not light up
--   * Sidebar.tsx:82 -- the parent "Appraisal" group does not read as active
--   * useBreadcrumb.ts:65 -- the trail is reset only for a path found in the
--     menu tree, so an unknown path leaves the crumb from the record the user
--     just left ("REQ-105517-2569") on screen after they return to the list.
--     That is the symptom that surfaced this.
--
-- Guarded on the old value so an administrator who has already corrected it is
-- not overwritten, and reports its row count so the migration log distinguishes
-- "repaired a drifted database" from "found nothing to do", which a silent
-- UPDATE cannot.
--
-- ⚠ Takes effect on a running node only after recycling: MenuTreeCache holds the
--    whole tree in IMemoryCache with no expiry and invalidates per-process, so a
--    DB-only change leaves warm API nodes serving the old path.
-- ============================================================

UPDATE auth.MenuItems
SET    Path = '/appraisals/search'
WHERE  ItemKey = 'main.appraisal.search'
  AND  Path = '/appraisals/list';

IF @@ROWCOUNT = 0
    PRINT 'main.appraisal.search already points at /appraisals/search - nothing to repair.';
ELSE
    PRINT 'main.appraisal.search repaired: /appraisals/list -> /appraisals/search.';
