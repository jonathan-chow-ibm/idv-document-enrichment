<#
.SYNOPSIS
    Grants Sites.ReadWrite.All to the Function App's Managed Identity for SharePoint access.
    Run after deploying the Bicep infrastructure.

.DESCRIPTION
    Grants the Sites.ReadWrite.All application permission to the system-assigned Managed
    Identity of the specified Azure Function App. This allows the Function App to read
    and write any SharePoint site in the tenant.

    PREREQUISITES:
      1. Azure CLI installed and authenticated: az login
      2. Microsoft.Graph PowerShell module:
            Install-Module Microsoft.Graph -Scope CurrentUser
      3. Entra ID role: Global Administrator or Privileged Role Administrator
         (required to consent AppRoleAssignment.ReadWrite.All)
      Note: The script prompts for Microsoft Graph authentication separately
            from az login — you will be asked to sign in twice.

.PARAMETER FunctionAppName
    Name of the deployed Azure Function App.

.PARAMETER ResourceGroupName
    Resource group containing the Function App.

.PARAMETER Force
    Skip the confirmation prompt.

.EXAMPLE
    .\Grant-GraphPermissions.ps1 -FunctionAppName func-idv-doc-enrich-dev -ResourceGroupName rg-idv-enrichment
    .\Grant-GraphPermissions.ps1 -FunctionAppName func-idv-doc-enrich-dev -ResourceGroupName rg-idv-enrichment -Force
#>

param(
    [Parameter(Mandatory)]
    [string]$FunctionAppName,

    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

# --- Step 1: Get Managed Identity info ---
Write-Host "`n=== Step 1: Retrieving Managed Identity ===" -ForegroundColor Cyan

$miObjectId = az functionapp identity show `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --query principalId -o tsv

if (-not $miObjectId) {
    throw "Could not retrieve Managed Identity. Is the Function App deployed with a system-assigned identity?"
}

$maxRetries = 5
$miAppId = $null
for ($i = 1; $i -le $maxRetries; $i++) {
    $miAppId = az ad sp show --id $miObjectId --query appId -o tsv 2>$null
    if ($miAppId) { break }
    Write-Host "  Managed Identity not yet visible in Entra ID, retrying ($i/$maxRetries)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
}
if (-not $miAppId) {
    throw "Could not retrieve App ID for Managed Identity after $maxRetries retries. Ensure the Function App is deployed and try again in a minute."
}

Write-Host "  Managed Identity Object ID : $miObjectId"
Write-Host "  Managed Identity App ID    : $miAppId"

# --- Step 2: Connect to Microsoft Graph ---
Write-Host "`n=== Step 2: Connecting to Microsoft Graph ===" -ForegroundColor Cyan

Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All", "Application.Read.All"
Write-Host "  Connected."

# --- Confirmation ---
Write-Host "`n=== Permission Summary ===" -ForegroundColor Yellow
Write-Host "  This script will grant the following application permissions to '$FunctionAppName':"
Write-Host "    - Sites.ReadWrite.All  (read/write access to ALL SharePoint sites in the tenant)"
Write-Host ""
Write-Host "  These are tenant-wide permissions. Ensure you have authorization to grant them." -ForegroundColor Yellow

if (-not $Force) {
    $confirm = Read-Host "  Type 'yes' to proceed"
    if ($confirm -ne "yes") {
        Write-Host "  Aborted." -ForegroundColor Red
        exit 0
    }
}

# --- Step 3: Granting Graph API permission ---
Write-Host "`n=== Step 3: Granting Graph API permission ===" -ForegroundColor Cyan

$graphSp = Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'"

$role = $graphSp.AppRoles | Where-Object { $_.Value -eq "Sites.ReadWrite.All" }
if (-not $role) {
    throw "Could not find Graph API role: Sites.ReadWrite.All"
}

$existingAssignments = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId
$existing = $existingAssignments | Where-Object { $_.AppRoleId -eq $role.Id }

if ($existing) {
    Write-Host "  Sites.ReadWrite.All is already assigned. Skipping." -ForegroundColor Yellow
} else {
    New-MgServicePrincipalAppRoleAssignment `
        -ServicePrincipalId $miObjectId `
        -PrincipalId $miObjectId `
        -ResourceId $graphSp.Id `
        -AppRoleId $role.Id | Out-Null

    Write-Host "  Granted: Sites.ReadWrite.All" -ForegroundColor Green
}

# --- Step 4: Verify permissions ---
Write-Host "`n=== Step 4: Verify permissions ===" -ForegroundColor Cyan
Write-Host "  Permissions have been granted. To verify, go to:"
Write-Host "  Entra ID -> Enterprise Applications -> '$FunctionAppName' -> Permissions"
Write-Host "  The granted permissions should appear under 'Application permissions'."
Write-Host ""

Disconnect-MgGraph

Write-Host "=== Setup complete ===" -ForegroundColor Green
Write-Host "  The Function App can read/write any SharePoint site."
Write-Host "  No further permission steps needed."
