<#
.SYNOPSIS
    Audits an entire SharePoint site for IDV taxonomy columns and content types —
    at both the site level and across every list/library. Use this before
    Remove-SharePointSchema.ps1 to confirm what exists and where.

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.EXAMPLE
    .\Find-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects"
#>

param(
    [Parameter(Mandatory)]
    [string]$SiteUrl
)

$ErrorActionPreference = "Stop"

Connect-MgGraph -Scopes "Sites.ReadWrite.All", "Sites.Manage.All"

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

$ctGroup  = "IDV Document Types"
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

# --- Site-level columns ---
Write-Host "`n=== Site Columns ===" -ForegroundColor Cyan
$siteColumns = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns").value
$foundSiteCols = $siteColumns | Where-Object { $colNames -contains $_.name }
if ($foundSiteCols) {
    foreach ($c in $foundSiteCols) {
        Write-Host ("  [FOUND] {0,-30} id={1}  group={2}" -f $c.displayName, $c.id, $c.group) -ForegroundColor Yellow
    }
} else {
    Write-Host "  None found." -ForegroundColor DarkGray
}

# --- Site-level content types ---
Write-Host "`n=== Site Content Types (group: $ctGroup) ===" -ForegroundColor Cyan
$siteCTs = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes").value
$foundSiteCTs = $siteCTs | Where-Object { $_.group -eq $ctGroup }
if ($foundSiteCTs) {
    foreach ($ct in $foundSiteCTs) {
        Write-Host ("  [FOUND] {0,-20} id={1}" -f $ct.name, $ct.id) -ForegroundColor Yellow
    }
} else {
    Write-Host "  None found." -ForegroundColor DarkGray
}

# --- Scan every list for IDV columns and content types ---
Write-Host "`n=== Per-List Scan ===" -ForegroundColor Cyan
$lists = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists").value

foreach ($list in $lists) {
    $listId   = $list.id
    $listName = $list.displayName

    # List-level columns matching our names
    $listCols = (Invoke-MgGraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$listId/columns").value
    $matchCols = $listCols | Where-Object { $colNames -contains $_.name }

    # List-level content types in our group
    $listCTs = (Invoke-MgGraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$listId/contentTypes").value
    $matchCTs = $listCTs | Where-Object { $_.group -eq $ctGroup }

    if ($matchCols -or $matchCTs) {
        Write-Host ""
        Write-Host "  List: $listName ($listId)" -ForegroundColor White
        foreach ($c in $matchCols) {
            Write-Host ("    [COLUMN] {0,-30} id={1}" -f $c.displayName, $c.id) -ForegroundColor Yellow
        }
        foreach ($ct in $matchCTs) {
            Write-Host ("    [CT]     {0,-20} id={1}" -f $ct.name, $ct.id) -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "Scan complete." -ForegroundColor Green
