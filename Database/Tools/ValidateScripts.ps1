param(
    [string]$ScriptsPath = "Scripts",
    [string]$ConnectionString = ""
)

$ErrorCount = 0
$WarningCount = 0

Write-Host "Validating SQL scripts in: $ScriptsPath" -ForegroundColor Yellow

# Get all SQL files
$SqlFiles = Get-ChildItem -Path $ScriptsPath -Filter "*.sql" -Recurse

foreach ($File in $SqlFiles) {
    Write-Host "Validating: $($File.Name)" -NoNewline
    
    $Content = Get-Content $File.FullName -Raw
    
    # Basic syntax checks
    $Issues = @()
    
    # Check for common issues
    if ($Content -match "SELECT \*" -and $File.Name -like "*View*") {
        $Issues += "WARNING: SELECT * found in view"
        $WarningCount++
    }
    
    if ($Content -notmatch "GO\s*$") {
        $Issues += "WARNING: Missing GO statement at end"
        $WarningCount++
    }
    
    if ($Content -notmatch "GRANT\s+(SELECT|EXECUTE)") {
        $Issues += "WARNING: Missing permission grants"
        $WarningCount++
    }
    
    if ($Content -match "SET NOCOUNT ON" -and $File.Name -notlike "*StoredProcedure*") {
        $Issues += "INFO: SET NOCOUNT ON in non-procedure"
    }
    
    # SQL syntax validation (if connection string provided)
    if ($ConnectionString) {
        try {
            $Connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
            $Connection.Open()
            
            $Command = $Connection.CreateCommand()
            $Command.CommandText = "SET PARSEONLY ON; $Content; SET PARSEONLY OFF;"
            $Command.ExecuteNonQuery() | Out-Null
            
            $Connection.Close()
        }
        catch {
            $Issues += "ERROR: SQL syntax error - $($_.Exception.Message)"
            $ErrorCount++
        }
    }
    
    if ($Issues.Count -eq 0) {
        Write-Host " ✓" -ForegroundColor Green
    }
    else {
        Write-Host " ✗" -ForegroundColor Red
        foreach ($Issue in $Issues) {
            $Color = if ($Issue.StartsWith("ERROR")) { "Red" } 
                    elseif ($Issue.StartsWith("WARNING")) { "Yellow" } 
                    else { "Cyan" }
            Write-Host "    $Issue" -ForegroundColor $Color
        }
    }
}

Write-Host "`nValidation Summary:" -ForegroundColor Yellow
Write-Host "  Files processed: $($SqlFiles.Count)"
Write-Host "  Errors: $ErrorCount" -ForegroundColor $(if ($ErrorCount -gt 0) { "Red" } else { "Green" })
Write-Host "  Warnings: $WarningCount" -ForegroundColor $(if ($WarningCount -gt 0) { "Yellow" } else { "Green" })

if ($ErrorCount -gt 0) {
    exit 1
}