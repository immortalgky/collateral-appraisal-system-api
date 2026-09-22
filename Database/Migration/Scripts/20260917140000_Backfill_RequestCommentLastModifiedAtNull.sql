-- ============================================================
-- Backfill: clear the fake "last modified" date on comments that were never edited.
--
-- request.RequestComments.LastModifiedAt used to be NOT NULL, and creating a comment never set it,
-- so every comment was stored with 0001-01-01. The UI shows "(edited)" whenever the value is present,
-- which labelled every new comment as edited. The column is now nullable (EF migration
-- MakeRequestCommentLastModifiedAtNullable); this turns the placeholder into NULL.
--
-- Ordering: DbUp runs after every EF migration, so the column already accepts NULL.
-- Idempotent: only rows still holding the placeholder are touched.
-- ============================================================

UPDATE [request].[RequestComments]
SET [LastModifiedAt] = NULL
WHERE [LastModifiedAt] = '0001-01-01';
