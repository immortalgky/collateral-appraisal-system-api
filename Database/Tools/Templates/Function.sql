-- =============================================
-- Function: {FunctionName}
-- Schema: {SchemaName}
-- Description: {Description}
-- Created: {Date}
-- Author: {Author}
-- =============================================

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{SchemaName}].[{FunctionName}]') AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
    DROP FUNCTION [{SchemaName}].[{FunctionName}]
GO

CREATE FUNCTION [{SchemaName}].[{FunctionName}]
(
    @Parameter1 INT,
    @Parameter2 NVARCHAR(255)
)
RETURNS {ReturnType}
AS
BEGIN
    -- Function logic here
    RETURN @Parameter1
END
GO

-- Grant permissions
GRANT SELECT ON [{SchemaName}].[{FunctionName}] TO [db_datareader]
GO