# Database Migration Tool

This tool handles database object migrations (views, stored procedures, and functions) using DbUp.

## Purpose

- **Views**: Create and update database views
- **Stored Procedures**: Create and update stored procedures
- **Functions**: Create and update database functions
- **Schema Objects**: Any other database objects that aren't tables

## NOT Handled

- **Table Migrations**: Handled by EF Core in individual modules
- **Entity Models**: Managed by each module's DbContext
- **Data Seeding**: Handled by individual modules

## Usage

```bash
# Run migrations
dotnet run --project Database/Database.csproj migrate

# Run migrations for specific environment
dotnet run --project Database/Database.csproj migrate Production

# View migration history
dotnet run --project Database/Database.csproj history

# Validate pending migrations
dotnet run --project Database/Database.csproj validate

# Generate rollback script
dotnet run --project Database/Database.csproj generate-rollback 1.0.0 rollback.sql

# Show help
dotnet run --project Database/Database.csproj help
```

## Configuration

Configuration is in `Configuration/appsettings.Database.json`:

```json
{
  "DatabaseMigration": {
    "EnableMigration": true,
    "MigrationsTableName": "DatabaseMigrationHistory",
    "MigrationsSchema": "dbo",
    "ScriptTimeout": 300,
    "BackupDatabase": true,
    "BackupPath": "",
    "ValidateOnly": false
  }
}
```

## Environment Variables

- `DATABASE_CONNECTION_STRING`: Database connection string
- `ASPNETCORE_ENVIRONMENT`: Environment name (Development/Production/etc.)

## Script Organization

```
Scripts/
├── Views/
│   ├── Request/
│   ├── Document/
│   ├── Assignment/
│   ├── Auth/
│   ├── Notification/
│   └── Shared/
├── StoredProcedures/
│   └── [same structure as Views]
└── Functions/
    └── [same structure as Views]
```

## Features

- **Checksum-based re-execution**: Updated scripts are automatically re-run
- **Semantic versioning**: Automatic version extraction from script names
- **Intelligent rollback**: Generate rollback scripts to specific versions
- **Backup support**: Configurable database backup before migrations
- **Transaction safety**: All migrations run in transactions
- **Environment-specific configuration**: Different settings per environment

## Versioning System

The system automatically extracts versions from script names using these patterns:

1. **Semantic versioning prefix**: `v1.2.3_ScriptName.sql` → Version 1.2.3
2. **Numeric prefix**: `001_ScriptName.sql` → Version 0.0.1
3. **Date-based**: `20250801120000_Script.sql` → Version 2025.08.01
4. **Type-based defaults**: Views=1.1.0, Procedures=1.2.0, Functions=1.3.0

## Example Scripts

```sql
-- Scripts/Views/Request/v1.2.0_vw_ActiveRequests.sql
IF OBJECT_ID('[request].[vw_ActiveRequests]', 'V') IS NOT NULL
    DROP VIEW [request].[vw_ActiveRequests];

EXEC('CREATE VIEW [request].[vw_ActiveRequests]
AS
SELECT Id, AppraisalNo, Status, CreatedOn
FROM request.Requests
WHERE Status = ''Active''');
```

## Rollback Scripts

Generate rollback scripts to revert to a specific version:

```bash
# Generate rollback script to version 1.1.0
dotnet run --project Database/Database.csproj generate-rollback 1.1.0 rollback.sql
```

The generated script includes:
- DROP statements for database objects created after the target version
- DELETE statements to clean up migration history
- Comments showing which scripts will be rolled back

## Integration

This tool is designed to work alongside EF Core migrations:

1. **EF Core** handles table schema migrations in each module
2. **This tool** handles views, stored procedures, and functions
3. Both can run independently without conflicts