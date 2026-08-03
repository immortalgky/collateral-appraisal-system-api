-- ============================================================
-- Rename the seeded external appraisal companies from English to Thai
-- Schema: auth
--
-- auth.Companies.Name has always held the English name from the bank's ExtAppraisalCompany
-- sheet, while the Thai name was seeded alongside it into NameLocal. The business wants Thai
-- everywhere, and ~12 views project comp.Name (vw_TaskList, vw_AppraisalList, the RCAS
-- reports), so promoting NameLocal into Name switches all of them at once with no view change.
--
-- ORDERING — READ BEFORE DEPLOYING
-- This script must NOT reach an environment before the matching AuthDataSeed change is live.
-- The old seeder matched seed rows by Name; against Thai names it recognises nothing and
-- re-inserts all 94 companies under their old English names on the next app start. The fixed
-- seeder keys on LegacyCompanyCode, which this script does not touch. Deploy the app change
-- first, confirm it is running on every node, then apply this bundle.
--
-- Narrow by design: only rows carrying a LegacyCompanyCode are touched, i.e. rows this
-- seeder created. Companies added by hand through /admin/companies keep whatever name their
-- admin gave them. NameLocal is left populated so the Thai name still has a home of its own.
-- ============================================================

SET NOCOUNT ON;

-- ------------------------------------------------------------
-- Guard: auth.Companies has a UNIQUE index on Name filtered to IsDeleted = 0
-- (CompanyConfiguration.cs). Two live rows resolving to the same NameLocal would violate it
-- mid-UPDATE, so fail loudly and leave the data untouched rather than half-renaming.
--
-- The seed file itself contains one duplicate NameLocal — legacyCompanyCode 85 and 95, both
-- 'Advance Property and Consultant Co.,Ltd.' — but their English names collide too, so the
-- Name-keyed seeder only ever inserted code 85. This guard is really here for companies
-- created by hand since go-live.
-- ------------------------------------------------------------

IF EXISTS (
    SELECT 1
    FROM   auth.Companies
    WHERE  IsDeleted = 0
      AND  LegacyCompanyCode IS NOT NULL
      AND  NULLIF(LTRIM(RTRIM(NameLocal)), N'') IS NOT NULL
    GROUP BY LTRIM(RTRIM(NameLocal))
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 50001,
        'Duplicate NameLocal among live seeded companies — resolve the collision before renaming.',
        1;
END

-- ------------------------------------------------------------
-- Promote NameLocal into Name
-- ------------------------------------------------------------

UPDATE auth.Companies
SET    Name = LTRIM(RTRIM(NameLocal))
WHERE  LegacyCompanyCode IS NOT NULL
  AND  NULLIF(LTRIM(RTRIM(NameLocal)), N'') IS NOT NULL
  AND  Name <> LTRIM(RTRIM(NameLocal));

PRINT CONCAT(N'Renamed ', @@ROWCOUNT, N' external appraisal company/companies to their Thai name.');
