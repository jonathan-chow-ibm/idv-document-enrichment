<#
.SYNOPSIS
    Provisions the taxonomy v4 metadata columns and 5 content types on a SharePoint
    document library via Microsoft Graph API. Run after Graph permissions are granted.

    Columns and content types mirror docs/taxonomy/taxonomy.yaml (v4) and ADR-009.
    Review is handled inline via a filtered library view on AIProcessingStatus (ADR-006).

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.PARAMETER DocumentLibraryName
    Display name of a single target document library. Mutually exclusive with -AllLibraries.

.PARAMETER AllLibraries
    Discover and provision every visible document library on the site. Mutually exclusive
    with -DocumentLibraryName. System/hidden libraries are skipped automatically.

.PARAMETER DryRun
    If set, shows what would be created without making changes.

.EXAMPLE
    # Single library
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -DocumentLibraryName "Documents"
    # All libraries on the site
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -AllLibraries
    # Dry run across all libraries
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -AllLibraries -DryRun
#>

param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [Parameter(ParameterSetName = "Single")]
    [string]$DocumentLibraryName = "Documents",

    [Parameter(ParameterSetName = "All")]
    [switch]$AllLibraries,

    [Parameter(ParameterSetName = "All")]
    [string[]]$ExcludeLibraries = @(),

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --- Connect ---
Connect-MgGraph -Scopes "Sites.ReadWrite.All", "Sites.Manage.All"

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

# --- Build target library list ---
$allSiteLists = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists").value

if ($AllLibraries) {
    # Discover all visible document libraries; skip hidden/system ones
    $targetLists = $allSiteLists | Where-Object {
        $_.list.template -eq "documentLibrary" -and -not $_.list.hidden
    }
    if ($ExcludeLibraries.Count -gt 0) {
        $targetLists = $targetLists | Where-Object { $ExcludeLibraries -notcontains $_.displayName }
    }
    if (-not $targetLists) { throw "No visible document libraries found on this site after exclusions." }
    Write-Host "Libraries discovered ($($targetLists.Count)):" -ForegroundColor Cyan
    foreach ($l in $targetLists) { Write-Host "  - $($l.displayName)" -ForegroundColor White }
} else {
    $targetLists = $allSiteLists | Where-Object { $_.displayName -eq $DocumentLibraryName }
    if (-not $targetLists) {
        throw "Library '$DocumentLibraryName' not found. Available: $($allSiteLists.displayName -join ', ')"
    }
    Write-Host "Library: $($targetLists.displayName)" -ForegroundColor Cyan
}

# --- Choice value sets (keep in sync with taxonomy.yaml v4) ---
$documentTypes = @(
    "PSA - Acquisition", "PSA - Disposition", "Lease", "Lease Amendment", "Vendor Contract",
    "Commission Agreement", "Loan Agreement", "JV Agreement", "Development Agreement",
    "Letter of Intent", "Term Sheet",
    "Survey", "Plat", "Design Drawing",
    "Closing Statement", "Environmental Survey", "Geotechnical Report", "Easement Document",
    "Proforma", "Budget / Cost Estimate", "Bid Tab", "Draw Request", "Operating Budget",
    "Other"
)
$transactionTypes = @("Acquisition", "Disposition", "Lease", "Easement", "Development Agreement", "Loan", "Construction Contract")
$documentStatuses = @("Draft", "Executed", "Final", "Superseded")
$disciplines      = @("Architectural", "Civil", "Landscape", "Structural", "Mechanical", "Electrical", "Plumbing")

# --- Column definitions (v4) ---
# Folder-derived columns (State/PropertyName/ProjectName) are created here but should be POPULATED
# via SharePoint folder default column values, not by the pipeline. See notes at the end.
$columns = @(
    # -- Classification / operational (AI) --
    @{ name = "DocumentType"; displayName = "Document Type"; group = "IDV Document Columns"; choice = @{ choices = $documentTypes } }
    @{ name = "AIConfidence"; displayName = "AI Confidence"; group = "IDV Document Columns"; number = @{} }
    @{ name = "AIProcessingStatus"; displayName = "AI Processing Status"; group = "IDV Document Columns"; choice = @{ choices = @("Classified","Under Review","Failed","Reviewed") } }
    @{ name = "AIClassifiedDate"; displayName = "AI Classified Date"; group = "IDV Document Columns"; dateTime = @{} }
    @{ name = "SuggestedFields"; displayName = "Suggested Fields"; group = "IDV Document Columns"; text = @{ allowMultipleLines = $true } }
    @{ name = "AIOriginalClassification"; displayName = "AI Original Classification"; group = "IDV Document Columns"; text = @{ allowMultipleLines = $true } }
    @{ name = "SourceSystem"; displayName = "Source System"; group = "IDV Document Columns"; text = @{} }

    # -- Folder-derived (populate via folder default column values) --
    @{ name = "State"; displayName = "State"; group = "IDV Document Columns"; text = @{} }
    @{ name = "PropertyName"; displayName = "Property Name"; group = "IDV Document Columns"; text = @{} }
    @{ name = "ProjectName"; displayName = "Project Name"; group = "IDV Document Columns"; text = @{} }

    # -- Content: universal --
    @{ name = "DocumentStatus"; displayName = "Document Status"; group = "IDV Document Columns"; choice = @{ choices = $documentStatuses } }
    @{ name = "Counterparty"; displayName = "Counterparty"; group = "IDV Document Columns"; text = @{} }
    @{ name = "TransactionType"; displayName = "Transaction Type"; group = "IDV Document Columns"; choice = @{ choices = $transactionTypes } }
    @{ name = "ExecutionDate"; displayName = "Execution Date"; group = "IDV Document Columns"; dateTime = @{} }
    @{ name = "EffectiveDate"; displayName = "Effective Date"; group = "IDV Document Columns"; dateTime = @{} }
    @{ name = "ExpirationDate"; displayName = "Expiration Date"; group = "IDV Document Columns"; dateTime = @{} }

    # -- Content: property --
    @{ name = "PropertyAddress"; displayName = "Property Address"; group = "IDV Document Columns"; text = @{} }
    @{ name = "ParcelID"; displayName = "Parcel ID"; group = "IDV Document Columns"; text = @{} }
    @{ name = "County"; displayName = "County"; group = "IDV Document Columns"; text = @{} }
    @{ name = "CityJurisdiction"; displayName = "City / ETJ Jurisdiction"; group = "IDV Document Columns"; text = @{} }
    @{ name = "Acres"; displayName = "Acres"; group = "IDV Document Columns"; number = @{} }
    @{ name = "SquareFootage"; displayName = "Square Footage"; group = "IDV Document Columns"; number = @{} }
    @{ name = "LandUse"; displayName = "Land Use"; group = "IDV Document Columns"; text = @{} }
    @{ name = "Zoning"; displayName = "Zoning"; group = "IDV Document Columns"; text = @{} }
    @{ name = "OpportunityZone"; displayName = "Opportunity Zone"; group = "IDV Document Columns"; choice = @{ choices = @("Yes","No","Unknown") } }

    # -- Content: transaction (financial values stored as text — they arrive as formatted strings) --
    @{ name = "Seller"; displayName = "Seller"; group = "IDV Document Columns"; text = @{} }
    @{ name = "Buyer"; displayName = "Buyer"; group = "IDV Document Columns"; text = @{} }
    @{ name = "Broker"; displayName = "Broker"; group = "IDV Document Columns"; text = @{} }
    @{ name = "TitleCompany"; displayName = "Title Company"; group = "IDV Document Columns"; text = @{} }
    @{ name = "ClosingDate"; displayName = "Closing Date"; group = "IDV Document Columns"; dateTime = @{} }
    @{ name = "PurchasePrice"; displayName = "Purchase Price"; group = "IDV Document Columns"; text = @{} }
    @{ name = "EarnestMoney"; displayName = "Earnest Money"; group = "IDV Document Columns"; text = @{} }
    @{ name = "DepositAmount"; displayName = "Deposit Amount"; group = "IDV Document Columns"; text = @{} }
    @{ name = "ContractValue"; displayName = "Contract Value"; group = "IDV Document Columns"; text = @{} }

    # -- Content: ownership --
    @{ name = "EntityName"; displayName = "Entity Name"; group = "IDV Document Columns"; text = @{} }
    @{ name = "InvestorFund"; displayName = "Investor / Fund"; group = "IDV Document Columns"; text = @{} }

    # -- Type-specific (Design Drawing) --
    @{ name = "Discipline"; displayName = "Discipline"; group = "IDV Document Columns"; choice = @{ choices = $disciplines } }
    @{ name = "SheetNumber"; displayName = "Sheet Number"; group = "IDV Document Columns"; text = @{} }
    @{ name = "DrawingTitle"; displayName = "Drawing Title"; group = "IDV Document Columns"; text = @{} }
)

# --- Create site columns (must be site-level so they can be linked to site content types) ---
Write-Host "`n=== Site Columns (v4) ===" -ForegroundColor Cyan

$existingCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns").value

$created = 0; $skipped = 0
foreach ($col in $columns) {
    $exists = $existingCols | Where-Object { $_.name -eq $col.name }
    if ($exists) {
        Write-Host "  [EXISTS] $($col.displayName)" -ForegroundColor Yellow
        $skipped++
        continue
    }
    if ($DryRun) {
        Write-Host "  [DRY RUN] Would create: $($col.displayName)" -ForegroundColor DarkGray
        continue
    }
    Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns" `
        -Body ($col | ConvertTo-Json -Depth 5) `
        -ContentType "application/json" | Out-Null
    Write-Host "  [CREATED] $($col.displayName)" -ForegroundColor Green
    $created++
}

# --- Build column name → id map (re-fetch after creation so new IDs are present) ---
# Site columns are used here because the $ref endpoint on site content types expects site column IDs.
$siteColumns = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns").value
$colIdByName = @{}
foreach ($siteCol in $siteColumns) {
    if ($siteCol.name) { $colIdByName[$siteCol.name] = $siteCol.id }
}

# --- Content type column assignments (per ADR-009) ---
$ctCommonCols = @(
    # AI operational
    "DocumentType", "AIConfidence", "AIProcessingStatus", "AIClassifiedDate",
    "SuggestedFields", "AIOriginalClassification", "SourceSystem",
    # Folder-derived
    "State", "PropertyName", "ProjectName",
    # Universal
    "DocumentStatus", "Counterparty", "TransactionType",
    # Property
    "PropertyAddress", "ParcelID", "County", "CityJurisdiction",
    "Acres", "SquareFootage", "LandUse", "Zoning", "OpportunityZone"
)
$ctTransactionCols = @(
    "ExecutionDate", "EffectiveDate", "ExpirationDate",
    "Seller", "Buyer", "Broker", "TitleCompany", "ClosingDate",
    "PurchasePrice", "EarnestMoney", "DepositAmount", "ContractValue",
    "EntityName", "InvestorFund"
)
$ctDrawingCols    = @("Discipline", "SheetNumber", "DrawingTitle")
$ctReportDateCols = @("ExecutionDate", "EffectiveDate", "ExpirationDate")

$contentTypeDefs = @(
    @{ name = "Contracts";     columns = $ctCommonCols + $ctTransactionCols }
    @{ name = "Drawing Files"; columns = $ctCommonCols + $ctDrawingCols }
    @{ name = "Reports";       columns = $ctCommonCols + $ctReportDateCols }
    @{ name = "Budget Files";  columns = $ctCommonCols }
    @{ name = "Other";         columns = $ctCommonCols + $ctTransactionCols }
)

# --- Content Types via Microsoft Graph API ---
# Root cause of all prior failures: the correct property for specifying the parent content
# type is "base" (contentTypeInfo), NOT "parentContentType". The Graph API v1.0 docs list
# "base" as the parent field; "parentContentType" is not a recognised request property, so
# it was silently ignored — leaving the parent unset — and the API returned "invalidCTParentId".
Write-Host "`n=== Content Types ===" -ForegroundColor Cyan

$existingSiteCTs = (Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes").value
$existingSiteCtByName = @{}
foreach ($ct in $existingSiteCTs) {
    if ($ct.name) { $existingSiteCtByName[$ct.name] = $ct.id }
}

$ctResults = @()
$ctCreated = 0; $ctSkipped = 0

foreach ($ctDef in $contentTypeDefs) {
    $ctName = $ctDef.name
    Write-Host ""
    Write-Host "  Content type: $ctName ($($ctDef.columns.Count) columns)" -ForegroundColor Cyan

    if ($existingSiteCtByName.ContainsKey($ctName)) {
        $ctId = $existingSiteCtByName[$ctName]
        Write-Host "    [EXISTS] Site CT (id: $ctId)" -ForegroundColor Yellow
        $ctSkipped++
    } elseif ($DryRun) {
        Write-Host "    [DRY RUN] Would create site CT '$ctName' (group: IDV Document Types)" -ForegroundColor DarkGray
        $ctId = $null
    } else {
        # "base" is the correct Graph API v1.0 property for the parent content type —
        # "parentContentType" is not a recognised field and caused "invalidCTParentId".
        $ctBody = @{
            name  = $ctName
            base  = @{ id = "0x0101"; name = "Document" }
            group = "IDV Document Types"
        } | ConvertTo-Json -Depth 3
        $newCt = Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes" `
            -Body $ctBody `
            -ContentType "application/json"
        $ctId = $newCt.id
        Write-Host "    [CREATED] Site CT (id: $ctId)" -ForegroundColor Green
        $ctCreated++
    }

    # Add site columns to the content type via columnLinks
    $existingCtColNames = @()
    if ($ctId) {
        $existingCtCols = (Invoke-MgGraphRequest -Method GET `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId/columns").value
        $existingCtColNames = $existingCtCols | Where-Object { $_.name } | Select-Object -ExpandProperty name
    }

    $colLinked = 0; $colAlreadyLinked = 0; $colMissing = 0
    foreach ($colName in $ctDef.columns) {
        if ($existingCtColNames -contains $colName) { $colAlreadyLinked++; continue }
        $colId = $colIdByName[$colName]
        if (-not $colId) {
            Write-Host "    [WARN] Site column '$colName' not found — skipped" -ForegroundColor Yellow
            $colMissing++; continue
        }
        if ($DryRun) {
            Write-Host "    [DRY RUN] Would link column '$colName'" -ForegroundColor DarkGray; continue
        }
        # columnLink.id IS the column's GUID (Graph API v1.0). @odata.id is not supported here.
        $refBody = @{ id = $colId } | ConvertTo-Json
        Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId/columnLinks" `
            -Body $refBody -ContentType "application/json" | Out-Null
        $colLinked++
    }
    if (-not $DryRun -and $ctId) {
        Write-Host "    Columns: $colLinked linked | $colAlreadyLinked already linked | $colMissing missing" -ForegroundColor DarkGray
    }

    $ctResults += [PSCustomObject]@{ Name = $ctName; Id = $ctId }
}

# --- Apply content types to each target library ---
Write-Host "`n=== Apply to Libraries ===" -ForegroundColor Cyan

foreach ($targetList in $targetLists) {
    $docListId   = $targetList.id
    $docListName = $targetList.displayName
    Write-Host "`n  Library: $docListName" -ForegroundColor White

    # Enable content type management
    if ($DryRun) {
        Write-Host "    [DRY RUN] Would enable contentTypesEnabled" -ForegroundColor DarkGray
    } else {
        $patchBody = @{ list = @{ contentTypesEnabled = $true } } | ConvertTo-Json -Depth 3
        Invoke-MgGraphRequest -Method PATCH `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId" `
            -Body $patchBody -ContentType "application/json" | Out-Null
        Write-Host "    [OK] contentTypesEnabled = true" -ForegroundColor DarkGray
    }

    # Fetch existing library CTs once per library
    $existingLibCtNames = @()
    if (-not $DryRun) {
        $existingLibCTs = (Invoke-MgGraphRequest -Method GET `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes").value
        $existingLibCtNames = $existingLibCTs | Select-Object -ExpandProperty name
    }

    foreach ($ctResult in $ctResults) {
        $ctName = $ctResult.Name
        $ctId   = $ctResult.Id
        if (-not $ctId) { continue }

        if ($existingLibCtNames -contains $ctName) {
            Write-Host "    [EXISTS] $ctName" -ForegroundColor Yellow
        } elseif ($DryRun) {
            Write-Host "    [DRY RUN] Would addCopy '$ctName'" -ForegroundColor DarkGray
        } else {
            $addCopyBody = @{ contentType = "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId" } | ConvertTo-Json
            Invoke-MgGraphRequest -Method POST `
                -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/addCopy" `
                -Body $addCopyBody -ContentType "application/json" | Out-Null
            Write-Host "    [ADDED] $ctName" -ForegroundColor Green
        }
    }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site            : $($site.displayName)"
Write-Host "  Libraries       : $($targetLists.Count) targeted ($($targetLists.displayName -join ', '))"
Write-Host "  Columns         : $($columns.Count) defined | $created created | $skipped existing"
Write-Host "  Content Types   : $($contentTypeDefs.Count) defined | $ctCreated created | $ctSkipped existing"
Write-Host ""
Write-Host "Site content type IDs:" -ForegroundColor Cyan
foreach ($ct in $ctResults) {
    if ($ct.Id) {
        Write-Host ("  {0,-14} {1}" -f $ct.Name, $ct.Id) -ForegroundColor White
    } else {
        Write-Host ("  {0,-14} (dry run — not created)" -f $ct.Name) -ForegroundColor DarkGray
    }
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Folder-derived columns (State, PropertyName, ProjectName) should be populated via"
Write-Host "     per-folder DEFAULT COLUMN VALUES (Set-PnPDefaultColumnValues), not by the pipeline."
Write-Host "  2. Create per-type library views for reviewers: filter by Content Type (ADR-009)."
Write-Host "  3. Create a filtered view 'Needs Review' on AIProcessingStatus = 'Under Review' (ADR-006)."
Write-Host "  4. Map columns to managed properties in the search schema for Copilot grounding (tenant admin)."
