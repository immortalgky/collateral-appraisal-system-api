-- Create RunningNumbers table in dbo schema (shared across modules)
-- Each Type+Year combination is a separate record (yearly reset)
IF OBJECT_ID('dbo.RunningNumbers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RunningNumbers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Type NVARCHAR(50) NOT NULL,           -- RunningNumberType: 'REQUEST', 'APPRAISAL', etc.
        Prefix NVARCHAR(10) NOT NULL,         -- 'REQ', 'A', etc.
        CurrentNumber INT NOT NULL DEFAULT 0,
        Year INT NOT NULL,                    -- Thai year (2568, 2569, etc.)
        CreatedOn DATETIME2 NULL,
        CreatedBy NVARCHAR(256) NULL,
        UpdatedOn DATETIME2 NULL,
        UpdatedBy NVARCHAR(256) NULL,

        -- Each type resets yearly - unique per Type+Year
        CONSTRAINT UQ_RunningNumbers_Type_Year UNIQUE (Type, Year)
    );

    -- Create index for fast lookups
    CREATE INDEX IX_RunningNumbers_Type_Year ON dbo.RunningNumbers (Type, Year);
END
GO

-- Seed initial data for current Thai year (2568 = 2025)
-- Only insert if no records exist for the current year
IF NOT EXISTS (SELECT 1 FROM dbo.RunningNumbers WHERE Type = 'REQUEST' AND Year = 2568)
BEGIN
    INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
    VALUES ('REQUEST', 'REQ', 0, 2568, GETUTCDATE());  -- Request: REQ-000001-2568
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RunningNumbers WHERE Type = 'APPRAISAL' AND Year = 2568)
BEGIN
    INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
    VALUES ('APPRAISAL', 'A', 0, 2568, GETUTCDATE());  -- Appraisal: 68A000001
END
GO
