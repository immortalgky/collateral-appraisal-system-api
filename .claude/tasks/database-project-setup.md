# Database Project Setup

## Task Overview
**Objective**: Create the foundational database project structure to manage views, stored procedures, and functions in the collateral appraisal system.

**Priority**: High  
**Estimated Effort**: 4-6 hours  
**Dependencies**: None  
**Assignee**: TBD  

## Prerequisites
- Access to the collateral-appraisal-system-api solution
- Visual Studio or VS Code with C# extension
- Understanding of the current modular architecture
- SQL Server Management Studio or Azure Data Studio (recommended)

## Current Architecture Analysis

### Existing Solution Structure
```
collateral-appraisal-system-api/
├── Bootstrapper/Api/                 # Main API project (.NET 9.0)
├── Modules/
│   ├── Request/Request/             # Uses "request" schema
│   ├── Document/Document/           # Uses EF migrations
│   ├── Assignment/Assignment/       # Uses "assignment" schema
│   ├── Auth/OAuth2OpenId/          # Uses EF migrations
│   └── Notification/Notification/   # Uses EF migrations
└── Shared/                         # Shared libraries
```

### Current Database Pattern
- Each module has its own `DbContext` with schema-specific configuration
- EF Core migrations handle table structure changes
- Schema names: "request", "assignment", "document", etc.
- Connection strings configured in startup project

## Implementation Steps

### Step 1: Create Database Project Structure
Create the following directory structure in the solution root:

```
Database/
├── Database.csproj
├── Scripts/
│   ├── Views/
│   │   ├── Request/
│   │   ├── Document/
│   │   ├── Assignment/
│   │   ├── Auth/
│   │   ├── Notification/
│   │   └── Shared/
│   ├── StoredProcedures/
│   │   ├── Request/
│   │   ├── Document/
│   │   ├── Assignment/
│   │   ├── Auth/
│   │   ├── Notification/
│   │   └── Shared/
│   ├── Functions/
│   │   ├── Request/
│   │   ├── Document/
│   │   ├── Assignment/
│   │   ├── Auth/
│   │   ├── Notification/
│   │   └── Shared/
│   └── Seed/
│       ├── MasterData/
│       └── TestData/
├── Migrations/
│   ├── SchemaObjects/
│   └── Rollback/
├── Configuration/
│   ├── appsettings.Database.json
│   └── connectionstrings.json
└── Tools/
    ├── Templates/
    └── Utilities/
```

### Step 2: Create Database.csproj File

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DbUp" Version="5.0.40" />
    <PackageReference Include="DbUp-SqlServer" Version="5.0.40" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.5" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.5" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.5" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.5" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>

  <ItemGroup>
    <None Include="Scripts\**\*.sql" CopyToOutputDirectory="Always" />
    <None Include="Configuration\*.json" CopyToOutputDirectory="Always" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="Scripts\Views\Request\" />
    <Folder Include="Scripts\Views\Document\" />
    <Folder Include="Scripts\Views\Assignment\" />
    <Folder Include="Scripts\Views\Auth\" />
    <Folder Include="Scripts\Views\Notification\" />
    <Folder Include="Scripts\Views\Shared\" />
    <Folder Include="Scripts\StoredProcedures\Request\" />
    <Folder Include="Scripts\StoredProcedures\Document\" />
    <Folder Include="Scripts\StoredProcedures\Assignment\" />
    <Folder Include="Scripts\StoredProcedures\Auth\" />
    <Folder Include="Scripts\StoredProcedures\Notification\" />
    <Folder Include="Scripts\StoredProcedures\Shared\" />
    <Folder Include="Scripts\Functions\Request\" />
    <Folder Include="Scripts\Functions\Document\" />
    <Folder Include="Scripts\Functions\Assignment\" />
    <Folder Include="Scripts\Functions\Auth\" />
    <Folder Include="Scripts\Functions\Notification\" />
    <Folder Include="Scripts\Functions\Shared\" />
  </ItemGroup>

</Project>
```

### Step 3: Add Database Project to Solution
1. Open the `collateral-appraisal-system-api.sln` file
2. Add the Database project reference:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Database", "Database\Database.csproj", "{NEW-GUID-HERE}"
EndProject
```

3. Add to solution configuration sections (copy pattern from other projects)

### Step 4: Create Configuration Files

**Configuration/appsettings.Database.json**:
```json
{
  "DatabaseMigration": {
    "EnableMigration": true,
    "MigrationsTableName": "DatabaseMigrationHistory",
    "MigrationsSchema": "dbo",
    "ScriptTimeout": 300,
    "BackupDatabase": true,
    "ValidateOnly": false
  },
  "Environments": {
    "Development": {
      "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=CollateralAppraisalSystem;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true",
      "EnableSeeding": true,
      "SeedingMode": "TestData"
    },
    "Staging": {
      "ConnectionString": "",
      "EnableSeeding": true,
      "SeedingMode": "MasterDataOnly"
    },
    "Production": {
      "ConnectionString": "",
      "EnableSeeding": false,
      "SeedingMode": "None"
    }
  }
}
```

### Step 5: Create Script Templates

**Tools/Templates/View.sql**:
```sql
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
```

**Tools/Templates/StoredProcedure.sql**:
```sql
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
```

**Tools/Templates/Function.sql**:
```sql
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
```

### Step 6: Create Sample Database Objects

**Scripts/Views/Request/vw_RequestSummary.sql**:
```sql
-- =============================================
-- View: vw_RequestSummary
-- Schema: request
-- Description: Provides summary information for requests with comment counts
-- Created: 2025-07-31
-- =============================================

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[request].[vw_RequestSummary]'))
    DROP VIEW [request].[vw_RequestSummary]
GO

CREATE VIEW [request].[vw_RequestSummary]
AS
    SELECT 
        r.Id,
        r.AppraisalNumber,
        r.PropertyType,
        r.Status,
        r.CreatedOn,
        r.CreatedBy,
        COUNT(rc.Id) as CommentCount,
        COUNT(rt.Id) as TitleCount
    FROM [request].[Requests] r
    LEFT JOIN [request].[RequestComments] rc ON r.Id = rc.RequestId
    LEFT JOIN [request].[RequestTitles] rt ON r.Id = rt.RequestId
    GROUP BY r.Id, r.AppraisalNumber, r.PropertyType, r.Status, r.CreatedOn, r.CreatedBy
GO

GRANT SELECT ON [request].[vw_RequestSummary] TO [db_datareader]
GO
```

**Scripts/StoredProcedures/Request/sp_GetRequestMetrics.sql**:
```sql
-- =============================================
-- Stored Procedure: sp_GetRequestMetrics
-- Schema: request
-- Description: Gets metrics for requests within a date range
-- Created: 2025-07-31
-- =============================================

IF EXISTS (SELECT * FROM sys.procedures WHERE object_id = OBJECT_ID(N'[request].[sp_GetRequestMetrics]'))
    DROP PROCEDURE [request].[sp_GetRequestMetrics]
GO

CREATE PROCEDURE [request].[sp_GetRequestMetrics]
    @StartDate DATETIME2,
    @EndDate DATETIME2,
    @PropertyType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        COUNT(*) as TotalRequests,
        COUNT(CASE WHEN Status = 'Completed' THEN 1 END) as CompletedRequests,
        COUNT(CASE WHEN Status = 'Pending' THEN 1 END) as PendingRequests,
        COUNT(CASE WHEN Status = 'InProgress' THEN 1 END) as InProgressRequests,
        AVG(DATEDIFF(DAY, CreatedOn, COALESCE(UpdatedOn, GETDATE()))) as AvgProcessingDays
    FROM [request].[Requests]
    WHERE CreatedOn >= @StartDate 
        AND CreatedOn <= @EndDate
        AND (@PropertyType IS NULL OR PropertyType = @PropertyType)
        
END
GO

GRANT EXECUTE ON [request].[sp_GetRequestMetrics] TO [db_executor]
GO
```

## Testing & Validation

### Unit Tests
Create basic unit tests to validate:
1. Database project builds successfully
2. Configuration files are valid JSON
3. SQL script syntax is correct
4. Template files contain required placeholders

### Integration Tests
1. Verify scripts can be executed against a test database
2. Validate that views return expected columns
3. Test stored procedures with sample parameters
4. Ensure functions return correct data types

### Manual Testing Checklist
- [ ] Database project builds without errors
- [ ] All folder structure is created correctly
- [ ] Configuration files are properly formatted
- [ ] Template files contain all required placeholders
- [ ] Sample scripts have correct syntax
- [ ] Project is properly added to solution file

## Acceptance Criteria

### Must Have
- [x] Database project structure created with proper folder organization
- [x] Database.csproj file configured with necessary NuGet packages
- [x] Project added to solution file
- [x] Configuration files created for different environments
- [x] Script templates created for views, stored procedures, and functions
- [x] Sample database objects created for Request module
- [x] All files use consistent naming conventions and formatting

### Should Have
- [ ] Build script that validates SQL syntax
- [ ] PowerShell utilities for creating new objects from templates
- [ ] Documentation for folder structure and naming conventions

### Nice to Have
- [ ] VS Code tasks for common operations
- [ ] SQL formatting and linting configuration
- [ ] Integration with existing logging framework

## Potential Issues & Solutions

### Issue 1: SQL Script Syntax Validation
**Problem**: No compile-time validation of SQL scripts  
**Solution**: Use SqlCmd or SQL Server Database Project tools for validation

### Issue 2: Connection String Management
**Problem**: Different connection strings for different environments  
**Solution**: Use configuration files and environment-specific appsettings

### Issue 3: Schema Dependencies
**Problem**: Database objects may depend on tables from different modules  
**Solution**: Document dependencies and ensure proper deployment order

## Handoff Notes

### For Next Task (Migration Framework Implementation)
- Database project structure is ready for DbUp integration
- Configuration files are prepared for different environments
- Sample scripts demonstrate the expected format and structure
- Templates are available for creating new database objects

### Key Files Created
- `/Database/Database.csproj` - Main project file
- `/Database/Configuration/appsettings.Database.json` - Environment configurations
- `/Database/Tools/Templates/*.sql` - Script templates
- `/Database/Scripts/**/*.sql` - Sample database objects

### Next Steps
1. Implement DbUp migration framework using the created structure
2. Add build validation for SQL scripts
3. Create PowerShell utilities for script generation
4. Integrate with CI/CD pipeline

## Time Tracking
- **Estimated**: 4-6 hours
- **Actual**: 2 hours
- **Variance**: -2 to -4 hours (completed ahead of schedule)

## Implementation Notes
**Completed**: 2025-07-31 by Claude

### Key Accomplishments
- ✅ Created complete Database project structure with all 24 required directories
- ✅ Database.csproj builds successfully with DbUp packages (.NET 9.0)
- ✅ Added to solution file under "Database" solution folder for better organization
- ✅ Created environment-specific configuration (appsettings.Database.json)
- ✅ Built SQL script templates with parameterized placeholders for all object types
- ✅ Implemented sample database objects for Request module (vw_RequestSummary, sp_GetRequestMetrics)
- ✅ All validation tests passed (build, JSON syntax, template placeholders)

### Implementation Highlights
- **Solution Organization**: Database project properly nested under "Database" solution folder
- **Modular Structure**: Scripts organized by module (Request, Document, Assignment, Auth, Notification, Shared)
- **Template System**: Ready-to-use templates for Views, StoredProcedures, and Functions
- **Configuration**: Environment-specific settings prepared for Dev/Staging/Production
- **Sample Objects**: Working examples reference existing EF Core tables

### Ready for Next Task
The Database project foundation is complete and ready for **Migration Framework Implementation**. All dependencies are satisfied and the structure supports the planned DbUp integration.