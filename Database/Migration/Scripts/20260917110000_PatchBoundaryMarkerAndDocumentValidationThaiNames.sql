-- Give the BoundaryMarker and DocumentValidation parameter groups their Thai wording.
--
-- Both groups shipped in 20260317002600_SeedData_GeneralParameter.sql with every TH-language row
-- carrying the ENGLISH string, so หลักเขต and ผลตรวจสอบเอกสาร on the land title dialog read in
-- English whichever language the user picked. Two EN rows are tidied while here: BoundaryMarker
-- '02' lost its trailing full stop and '99' its lower-case 'other'.
--
-- Only descriptions change. Group names and codes stay as they are — they are what saved titles
-- store and what the forms look up — so no stored value moves.
--
-- The seed script itself is corrected in place for fresh databases, but DbUp journals one-time
-- scripts by file name with no checksum, so that edit is a no-op wherever it already ran. This
-- patch is what reaches those databases.
--
-- Joining on the stale description as well as the code makes this idempotent AND stops it
-- overwriting a description an administrator has since edited through the Parameter screen.

UPDATE p
SET [Description] = c.NewDescription,
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
FROM [parameter].[Parameters] p
JOIN (VALUES
        (N'BoundaryMarker',     N'TH', N'01', N'Found boundary marker',           N'พบหลักเขต'),
        (N'BoundaryMarker',     N'TH', N'02', N'The boundary marker is unclear.', N'หลักเขตไม่ชัดเจน'),
        (N'BoundaryMarker',     N'EN', N'02', N'The boundary marker is unclear.', N'The boundary marker is unclear'),
        (N'BoundaryMarker',     N'TH', N'03', N'No boundary marker found',        N'ไม่พบหลักเขต'),
        (N'BoundaryMarker',     N'TH', N'04', N'Damaged',                         N'หลักเขตชำรุด'),
        (N'BoundaryMarker',     N'TH', N'99', N'other',                           N'อื่นๆ'),
        (N'BoundaryMarker',     N'EN', N'99', N'other',                           N'Other'),
        (N'DocumentValidation', N'TH', N'01', N'Correctly Matched',               N'ถูกต้องตรงกัน'),
        (N'DocumentValidation', N'TH', N'02', N'Not Consistent',                  N'ไม่สอดคล้องกัน')
     ) AS c ([Group], [Language], Code, StaleDescription, NewDescription)
  ON  c.[Group]          = p.[Group]
  AND c.[Language]       = p.[Language]
  AND c.Code             = p.[Code]
  AND c.StaleDescription = p.[Description];

PRINT CONCAT('Patched boundary marker / document validation parameter names: ', @@ROWCOUNT, ' row(s).');
