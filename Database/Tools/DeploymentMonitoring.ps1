param(
    [string]$Environment = "Production",
    [string]$ConnectionString = "",
    [string]$WebhookUrl = "",
    [int]$AlertThresholdMinutes = 30
)

# Get recent deployment activity
function Get-RecentDeployments {
    param($Connection, $ThresholdMinutes)
    
    $sql = @"
SELECT 
    ScriptName,
    ExecutedOn,
    ExecutionTimeMs,
    Success,
    ErrorMessage,
    Environment
FROM dbo.DatabaseMigrationHistory 
WHERE ExecutedOn >= DATEADD(MINUTE, -@Threshold, GETDATE())
ORDER BY ExecutedOn DESC
"@
    
    $command = $Connection.CreateCommand()
    $command.CommandText = $sql
    $command.Parameters.AddWithValue("@Threshold", $ThresholdMinutes) | Out-Null
    
    $reader = $command.ExecuteReader()
    $deployments = @()
    
    while ($reader.Read()) {
        $deployments += @{
            ScriptName = $reader["ScriptName"]
            ExecutedOn = $reader["ExecutedOn"]
            ExecutionTimeMs = $reader["ExecutionTimeMs"]
            Success = $reader["Success"]
            ErrorMessage = if ($reader["ErrorMessage"] -eq [DBNull]::Value) { $null } else { $reader["ErrorMessage"] }
            Environment = $reader["Environment"]
        }
    }
    
    $reader.Close()
    return $deployments
}

# Send alert notification
function Send-Alert {
    param($Message, $Severity = "INFO")
    
    $payload = @{
        text = "$Severity: $Message"
        timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        environment = $Environment
    } | ConvertTo-Json
    
    if (-not [string]::IsNullOrEmpty($WebhookUrl)) {
        try {
            Invoke-RestMethod -Uri $WebhookUrl -Method Post -Body $payload -ContentType "application/json"
        } catch {
            Write-Warning "Failed to send webhook notification: $($_.Exception.Message)"
        }
    }
    
    # Also log to console
    $color = switch ($Severity) {
        "ERROR" { "Red" }
        "WARNING" { "Yellow" }
        default { "Green" }
    }
    Write-Host "[$Severity] $Message" -ForegroundColor $color
}

# Main monitoring logic
try {
    if ([string]::IsNullOrEmpty($ConnectionString)) {
        $ConnectionString = switch ($Environment.ToLower()) {
            "production" { $env:PRODUCTION_CONNECTION_STRING }
            "staging" { $env:STAGING_CONNECTION_STRING }
            "test" { $env:TEST_CONNECTION_STRING }
            "development" { $env:DEV_CONNECTION_STRING }
        }
    }
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $connection.Open()
    
    $recentDeployments = Get-RecentDeployments -Connection $connection -ThresholdMinutes $AlertThresholdMinutes
    
    if ($recentDeployments.Count -eq 0) {
        Send-Alert "No recent deployments found in the last $AlertThresholdMinutes minutes" "INFO"
    } else {
        $failedDeployments = $recentDeployments | Where-Object { -not $_.Success }
        $slowDeployments = $recentDeployments | Where-Object { $_.ExecutionTimeMs -gt 300000 }  # > 5 minutes
        
        Send-Alert "Found $($recentDeployments.Count) recent deployments" "INFO"
        
        if ($failedDeployments.Count -gt 0) {
            foreach ($failed in $failedDeployments) {
                Send-Alert "Failed deployment: $($failed.ScriptName) - $($failed.ErrorMessage)" "ERROR"
            }
        }
        
        if ($slowDeployments.Count -gt 0) {
            foreach ($slow in $slowDeployments) {
                $minutes = [math]::Round($slow.ExecutionTimeMs / 60000, 2)
                Send-Alert "Slow deployment detected: $($slow.ScriptName) took $minutes minutes" "WARNING"
            }
        }
        
        if ($failedDeployments.Count -eq 0 -and $slowDeployments.Count -eq 0) {
            Send-Alert "All recent deployments completed successfully" "INFO"
        }
    }
    
    $connection.Close()
    
} catch {
    Send-Alert "Monitoring script failed: $($_.Exception.Message)" "ERROR"
    exit 1
}