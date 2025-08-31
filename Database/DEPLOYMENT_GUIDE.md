# Database Deployment Guide

## Quick Start

### 1. Check CLI is Working
```bash
dotnet run --project Database/Database.csproj help
```

### 2. Set Connection String
```bash
# For Development (example - adjust for your setup)
export DATABASE_CONNECTION_STRING="Server=(localdb)\\mssqllocaldb;Database=CollateralAppraisalSystem;Trusted_Connection=true;MultipleActiveResultSets=true"

# Alternative: Set in appsettings
# Edit Database/Configuration/appsettings.Database.json
```

### 3. Deploy to Development
```bash
dotnet run --project Database/Database.csproj migrate Development
```

## Available Commands

### Migration Commands
```bash
# Run all migrations (EF Core + DbUp: tables, views, stored procedures, functions)
dotnet run --project Database/Database.csproj migrate [Development|Test|Staging|Production]

# Validate all pending migrations
dotnet run --project Database/Database.csproj validate

# Show migration history (combined)
dotnet run --project Database/Database.csproj history

# Generate rollback script
dotnet run --project Database/Database.csproj generate-rollback 1.0.0 rollback.sql

# Rollback to specific version
dotnet run --project Database/Database.csproj rollback 1.0.0
```

### EF Core Specific Commands
```bash
# Show EF Core migration status by DbContext
dotnet run --project Database/Database.csproj efcore-status

# Validate EF Core migrations only
dotnet run --project Database/Database.csproj efcore-validate
```

## How Integration Works

The Database project now supports both **EF Core** and **DbUp** migrations in a coordinated fashion:

### Migration Order
1. **EF Core migrations** run first (tables, relationships, constraints)
2. **DbUp migrations** run second (views, stored procedures, functions)

This ensures that:
- Entity tables exist before database objects reference them
- Schema changes are applied in dependency order
- Both migration systems work together seamlessly

### DbContexts Included
- **RequestDbContext** - Request management entities
- **DocumentDbContext** - Document storage entities  
- **AssignmentDbContext** - Task assignment entities
- **NotificationDbContext** - Notification system entities
- **OpenIddictDbContext** - Authentication and authorization entities

## What Gets Deployed

The migration will create:

### Views
- `[request].[vw_Request_Summary]` - Request information with metrics
- `[request].[vw_Request_Dashboard]` - Dashboard metrics
- `[document].[vw_Document_Summary]` - Document metadata
- `[assignment].[vw_Assignment_TaskMetrics]` - Task workload analysis

### Stored Procedures
- `[request].[sp_Request_GetMetrics]` - Comprehensive request reporting
- `[document].[sp_Document_CleanupExpired]` - Document retention management

### Functions
- `[request].[fn_Request_CalculateAge]` - Business day age calculation

## Connection String Examples

### LocalDB (Development)
```bash
export DATABASE_CONNECTION_STRING="Server=(localdb)\\mssqllocaldb;Database=CollateralAppraisalSystem;Trusted_Connection=true;MultipleActiveResultSets=true"
```

### SQL Server Express
```bash
export DATABASE_CONNECTION_STRING="Server=localhost\\SQLEXPRESS;Database=CollateralAppraisalSystem;Trusted_Connection=true;MultipleActiveResultSets=true"
```

### Azure SQL Database
```bash
export DATABASE_CONNECTION_STRING="Server=your-server.database.windows.net;Database=CollateralAppraisalSystem;User Id=your-user;Password=your-password;Encrypt=true"
```

## Troubleshooting

### "No connection string configured"
Set the DATABASE_CONNECTION_STRING environment variable or update appsettings.Database.json

### "Database does not exist"
Create the database first:
```sql
CREATE DATABASE [CollateralAppraisalSystem];
```

### Check Migration Status
```bash
dotnet run --project Database/Database.csproj history
```

### Validate Pipeline
```bash
# Basic validation (no DB required)
./Database/Tools/validate-pipeline-basic.sh

# SQL script validation
./Database/Tools/validate-sql-scripts.sh

# EF Core specific validation
dotnet run --project Database/Database.csproj efcore-validate
```

### EF Core Troubleshooting
```bash
# Check EF Core migration status
dotnet run --project Database/Database.csproj efcore-status

# Check if DbContexts are registered properly
dotnet build Database/Database.csproj

# If EF Core migrations fail, check individual contexts
dotnet ef migrations list --project Modules/Request/Request
dotnet ef migrations list --project Modules/Document/Document
```

## CI/CD Integration

### GitHub Actions
- Push to `develop` → Auto-deploy to Development/Test
- Push to `main` → Deploy to Staging/Production (with approvals)

### Azure DevOps
- Uses `azure-pipelines-database.yml`
- Multi-stage deployment with approval gates

## Next Steps

1. **Test locally**: Deploy to Development environment
2. **Set up CI/CD**: Configure your pipeline platform
3. **Production deployment**: Use proper connection strings and approvals
4. **Monitor**: Use the monitoring tools in Database/Tools/