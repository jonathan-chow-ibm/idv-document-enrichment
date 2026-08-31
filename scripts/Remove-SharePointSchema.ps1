<#
.SYNOPSIS
    Removes the IDV taxonomy v4 site columns, site content types, and list content types
    created by Provision-SharePointSchema.ps1. Run this to reset a site before re-provisioning.

    Deletion order matters:
      1. Remove content types from the library (list-level CTs)
      2. Delete list-level orphan columns from the library (created by old broken runs)
      3. Delete the site-level content types (group: IDV Document Types)
      4. Delete the site columns (matched by internal name)

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.PARAMETER DocumentLibraryName
    Display name of the target document library. Default: "Documents"

.EXAMPLE
    .\Remove-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -DocumentLibraryName "Test"
#>

param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [string]$DocumentLibraryName = "Documents"
)

$ErrorActionPreference = "Stop"

Connect-MgGraph -Scopes "Sites.ReadWrite.All", "Sites.Manage.All"

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

# --- Find the library list ---
$lists = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists"
$docList = $lists.value | Where-Object { $_.displayName -eq $DocumentLibraryName }
if (-not $docList) {
    throw "Library '$DocumentLibraryName' not found. Available: $($lists.value.name -join ', ')"
}
$docListId = $docList.id
Write-Host "Library: $DocumentLibraryName (listId: $docListId)" -ForegroundColor Cyan

$ctGroup = "IDV Document Types"

# Column internal names to remove (must match $columns in Provision-SharePointSchema.ps1)
$colNames = @(
    "DocumentType", "AIConfidence", "AIProcessingStatus", "AIClassifiedDate",
    "SuggestedFields", "AIOriginalClassification", "SourceSystem",
    "State", "PropertyName", "ProjectName",
    "DocumentStatus", "Counterparty", "TransactionType",
    "ExecutionDate", "EffectiveDate", "ExpirationDate",
    "PropertyAddress", "ParcelID", "County", "CityJurisdiction",
    "Acres", "SquareFootage", "LandUse", "Zoning", "OpportunityZone",
    "Seller", "Buyer", "Broker", "TitleCompany", "ClosingDate",
    "PurchasePrice", "EarnestMoney", "DepositAmount", "ContractValue",
    "EntityName", "InvestorFund",
    "Discipline", "SheetNumber", "DrawingTitle"
)

# --- Step 1: Remove IDV content types from the library ---
Write-Host "`n=== Step 1: Remove Content Types from Library ===" -ForegroundColor Cyan
$libCTs = (Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes").value
$libIDVCTs = $libCTs | Where-Object { $_.group -eq $ctGroup }

if ($libIDVCTs.Count -eq 0) {
    Write-Host "  No '$ctGroup' content types found in library — skipping." -ForegroundColor DarkGray
} else {
    foreach ($ct in $libIDVCTs) {
        Write-Host "  Removing '$($ct.name)' from library..." -NoNewline
        Invoke-MgGraphRequest -Method DELETE `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/$($ct.id)" | Out-Null
        Write-Host " [DONE]" -ForegroundColor Green
    }
}

# --- Step 2: Delete orphan list-level columns from the library ---
# These were created by script runs before the column-level fix (when columns were created
# at list scope instead of site scope). They have different GUIDs from the site columns.
Write-Host "`n=== Step 2: Delete Orphan List Columns from Library ===" -ForegroundColor Cyan
$listColumns = (Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value

$listDeleted = 0; $listNotFound = 0
foreach ($colName in $colNames) {
    $col = $listColumns | Where-Object { $_.name -eq $colName }
    if ($col) {
        Write-Host "  Deleting list column '$($col.displayName)'..." -NoNewline
        Invoke-MgGraphRequest -Method DELETE `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns/$($col.id)" | Out-Null
        Write-Host " [DONE]" -ForegroundColor Green
        $listDeleted++
    } else {
        $listNotFound++
    }
}
if ($listNotFound -eq $colNames.Count) {
    Write-Host "  No orphan list columns found — skipping." -ForegroundColor DarkGray
} else {
    Write-Host "  $listDeleted deleted, $listNotFound not present on list." -ForegroundColor DarkGray
}

# --- Step 3: Delete site-level content types ---
Write-Host "`n=== Step 3: Delete Site Content Types ===" -ForegroundColor Cyan
$siteCTs = (Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes").value
$idvSiteCTs = $siteCTs | Where-Object { $_.group -eq $ctGroup }

if ($idvSiteCTs.Count -eq 0) {
    Write-Host "  No '$ctGroup' site content types found — skipping." -ForegroundColor DarkGray
} else {
    foreach ($ct in $idvSiteCTs) {
        Write-Host "  Deleting site CT '$($ct.name)'..." -NoNewline
        Invoke-MgGraphRequest -Method DELETE `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$($ct.id)" | Out-Null
        Write-Host " [DONE]" -ForegroundColor Green
    }
}

# --- Step 4: Delete site columns ---
Write-Host "`n=== Step 4: Delete Site Columns ===" -ForegroundColor Cyan
$siteColumns = (Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns").value

$deleted = 0; $notFound = 0
foreach ($colName in $colNames) {
    $col = $siteColumns | Where-Object { $_.name -eq $colName }
    if ($col) {
        Write-Host "  Deleting '$($col.displayName)'..." -NoNewline
        Invoke-MgGraphRequest -Method DELETE `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns/$($col.id)" | Out-Null
        Write-Host " [DONE]" -ForegroundColor Green
        $deleted++
    } else {
        Write-Host "  [NOT FOUND] $colName" -ForegroundColor DarkGray
        $notFound++
    }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Library CTs removed      : $($libIDVCTs.Count)"
Write-Host "  Library columns deleted  : $listDeleted"
Write-Host "  Site CTs deleted         : $($idvSiteCTs.Count)"
Write-Host "  Site columns deleted     : $deleted  ($notFound not found)"
Write-Host ""
Write-Host "Ready to re-run Provision-SharePointSchema.ps1." -ForegroundColor Green
