param(
    [Parameter(Mandatory=$true)]
    [string]$Environment,
    
    [Parameter(Mandatory=$true)]
    [string]$TargetVersion,
    
    [string]$ConnectionString = "",
    [switch]$DryRun,
    [switch]$Force
)

Write-Host "Database Rollback Script" -ForegroundColor Yellow
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "Target Version: $TargetVersion" -ForegroundColor Cyan
Write-Host "Dry Run: $DryRun" -ForegroundColor Cyan

# Get connection string
if ([string]::IsNullOrEmpty($ConnectionString)) {
    $ConnectionString = switch ($Environment.ToLower()) {
        "development" { $env:DEV_CONNECTION_STRING }
        "test" { $env:TEST_CONNECTION_STRING }
        "staging" { $env:STAGING_CONNECTION_STRING }
        "production" { $env:PRODUCTION_CONNECTION_STRING }
        default { throw "Unknown environment: $Environment" }
    }
}

if ([string]::IsNullOrEmpty($ConnectionString)) {
    throw "Connection string not found for environment: $Environment"
}

# Validate target version exists
try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    
    $sql = "SELECT COUNT(*) FROM dbo.DatabaseMigrationHistory WHERE Version = @Version"
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.Parameters.AddWithValue("@Version", $TargetVersion) | Out-Null
    $versionExists = $command.ExecuteScalar()
    
    $connection.Close()
    
    if ($versionExists -eq 0) {
        throw "Target version '$TargetVersion' not found in migration history"
    }
} catch {
    throw "Failed to validate target version: $($_.Exception.Message)"
}

# Get migrations to rollback
try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    
    $sql = @"
SELECT ScriptName, Version, ExecutedOn 
FROM dbo.DatabaseMigrationHistory 
WHERE Version > @TargetVersion 
ORDER BY ExecutedOn DESC
"@
    
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.Parameters.AddWithValue("@TargetVersion", $TargetVersion) | Out-Null
    $reader = $command.ExecuteReader()
    
    $migrationsToRollback = @()
    while ($reader.Read()) {
        $migrationsToRollback += @{
            ScriptName = $reader["ScriptName"]
            Version = $reader["Version"]
            ExecutedOn = $reader["ExecutedOn"]
        }
    }
    
    $reader.Close()
    $connection.Close()
    
    if ($migrationsToRollback.Count -eq 0) {
        Write-Host "No migrations to rollback - database is already at or before target version" -ForegroundColor Green
        exit 0
    }
    
    Write-Host "Found $($migrationsToRollback.Count) migrations to rollback:" -ForegroundColor Yellow
    foreach ($migration in $migrationsToRollback) {
        Write-Host "  $($migration.ScriptName) - $($migration.Version) - $($migration.ExecutedOn)" -ForegroundColor Cyan
    }
    
} catch {
    throw "Failed to retrieve migrations to rollback: $($_.Exception.Message)"
}

# Confirmation for production
if ($Environment.ToLower() -eq "production" -and -not $Force) {
    Write-Host "WARNING: This will rollback the PRODUCTION database!" -ForegroundColor Red
    Write-Host "This operation will:" -ForegroundColor Yellow
    Write-Host "  - Drop or modify database objects" -ForegroundColor Yellow
    Write-Host "  - Potentially cause data loss" -ForegroundColor Yellow
    Write-Host "  - Affect application functionality" -ForegroundColor Yellow
    
    $confirmation = Read-Host "Type 'ROLLBACK PRODUCTION' to confirm"
    if ($confirmation -ne "ROLLBACK PRODUCTION") {
        Write-Host "Rollback cancelled" -ForegroundColor Green
        exit 0
    }
}

if ($DryRun) {
    Write-Host "DRY RUN - No changes will be made" -ForegroundColor Yellow
    
    # Generate rollback script content
    $rollbackContent = @"
-- Rollback script for $Environment environment
-- Target version: $TargetVersion
-- Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
-- 
-- WARNING: This script will modify database objects and may cause data loss
--

BEGIN TRANSACTION RollbackTransaction;

"@
    
    foreach ($migration in $migrationsToRollback) {
        $rollbackContent += @"

-- Rollback for: $($migration.ScriptName)
-- Version: $($migration.Version)
-- Original execution: $($migration.ExecutedOn)

-- TODO: Add rollback logic for $($migration.ScriptName)
-- This would typically include:
-- - DROP statements for created objects
-- - Restore statements for modified objects
-- - Data restoration if needed

"@
    }
    
    $rollbackContent += @"

-- Update migration history
DELETE FROM dbo.DatabaseMigrationHistory 
WHERE Version > '$TargetVersion';

COMMIT TRANSACTION RollbackTransaction;
"@
    
    $outputFile = "rollback_${Environment}_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
    $rollbackContent | Out-File $outputFile -Encoding UTF8
    
    Write-Host "Rollback script generated: $outputFile" -ForegroundColor Green
    Write-Host "Review the script and execute manually if needed" -ForegroundColor Yellow
    
} else {
    Write-Host "Executing rollback..." -ForegroundColor Yellow
    
    # Create backup before rollback
    Write-Host "Creating backup before rollback..." -ForegroundColor Cyan
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupPath = "CollateralAppraisal_${Environment}_PreRollback_${timestamp}.bak"
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    
    $databaseName = switch ($Environment.ToLower()) {
        "production" { "CollateralAppraisalSystem" }
        default { "CollateralAppraisalSystem_$Environment" }
    }
    
    $sql = "BACKUP DATABASE [$databaseName] TO DISK = '$backupPath' WITH COMPRESSION"
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 1800
    $command.ExecuteNonQuery()
    
    Write-Host "Backup created: $backupPath" -ForegroundColor Green
    
    # Execute rollback using migration service
    try {
        # Use the migration CLI tool for rollback
        $migrationArgs = "rollback $TargetVersion"
        $result = & dotnet run --project Database/Database.csproj -- $migrationArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Migration rollback failed with exit code: $LASTEXITCODE"
        }
        
        Write-Host "Rollback completed successfully" -ForegroundColor Green
        
    } catch {
        Write-Error "Rollback failed: $($_.Exception.Message)"
        Write-Host "Database backup available at: $backupPath" -ForegroundColor Yellow
        exit 1
    } finally {
        if ($connection.State -eq "Open") {
            $connection.Close()
        }
    }
}

Write-Host "Rollback operation completed" -ForegroundColor Green