-- =============================================================================
--  UPDATE APPRAISAL SEARCH MENU URL   /appraisals/search  ->  /appraisals/list
--  Repoints the top-level "Appraisal Search" menu item. Idempotent (targets the
--  item by ItemKey and only updates when the path still differs).
--  SET QUOTED_IDENTIFIER ON is REQUIRED — auth.MenuItems has a filtered index.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

UPDATE auth.MenuItems
SET Path = N'/appraisals/list'
WHERE ItemKey = N'main.appraisal.search'
  AND Path = N'/appraisals/search';

-- Verify — expect path = /appraisals/list
SELECT ItemKey, Path
FROM auth.MenuItems
WHERE ItemKey = N'main.appraisal.search';
