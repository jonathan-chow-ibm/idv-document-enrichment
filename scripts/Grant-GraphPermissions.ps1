<#
.SYNOPSIS
    Grants Microsoft Graph API permissions to the Function App's Managed Identity
    for SharePoint access. Run after deploying the Bicep infrastructure.

.DESCRIPTION
    Supports two approaches:
      -FullAccess    → Sites.ReadWrite.All (all sites, simpler setup)
      -SiteSelected  → Sites.Selected (one site, requires site-level grant)

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

.PARAMETER Approach
    "FullAccess" or "SiteSelected".

.PARAMETER SiteUrl
    SharePoint site URL (required for SiteSelected approach).
    Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.EXAMPLE
    # Full access (all SharePoint sites)
    .\Grant-GraphPermissions.ps1 -FunctionAppName func-idv-enrich-dev -ResourceGroupName rg-idv-enrichment -Approach FullAccess

    # Scoped to one site
    .\Grant-GraphPermissions.ps1 -FunctionAppName func-idv-enrich-dev -ResourceGroupName rg-idv-enrichment -Approach SiteSelected -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects"
#>

param(
    [Parameter(Mandatory)]
    [string]$FunctionAppName,

    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory)]
    [ValidateSet("FullAccess", "SiteSelected")]
    [string]$Approach,

    [string]$SiteUrl
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

$scopes = if ($Approach -eq "SiteSelected") {
    "AppRoleAssignment.ReadWrite.All", "Sites.FullControl.All"
} else {
    "AppRoleAssignment.ReadWrite.All"
}

Connect-MgGraph -Scopes $scopes
Write-Host "  Connected."

# --- Step 3: Get Graph service principal and target role ---
Write-Host "`n=== Step 3: Granting Graph API permission ===" -ForegroundColor Cyan

$graphSp = Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'"

$roleName = if ($Approach -eq "FullAccess") { "Sites.ReadWrite.All" } else { "Sites.Selected" }
$role = $graphSp.AppRoles | Where-Object { $_.Value -eq $roleName }

if (-not $role) {
    throw "Could not find Graph API role: $roleName"
}

# Check if already assigned
$existing = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId |
    Where-Object { $_.AppRoleId -eq $role.Id }

if ($existing) {
    Write-Host "  $roleName is already assigned. Skipping." -ForegroundColor Yellow
} else {
    New-MgServicePrincipalAppRoleAssignment `
        -ServicePrincipalId $miObjectId `
        -PrincipalId $miObjectId `
        -ResourceId $graphSp.Id `
        -AppRoleId $role.Id | Out-Null

    Write-Host "  Granted: $roleName" -ForegroundColor Green
}

# Grant Files.ReadWrite.All (required for @microsoft.graph.downloadUrl on DriveItems)
$filesRole = $graphSp.AppRoles | Where-Object { $_.Value -eq "Files.ReadWrite.All" }
if (-not $filesRole) {
    throw "Could not find Graph API role: Files.ReadWrite.All"
}
$existingFiles = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId |
    Where-Object { $_.AppRoleId -eq $filesRole.Id }

if ($existingFiles) {
    Write-Host "  Files.ReadWrite.All is already assigned. Skipping." -ForegroundColor Yellow
} else {
    New-MgServicePrincipalAppRoleAssignment `
        -ServicePrincipalId $miObjectId `
        -PrincipalId $miObjectId `
        -ResourceId $graphSp.Id `
        -AppRoleId $filesRole.Id | Out-Null
    Write-Host "  Granted: Files.ReadWrite.All" -ForegroundColor Green
}

# --- Step 4: Verify permissions ---
Write-Host "`n=== Step 4: Verify permissions ===" -ForegroundColor Cyan
Write-Host "  Permissions have been granted. To verify, go to:"
Write-Host "  Entra ID -> Enterprise Applications -> '$FunctionAppName' -> Permissions"
Write-Host "  The granted permissions should appear under 'Application permissions'."
Write-Host ""

if ($Approach -eq "FullAccess") {
    Write-Host "=== Setup complete (FullAccess) ===" -ForegroundColor Green
    Write-Host "  The Function App can read/write any SharePoint site."
    Write-Host "  No further permission steps needed."
    return
}

# --- Step 5 (SiteSelected only): Grant site-level write access ---
Write-Host "`n=== Step 5: Granting site-level write access ===" -ForegroundColor Cyan

if (-not $SiteUrl) {
    throw "-SiteUrl is required for SiteSelected approach. Example: 'contoso.sharepoint.com:/sites/ActiveProjects'"
}

# Resolve site ID from URL
$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "  Resolved site: $($site.displayName) ($siteId)"

# Check existing permissions
$existingPerms = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/permissions"
$alreadyGranted = $existingPerms.value | Where-Object {
    $_.grantedToIdentitiesV2.application.id -eq $miAppId
}

if ($alreadyGranted) {
    Write-Host "  Site-level permission already granted. Skipping." -ForegroundColor Yellow
} else {
    $body = @{
        roles = @("write")
        grantedToIdentitiesV2 = @(
            @{
                application = @{
                    id = $miAppId
                    displayName = $FunctionAppName
                }
            }
        )
    }

    Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/permissions" `
        -Body ($body | ConvertTo-Json -Depth 5) `
        -ContentType "application/json" | Out-Null

    Write-Host "  Granted: write access on $($site.displayName)" -ForegroundColor Green
}

# --- Step 6: Verify access ---
Write-Host "`n=== Step 6: Verifying access ===" -ForegroundColor Cyan
Write-Host "  NOTE: Verification uses YOUR credentials, not the Managed Identity."
Write-Host "  The Managed Identity will use these permissions at runtime via DefaultAzureCredential."
Write-Host ""

try {
    $drives = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/drives"
    Write-Host "  Site has $($drives.value.Count) document library/libraries:" -ForegroundColor Green
    foreach ($d in $drives.value) {
        Write-Host "    - $($d.name) (driveId: $($d.id))"
    }
} catch {
    Write-Host "  Could not list drives: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  This may be expected if permissions have not yet propagated."
}

Write-Host "`n=== Setup complete (SiteSelected) ===" -ForegroundColor Green
Write-Host "  The Function App can read/write the granted site."
Write-Host "  To add another site, re-run with a different -SiteUrl."
