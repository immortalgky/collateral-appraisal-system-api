-- =============================================
-- Stored Procedure: {ProcedureName}
-- Schema: {SchemaName}
-- Description: {Description}
-- Created: {Date}
-- Author: {Author}
-- =============================================

IF EXISTS (SELECT * FROM sys.procedures WHERE object_id = OBJECT_ID(N'[{SchemaName}].[{ProcedureName}]'))
    DROP PROCEDURE [{SchemaName}].[{ProcedureName}]
GO

CREATE PROCEDURE [{SchemaName}].[{ProcedureName}]
    @Parameter1 INT = NULL,
    @Parameter2 NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Procedure logic here
    SELECT 
        @Parameter1 as Parameter1,
        @Parameter2 as Parameter2
        
END
GO

-- Grant permissions
GRANT EXECUTE ON [{SchemaName}].[{ProcedureName}] TO [db_executor]
GO