-- ============================================================
-- Data fix: Obligation is now a multi-select (ObligationType, a JSON array of Obligation parameter codes)
-- instead of the single value that lived in HasObligation, and a mortgage has its own detail column.
--
-- The EF migration AddPropertyDetailBookFields only renames HasObligation -> ObligationType (and widens it
-- to nvarchar(500)); it does not touch the values. This script converts them for the five tables that carry
-- the field (LandAppraisalDetails, BuildingAppraisalDetails, CondoAppraisalDetails, ProjectLands,
-- ProjectTowers). It makes no assumption about what the old column held in any one database, so every
-- shape the single-value column allowed is handled:
--   1. blank / whitespace-only            -> NULL
--   2. already a valid JSON array         -> untouched
--   3. exactly 01 02 03 04 05 99 (trimmed) -> that code as a one-element array  '02' -> '["02"]'
--   4. legacy boolean '0'                 -> ["01"]  (ไม่มีภาระผูกพัน)
--   5. legacy boolean '1'                 -> ["99"]  (อื่นๆ), ObligationDetails kept so the text shows under ภาระอื่นๆ
--   6. anything else (garbage, '2'..'5', broken JSON) -> ["99"], ObligationDetails kept; the count is PRINTed
--      per table so the deploy log shows it. Codes are never guessed (a bare '2' is not read as '02').
--   7. a row whose ORIGINAL single value was '02' moves its ObligationDetails to MortgageDetails, only while
--      MortgageDetails is still empty. ObligationDetails now means "other obligations" and is shown only when
--      03/04/05/99 is picked. A text longer than 500 (Condo ObligationDetails is nvarchar(4000), MortgageDetails
--      nvarchar(500)) is NOT moved rather than truncated; the count is PRINTed.
--   8. Land and Condo: a list containing 03 (ติดสัญญาเช่า) forces การให้เช่า = มี, i.e. IsRentedOut NULL/0 -> 1.
--      Building and the project tables have no IsRentedOut. The count is PRINTed.
--
-- One transaction per table: a failure rolls that table back whole, and the steps run mortgage-move first,
-- conversion second, so a re-run after a stopped run starts from a consistent table. Replay-safe: after a
-- complete run every value is a JSON array, so steps 1-7 match nothing; step 8 only fires on rows still out of line.
--
-- NOTE: the Obligation parameter cache has no eviction - restart the API after running this.
-- ============================================================
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- LandAppraisalDetails
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @blank int, @moved int, @notMoved int, @unrecognised int, @rented int;

    -- 1. blank -> NULL
    UPDATE [appraisal].[LandAppraisalDetails] SET [ObligationType] = NULL
    WHERE  [ObligationType] IS NOT NULL AND LTRIM(RTRIM([ObligationType])) = N'';
    SET @blank = @@ROWCOUNT;

    -- 7. mortgage move, decided on the ORIGINAL single value, before it is rewritten
    SELECT @notMoved = COUNT(*) FROM [appraisal].[LandAppraisalDetails]
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) > 500;

    UPDATE [appraisal].[LandAppraisalDetails]
    SET    [MortgageDetails] = [ObligationDetails], [ObligationDetails] = NULL
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) <= 500;
    SET @moved = @@ROWCOUNT;

    -- 3-6. single value -> array (a valid JSON array is left alone)
    SELECT @unrecognised = COUNT(*) FROM [appraisal].[LandAppraisalDetails]
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1)
      AND  LTRIM(RTRIM([ObligationType])) NOT IN (N'01', N'02', N'03', N'04', N'05', N'99', N'0', N'1');

    UPDATE [appraisal].[LandAppraisalDetails]
    SET    [ObligationType] = CASE LTRIM(RTRIM([ObligationType]))
                                  WHEN N'0'  THEN N'["01"]'
                                  WHEN N'01' THEN N'["01"]'
                                  WHEN N'02' THEN N'["02"]'
                                  WHEN N'03' THEN N'["03"]'
                                  WHEN N'04' THEN N'["04"]'
                                  WHEN N'05' THEN N'["05"]'
                                  ELSE N'["99"]'            -- '99', legacy '1', and anything unrecognised
                              END
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1);

    -- 8. 03 means the property is let out
    UPDATE [appraisal].[LandAppraisalDetails] SET [IsRentedOut] = 1
    WHERE  [ObligationType] LIKE N'%"03"%' AND ([IsRentedOut] IS NULL OR [IsRentedOut] = 0);
    SET @rented = @@ROWCOUNT;

    COMMIT TRANSACTION;

    PRINT N'LandAppraisalDetails: ' + CAST(@blank AS nvarchar(20)) + N' blank -> NULL, '
        + CAST(@moved AS nvarchar(20)) + N' mortgage text moved ('
        + CAST(@notMoved AS nvarchar(20)) + N' over 500 chars left in ObligationDetails), '
        + CAST(@unrecognised AS nvarchar(20)) + N' unrecognised value(s) mapped to ["99"], '
        + CAST(@rented AS nvarchar(20)) + N' set to rented out (03)';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- BuildingAppraisalDetails
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @blank int, @moved int, @notMoved int, @unrecognised int;

    -- 1. blank -> NULL
    UPDATE [appraisal].[BuildingAppraisalDetails] SET [ObligationType] = NULL
    WHERE  [ObligationType] IS NOT NULL AND LTRIM(RTRIM([ObligationType])) = N'';
    SET @blank = @@ROWCOUNT;

    -- 7. mortgage move, decided on the ORIGINAL single value, before it is rewritten
    SELECT @notMoved = COUNT(*) FROM [appraisal].[BuildingAppraisalDetails]
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) > 500;

    UPDATE [appraisal].[BuildingAppraisalDetails]
    SET    [MortgageDetails] = [ObligationDetails], [ObligationDetails] = NULL
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) <= 500;
    SET @moved = @@ROWCOUNT;

    -- 3-6. single value -> array (a valid JSON array is left alone)
    SELECT @unrecognised = COUNT(*) FROM [appraisal].[BuildingAppraisalDetails]
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1)
      AND  LTRIM(RTRIM([ObligationType])) NOT IN (N'01', N'02', N'03', N'04', N'05', N'99', N'0', N'1');

    UPDATE [appraisal].[BuildingAppraisalDetails]
    SET    [ObligationType] = CASE LTRIM(RTRIM([ObligationType]))
                                  WHEN N'0'  THEN N'["01"]'
                                  WHEN N'01' THEN N'["01"]'
                                  WHEN N'02' THEN N'["02"]'
                                  WHEN N'03' THEN N'["03"]'
                                  WHEN N'04' THEN N'["04"]'
                                  WHEN N'05' THEN N'["05"]'
                                  ELSE N'["99"]'            -- '99', legacy '1', and anything unrecognised
                              END
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1);

    COMMIT TRANSACTION;

    PRINT N'BuildingAppraisalDetails: ' + CAST(@blank AS nvarchar(20)) + N' blank -> NULL, '
        + CAST(@moved AS nvarchar(20)) + N' mortgage text moved ('
        + CAST(@notMoved AS nvarchar(20)) + N' over 500 chars left in ObligationDetails), '
        + CAST(@unrecognised AS nvarchar(20)) + N' unrecognised value(s) mapped to ["99"]';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- CondoAppraisalDetails
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @blank int, @moved int, @notMoved int, @unrecognised int, @rented int;

    -- 1. blank -> NULL
    UPDATE [appraisal].[CondoAppraisalDetails] SET [ObligationType] = NULL
    WHERE  [ObligationType] IS NOT NULL AND LTRIM(RTRIM([ObligationType])) = N'';
    SET @blank = @@ROWCOUNT;

    -- 7. mortgage move, decided on the ORIGINAL single value, before it is rewritten
    SELECT @notMoved = COUNT(*) FROM [appraisal].[CondoAppraisalDetails]
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) > 500;

    UPDATE [appraisal].[CondoAppraisalDetails]
    SET    [MortgageDetails] = [ObligationDetails], [ObligationDetails] = NULL
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) <= 500;
    SET @moved = @@ROWCOUNT;

    -- 3-6. single value -> array (a valid JSON array is left alone)
    SELECT @unrecognised = COUNT(*) FROM [appraisal].[CondoAppraisalDetails]
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1)
      AND  LTRIM(RTRIM([ObligationType])) NOT IN (N'01', N'02', N'03', N'04', N'05', N'99', N'0', N'1');

    UPDATE [appraisal].[CondoAppraisalDetails]
    SET    [ObligationType] = CASE LTRIM(RTRIM([ObligationType]))
                                  WHEN N'0'  THEN N'["01"]'
                                  WHEN N'01' THEN N'["01"]'
                                  WHEN N'02' THEN N'["02"]'
                                  WHEN N'03' THEN N'["03"]'
                                  WHEN N'04' THEN N'["04"]'
                                  WHEN N'05' THEN N'["05"]'
                                  ELSE N'["99"]'            -- '99', legacy '1', and anything unrecognised
                              END
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1);

    -- 8. 03 means the property is let out
    UPDATE [appraisal].[CondoAppraisalDetails] SET [IsRentedOut] = 1
    WHERE  [ObligationType] LIKE N'%"03"%' AND ([IsRentedOut] IS NULL OR [IsRentedOut] = 0);
    SET @rented = @@ROWCOUNT;

    COMMIT TRANSACTION;

    PRINT N'CondoAppraisalDetails: ' + CAST(@blank AS nvarchar(20)) + N' blank -> NULL, '
        + CAST(@moved AS nvarchar(20)) + N' mortgage text moved ('
        + CAST(@notMoved AS nvarchar(20)) + N' over 500 chars left in ObligationDetails), '
        + CAST(@unrecognised AS nvarchar(20)) + N' unrecognised value(s) mapped to ["99"], '
        + CAST(@rented AS nvarchar(20)) + N' set to rented out (03)';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ProjectLands
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @blank int, @moved int, @notMoved int, @unrecognised int;

    -- 1. blank -> NULL
    UPDATE [appraisal].[ProjectLands] SET [ObligationType] = NULL
    WHERE  [ObligationType] IS NOT NULL AND LTRIM(RTRIM([ObligationType])) = N'';
    SET @blank = @@ROWCOUNT;

    -- 7. mortgage move, decided on the ORIGINAL single value, before it is rewritten
    SELECT @notMoved = COUNT(*) FROM [appraisal].[ProjectLands]
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) > 500;

    UPDATE [appraisal].[ProjectLands]
    SET    [MortgageDetails] = [ObligationDetails], [ObligationDetails] = NULL
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) <= 500;
    SET @moved = @@ROWCOUNT;

    -- 3-6. single value -> array (a valid JSON array is left alone)
    SELECT @unrecognised = COUNT(*) FROM [appraisal].[ProjectLands]
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1)
      AND  LTRIM(RTRIM([ObligationType])) NOT IN (N'01', N'02', N'03', N'04', N'05', N'99', N'0', N'1');

    UPDATE [appraisal].[ProjectLands]
    SET    [ObligationType] = CASE LTRIM(RTRIM([ObligationType]))
                                  WHEN N'0'  THEN N'["01"]'
                                  WHEN N'01' THEN N'["01"]'
                                  WHEN N'02' THEN N'["02"]'
                                  WHEN N'03' THEN N'["03"]'
                                  WHEN N'04' THEN N'["04"]'
                                  WHEN N'05' THEN N'["05"]'
                                  ELSE N'["99"]'            -- '99', legacy '1', and anything unrecognised
                              END
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1);

    COMMIT TRANSACTION;

    PRINT N'ProjectLands: ' + CAST(@blank AS nvarchar(20)) + N' blank -> NULL, '
        + CAST(@moved AS nvarchar(20)) + N' mortgage text moved ('
        + CAST(@notMoved AS nvarchar(20)) + N' over 500 chars left in ObligationDetails), '
        + CAST(@unrecognised AS nvarchar(20)) + N' unrecognised value(s) mapped to ["99"]';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ProjectTowers
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @blank int, @moved int, @notMoved int, @unrecognised int;

    -- 1. blank -> NULL
    UPDATE [appraisal].[ProjectTowers] SET [ObligationType] = NULL
    WHERE  [ObligationType] IS NOT NULL AND LTRIM(RTRIM([ObligationType])) = N'';
    SET @blank = @@ROWCOUNT;

    -- 7. mortgage move, decided on the ORIGINAL single value, before it is rewritten
    SELECT @notMoved = COUNT(*) FROM [appraisal].[ProjectTowers]
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) > 500;

    UPDATE [appraisal].[ProjectTowers]
    SET    [MortgageDetails] = [ObligationDetails], [ObligationDetails] = NULL
    WHERE  LTRIM(RTRIM([ObligationType])) = N'02' AND [MortgageDetails] IS NULL
      AND  NULLIF(LTRIM(RTRIM([ObligationDetails])), N'') IS NOT NULL AND LEN([ObligationDetails]) <= 500;
    SET @moved = @@ROWCOUNT;

    -- 3-6. single value -> array (a valid JSON array is left alone)
    SELECT @unrecognised = COUNT(*) FROM [appraisal].[ProjectTowers]
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1)
      AND  LTRIM(RTRIM([ObligationType])) NOT IN (N'01', N'02', N'03', N'04', N'05', N'99', N'0', N'1');

    UPDATE [appraisal].[ProjectTowers]
    SET    [ObligationType] = CASE LTRIM(RTRIM([ObligationType]))
                                  WHEN N'0'  THEN N'["01"]'
                                  WHEN N'01' THEN N'["01"]'
                                  WHEN N'02' THEN N'["02"]'
                                  WHEN N'03' THEN N'["03"]'
                                  WHEN N'04' THEN N'["04"]'
                                  WHEN N'05' THEN N'["05"]'
                                  ELSE N'["99"]'            -- '99', legacy '1', and anything unrecognised
                              END
    WHERE  [ObligationType] IS NOT NULL
      AND  NOT (LEFT(LTRIM([ObligationType]), 1) = N'[' AND ISJSON([ObligationType]) = 1);

    COMMIT TRANSACTION;

    PRINT N'ProjectTowers: ' + CAST(@blank AS nvarchar(20)) + N' blank -> NULL, '
        + CAST(@moved AS nvarchar(20)) + N' mortgage text moved ('
        + CAST(@notMoved AS nvarchar(20)) + N' over 500 chars left in ObligationDetails), '
        + CAST(@unrecognised AS nvarchar(20)) + N' unrecognised value(s) mapped to ["99"]';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
