-- ============================================================
-- Add parameter group 'Obligation' code 06, ภาระจำยอม (Servitude): the code the appraiser picks for an easement
-- obligation. Code 03/04/05/06/99 are the "other obligations" of ภาระผูกพันอื่นๆ; the book prints every picked code
-- except 01 (none) and 02 (mortgage), so no code change is needed to show it.
--
--   06  TH ภาระจำยอม   EN Servitude   (placed before 99 in seqno order)
--   99  seqno 6 -> 7                  only where 99 still holds the seeded seqno 6, so an admin's own ordering is kept
--
-- Idempotent: a row is inserted only when (group, country, language, code) is not there yet; the 99 move matches only
-- the seeded value. The group is seeded only by 20260317002600_SeedData_GeneralParameter.sql (no C# seeder), and a fresh
-- database runs that script first, so this one then adds the row.
--
-- NOTE: CachedParameterRepository is an in-memory cache with a 1 hour absolute expiry; only writes made through the
-- repository evict it, so restart the API after running this script or each instance serves the old list until it expires.
-- ============================================================
SET NOCOUNT ON;

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
SELECT N'Obligation', N'TH', v.lang, N'06', v.descr, 1, 6
FROM  (VALUES (N'EN', N'Servitude'), (N'TH', N'ภาระจำยอม')) v(lang, descr)
WHERE NOT EXISTS (SELECT 1 FROM parameter.Parameters p
                  WHERE p.[group] = N'Obligation' AND p.[country] = N'TH' AND p.[language] = v.lang AND p.[code] = N'06');

SELECT @@ROWCOUNT AS ObligationServitudeRowsInserted;

UPDATE parameter.Parameters
SET    [seqno] = 7
WHERE  [group] = N'Obligation' AND [code] = N'99' AND [seqno] = 6
  AND  EXISTS (SELECT 1 FROM parameter.Parameters x WHERE x.[group] = N'Obligation' AND x.[code] = N'06' AND x.[seqno] = 6);

SELECT @@ROWCOUNT AS ObligationOtherMovedAfterServitude;
GO
