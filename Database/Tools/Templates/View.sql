-- =============================================
-- View: {ViewName}
-- Schema: {SchemaName}
-- Description: {Description}
-- Created: {Date}
-- Author: {Author}
-- =============================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[{SchemaName}].[{ViewName}]'))
    DROP VIEW [{SchemaName}].[{ViewName}]
GO

CREATE VIEW [{SchemaName}].[{ViewName}]
AS
    -- View definition here
    SELECT 
        1 as PlaceholderColumn
GO

-- Grant permissions
GRANT SELECT ON [{SchemaName}].[{ViewName}] TO [db_datareader]
GO