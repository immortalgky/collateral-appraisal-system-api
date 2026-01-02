param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("View", "StoredProcedure", "Function")]
    [string]$ObjectType,
    
    [Parameter(Mandatory=$true)]
    [ValidateSet("Request", "Document", "Assignment", "Auth", "Notification", "Shared")]
    [string]$Module,
    
    [Parameter(Mandatory=$true)]
    [string]$ObjectName,
    
    [string]$Description = "",
    [string]$Author = $env:USERNAME
)

$Date = Get-Date -Format "yyyy-MM-dd"
$SchemaName = $Module.ToLower()

# Determine folder and file naming
switch ($ObjectType) {
    "View" { 
        $Folder = "Views"
        $FileName = "vw_${Module}_${ObjectName}.sql"
        $TemplateFile = "Tools\Templates\View.sql"
    }
    "StoredProcedure" { 
        $Folder = "StoredProcedures"
        $FileName = "sp_${Module}_${ObjectName}.sql"
        $TemplateFile = "Tools\Templates\StoredProcedure.sql"
    }
    "Function" { 
        $Folder = "Functions"
        $FileName = "fn_${Module}_${ObjectName}.sql"
        $TemplateFile = "Tools\Templates\Function.sql"
    }
}

$OutputPath = "Scripts\$Folder\$Module\$FileName"

# Read template
if (!(Test-Path $TemplateFile)) {
    Write-Error "Template file not found: $TemplateFile"
    exit 1
}

$Template = Get-Content $TemplateFile -Raw

# Replace placeholders
$Script = $Template -replace '\{ViewName\}', "vw_${Module}_${ObjectName}" `
                   -replace '\{ProcedureName\}', "sp_${Module}_${ObjectName}" `
                   -replace '\{FunctionName\}', "fn_${Module}_${ObjectName}" `
                   -replace '\{SchemaName\}', $SchemaName `
                   -replace '\{Description\}', $Description `
                   -replace '\{Date\}', $Date `
                   -replace '\{Author\}', $Author `
                   -replace '\{ReturnType\}', 'INT'  # Default return type

# Ensure directory exists
$Directory = Split-Path $OutputPath -Parent
if (!(Test-Path $Directory)) {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
}

# Write file
$Script | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host "Created $ObjectType`: $OutputPath" -ForegroundColor Green
Write-Host "Remember to:"
Write-Host "  1. Update the object definition"
Write-Host "  2. Add appropriate parameters and logic"
Write-Host "  3. Test the object before deployment"
Write-Host "  4. Add to source control"