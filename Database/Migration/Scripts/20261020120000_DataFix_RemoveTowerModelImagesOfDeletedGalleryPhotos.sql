-- ============================================================
-- Data fix: Block tower / model images that point at a gallery photo that no longer exists.
--
-- Deleting a photo from the gallery removed its property, topic and law-image links but not the
-- ProjectTowerImages / ProjectModelImages rows (no foreign key), so those rows were left pointing at
-- nothing. The editors hide them, but a dangling row that was the cover (IsThumbnail = 1) stopped the
-- next photo added from becoming the cover. The delete now unlinks them; this clears the rows already
-- left behind and gives each tower / model that lost its cover the image with the lowest
-- DisplaySequence as the new cover.
--
-- Only towers / models whose cover was among the deleted rows get a new cover: one that had its cover
-- switched off on purpose is left alone.
-- Runs inside the migrator's per-script transaction. Idempotent: a second run finds nothing to delete.
-- ============================================================

DECLARE @CoverlessTowers TABLE (ProjectTowerId UNIQUEIDENTIFIER PRIMARY KEY);
DECLARE @CoverlessModels TABLE (ProjectModelId UNIQUEIDENTIFIER PRIMARY KEY);
DECLARE @TowerImages INT, @ModelImages INT;

INSERT INTO @CoverlessTowers
SELECT DISTINCT i.[ProjectTowerId]
FROM [appraisal].[ProjectTowerImages] i
WHERE i.[IsThumbnail] = 1
  AND NOT EXISTS (SELECT 1 FROM [appraisal].[AppraisalGallery] g WHERE g.[Id] = i.[GalleryPhotoId]);

INSERT INTO @CoverlessModels
SELECT DISTINCT i.[ProjectModelId]
FROM [appraisal].[ProjectModelImages] i
WHERE i.[IsThumbnail] = 1
  AND NOT EXISTS (SELECT 1 FROM [appraisal].[AppraisalGallery] g WHERE g.[Id] = i.[GalleryPhotoId]);

DELETE i
FROM [appraisal].[ProjectTowerImages] i
WHERE NOT EXISTS (SELECT 1 FROM [appraisal].[AppraisalGallery] g WHERE g.[Id] = i.[GalleryPhotoId]);
SET @TowerImages = @@ROWCOUNT;

DELETE i
FROM [appraisal].[ProjectModelImages] i
WHERE NOT EXISTS (SELECT 1 FROM [appraisal].[AppraisalGallery] g WHERE g.[Id] = i.[GalleryPhotoId]);
SET @ModelImages = @@ROWCOUNT;

-- New cover: lowest DisplaySequence among what is left (Id breaks a tie so reruns pick the same row).
UPDATE i
SET    i.[IsThumbnail] = 1
FROM   [appraisal].[ProjectTowerImages] i
WHERE  i.[ProjectTowerId] IN (SELECT ProjectTowerId FROM @CoverlessTowers)
  AND  NOT EXISTS (SELECT 1 FROM [appraisal].[ProjectTowerImages] c
                   WHERE c.[ProjectTowerId] = i.[ProjectTowerId] AND c.[IsThumbnail] = 1)
  AND  i.[Id] = (SELECT TOP (1) f.[Id] FROM [appraisal].[ProjectTowerImages] f
                 WHERE f.[ProjectTowerId] = i.[ProjectTowerId]
                 ORDER BY f.[DisplaySequence], f.[Id]);

UPDATE i
SET    i.[IsThumbnail] = 1
FROM   [appraisal].[ProjectModelImages] i
WHERE  i.[ProjectModelId] IN (SELECT ProjectModelId FROM @CoverlessModels)
  AND  NOT EXISTS (SELECT 1 FROM [appraisal].[ProjectModelImages] c
                   WHERE c.[ProjectModelId] = i.[ProjectModelId] AND c.[IsThumbnail] = 1)
  AND  i.[Id] = (SELECT TOP (1) f.[Id] FROM [appraisal].[ProjectModelImages] f
                 WHERE f.[ProjectModelId] = i.[ProjectModelId]
                 ORDER BY f.[DisplaySequence], f.[Id]);

-- What moved, for the deployment log.
SELECT @TowerImages AS TowerImagesDeleted, @ModelImages AS ModelImagesDeleted;
