-- =============================================
-- Database Objects Testing Script
-- Description: Test all views, stored procedures, and functions
-- Created: 2025-07-31
-- =============================================

PRINT 'Starting Database Objects Testing...'
PRINT '====================================='

-- Test Views
PRINT ''
PRINT 'Testing Views:'
PRINT '--------------'

-- Test Request Summary View
BEGIN TRY
    PRINT 'Testing [request].[vw_Request_Summary]...'
    SELECT COUNT(*) as RecordCount FROM [request].[vw_Request_Summary]
    PRINT '✓ vw_Request_Summary - OK'
END TRY
BEGIN CATCH
    PRINT '✗ vw_Request_Summary - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Request Dashboard View
BEGIN TRY
    PRINT 'Testing [request].[vw_Request_Dashboard]...'
    SELECT * FROM [request].[vw_Request_Dashboard]
    PRINT '✓ vw_Request_Dashboard - OK'
END TRY
BEGIN CATCH
    PRINT '✗ vw_Request_Dashboard - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Document Summary View
BEGIN TRY
    PRINT 'Testing [document].[vw_Document_Summary]...'
    SELECT COUNT(*) as RecordCount FROM [document].[vw_Document_Summary]
    PRINT '✓ vw_Document_Summary - OK'
END TRY
BEGIN CATCH
    PRINT '✗ vw_Document_Summary - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Assignment Task Metrics View
BEGIN TRY
    PRINT 'Testing [assignment].[vw_Assignment_TaskMetrics]...'
    SELECT COUNT(*) as RecordCount FROM [assignment].[vw_Assignment_TaskMetrics]
    PRINT '✓ vw_Assignment_TaskMetrics - OK'
END TRY
BEGIN CATCH
    PRINT '✗ vw_Assignment_TaskMetrics - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Stored Procedures
PRINT ''
PRINT 'Testing Stored Procedures:'
PRINT '-------------------------'

-- Test Request Metrics Procedure
BEGIN TRY
    PRINT 'Testing [request].[sp_Request_GetMetrics]...'
    EXEC [request].[sp_Request_GetMetrics] 
        @StartDate = '2025-01-01', 
        @EndDate = '2025-07-31',
        @PropertyType = NULL,
        @IncludeInactive = 0
    PRINT '✓ sp_Request_GetMetrics - OK'
END TRY
BEGIN CATCH
    PRINT '✗ sp_Request_GetMetrics - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Document Cleanup Procedure (Dry Run)
BEGIN TRY
    PRINT 'Testing [document].[sp_Document_CleanupExpired] (dry run)...'
    EXEC [document].[sp_Document_CleanupExpired] 
        @RetentionDays = 365,
        @ArchiveOnly = 1,
        @DryRun = 1
    PRINT '✓ sp_Document_CleanupExpired - OK'
END TRY
BEGIN CATCH
    PRINT '✗ sp_Document_CleanupExpired - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Test Functions
PRINT ''
PRINT 'Testing Functions:'
PRINT '-----------------'

-- Test Request Age Function
BEGIN TRY
    PRINT 'Testing [request].[fn_Request_CalculateAge]...'
    -- Test with a sample request ID (1) - will return 0 if not found
    SELECT [request].[fn_Request_CalculateAge](1) as RequestAge
    PRINT '✓ fn_Request_CalculateAge - OK'
END TRY
BEGIN CATCH
    PRINT '✗ fn_Request_CalculateAge - ERROR: ' + ERROR_MESSAGE()
END CATCH

-- Performance Tests
PRINT ''
PRINT 'Performance Tests:'
PRINT '------------------'

DECLARE @StartTime DATETIME2 = GETDATE()

-- Test view performance
SELECT COUNT(*) FROM [request].[vw_Request_Summary]

DECLARE @EndTime DATETIME2 = GETDATE()
DECLARE @Duration INT = DATEDIFF(MILLISECOND, @StartTime, @EndTime)

PRINT 'vw_Request_Summary query duration: ' + CAST(@Duration AS VARCHAR(10)) + ' ms'

IF @Duration > 5000
    PRINT 'WARNING: Query took longer than 5 seconds'
ELSE
    PRINT '✓ Performance acceptable'

PRINT ''
PRINT 'Testing completed!'
PRINT '=================='