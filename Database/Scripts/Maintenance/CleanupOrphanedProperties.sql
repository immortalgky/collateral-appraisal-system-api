/*==============================================================================
  CleanupOrphanedProperties.sql
  ------------------------------------------------------------------------------
  Purpose : Repair two data-integrity problems around property grouping:

    (1) DANGLING GROUP ITEMS  — appraisal.PropertyGroupItems rows that point at
        an AppraisalPropertyId which no longer exists in
        appraisal.AppraisalProperties. These are created when a property is
        deleted without its group membership being removed (there is no FK from
        PropertyGroupItems.AppraisalPropertyId to AppraisalProperties to cascade).
        A dangling item keeps PropertyGroup.Items.Count > 0, so the application's
        "cannot delete a non-empty group" guard refuses to delete the group
        forever. => Part 1 deletes these. SAFE (junction rows, no dependents).

    (2) ORPHANED PROPERTIES    — appraisal.AppraisalProperties rows that are not
        referenced by ANY PropertyGroupItem, i.e. the property belongs to no
        group. These accumulated because the UI "Delete property" action used to
        only remove group membership (keeping the row), and because a property
        can be created without a groupId. Orphans are invisible in the UI
        (the property list only renders properties nested under a group).
        => Part 2 REPORTS them. The destructive delete is left commented out and
        must be enabled deliberately after review (see caveats below).

  Root cause is fixed forward in code:
    - Appraisal.RemoveProperty now also strips the property's PropertyGroupItem.
    - The UI "Delete" now calls the real DeleteProperty endpoint (deletes the row
      + pricing refs + group membership), and the pure "remove from group"
      endpoint was retired. So no NEW dangling items / orphans are produced.

  Run this MANUALLY (SSMS / sqlcmd). It is NOT part of DbUp/EF migrations.
  Schema is [appraisal].
==============================================================================*/

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- any runtime error aborts + rolls back, no open txn/locks.

/*------------------------------------------------------------------------------
  DIAGNOSTIC (read-only) — run first to see the scope of both problems.
------------------------------------------------------------------------------*/
PRINT '=== Dangling PropertyGroupItems (item -> missing property) ===';
SELECT  gi.Id                AS PropertyGroupItemId,
        gi.PropertyGroupId,
        gi.AppraisalPropertyId,
        g.AppraisalId,
        g.GroupName
FROM    appraisal.PropertyGroupItems gi
JOIN    appraisal.PropertyGroups     g  ON g.Id = gi.PropertyGroupId
WHERE   NOT EXISTS (SELECT 1 FROM appraisal.AppraisalProperties p
                    WHERE p.Id = gi.AppraisalPropertyId)
ORDER BY g.AppraisalId, g.GroupName;

PRINT '=== Orphaned AppraisalProperties (belong to no group) ===';
SELECT  p.Id                 AS AppraisalPropertyId,
        p.AppraisalId,
        p.SequenceNumber,
        p.PropertyType,
        a.IsDeleted          AS AppraisalIsDeleted,   -- orphans under a deleted appraisal are usually moot
        p.CreatedOn,
        p.CreatedBy,
        CASE WHEN EXISTS (SELECT 1 FROM appraisal.AppraisalGallery gal
                          WHERE gal.AppraisalPropertyId = p.Id)
             THEN 1 ELSE 0 END AS HasGalleryRows        -- gallery FK is RESTRICT (see caveats)
FROM    appraisal.AppraisalProperties p
LEFT JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId
WHERE   NOT EXISTS (SELECT 1 FROM appraisal.PropertyGroupItems gi
                    WHERE gi.AppraisalPropertyId = p.Id)
ORDER BY p.AppraisalId, p.SequenceNumber;

/*==============================================================================
  PART 1 — Delete dangling PropertyGroupItems.  SAFE. Enabled by default.
  Unblocks groups that could not be deleted because of phantom members.
==============================================================================*/
BEGIN TRANSACTION;

DELETE gi
FROM   appraisal.PropertyGroupItems gi
WHERE  NOT EXISTS (SELECT 1 FROM appraisal.AppraisalProperties p
                   WHERE p.Id = gi.AppraisalPropertyId);

PRINT CONCAT('Part 1: deleted ', @@ROWCOUNT, ' dangling PropertyGroupItem row(s).');

COMMIT TRANSACTION;

/*==============================================================================
  PART 2 — Delete orphaned AppraisalProperties.  DESTRUCTIVE — DISABLED.

  Review the Part-2 diagnostic above first: some orphans may be legitimately
  created-ungrouped data you still want. Delete only the rows you have confirmed
  should be removed.

  CAVEATS before enabling a bulk delete here:
    * Machinery properties may have MachineryCostRef PricingAnalyses anchored to
      them (PricingAnalysis.AnchorId = the property id). Those have NO FK and will
      NOT cascade — a raw delete would silently orphan them. The application's
      DeleteProperty endpoint cleans them up; a raw script must delete them too.
    * appraisal.AppraisalGallery has a RESTRICT FK on AppraisalPropertyId, so a
      raw delete fails while gallery/photo-mapping rows exist (HasGalleryRows = 1).
    * Owned detail tables (Land/Building/Condo/Machinery/Vehicle/Vessel/Lease
      details, LandTitles, RentalInfo, ConstructionInspection, ...) cascade on
      the AppraisalPropertyId FK and are removed automatically.

  RECOMMENDED: delete each confirmed orphan through the application's
  DELETE /appraisal/{appraisalId}/{propertyId} endpoint (now fixed), which
  handles pricing refs + gallery + cascade correctly and atomically.

  If you still want to bulk-delete the SAFE subset in SQL (no gallery rows, and
  handling machinery pricing refs), uncomment and adapt the block below.
==============================================================================*/
-- BEGIN TRANSACTION;
--
-- -- Collect the confirmed orphan ids (add explicit ids / filters you approved).
-- DECLARE @Orphans TABLE (Id uniqueidentifier PRIMARY KEY);
-- INSERT INTO @Orphans (Id)
-- SELECT p.Id
-- FROM   appraisal.AppraisalProperties p
-- WHERE  NOT EXISTS (SELECT 1 FROM appraisal.PropertyGroupItems gi
--                    WHERE gi.AppraisalPropertyId = p.Id)
--   AND  NOT EXISTS (SELECT 1 FROM appraisal.AppraisalGallery gal    -- skip RESTRICT-blocked rows
--                    WHERE gal.AppraisalPropertyId = p.Id);
--   -- AND p.Id IN ( ... explicit reviewed ids ... );
--
-- -- Mirror CleanupForPropertyAsync: remove MachineryCostRef pricing references
-- -- anchored to these properties BEFORE deleting the rows.
-- -- Verify the pricing table name/schema and the MachineryCostRef SubjectType
-- -- value in your DB, then enable:
-- -- DELETE pa FROM appraisal.PricingAnalyses pa
-- -- JOIN @Orphans o ON o.Id = pa.AnchorId
-- -- WHERE pa.SubjectType = <MachineryCostRef value>;
--
-- DELETE p
-- FROM   appraisal.AppraisalProperties p
-- JOIN   @Orphans o ON o.Id = p.Id;
--
-- PRINT CONCAT('Part 2: deleted ', @@ROWCOUNT, ' orphaned AppraisalProperty row(s).');
--
-- COMMIT TRANSACTION;
