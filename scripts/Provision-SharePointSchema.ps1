<#
.SYNOPSIS
    Provisions the taxonomy v4 metadata columns and 5 content types on a SharePoint
    document library via Microsoft Graph API. Run after Graph permissions are granted.

    Columns and content types mirror docs/taxonomy/taxonomy.yaml (v4) and ADR-009.
    Review is handled inline via a filtered library view on AIProcessingStatus (ADR-006).

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.PARAMETER DocumentLibraryName
    Display name of the target document library. Default: "Documents"

.PARAMETER DryRun
    If set, shows what would be created without making changes.

.EXAMPLE
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects"
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -DocumentLibraryName "Active Projects" -DryRun
#>

param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [string]$DocumentLibraryName = "Documents",

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --- Connect ---
Connect-MgGraph -Scopes "Sites.ReadWrite.All", "Sites.Manage.All"

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

# --- Find the document library ---
$drives = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/drives"
$drive = $drives.value | Where-Object { $_.name -eq $DocumentLibraryName }
if (-not $drive) {
    throw "Document library '$DocumentLibraryName' not found. Available: $($drives.value.name -join ', ')"
}

# Get the corresponding list (for column creation)
$lists = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists"
$docList = $lists.value | Where-Object { $_.displayName -eq $DocumentLibraryName }
if (-not $docList) {
    throw "Could not find list for library '$DocumentLibraryName'"
}
$docListId = $docList.id

Write-Host "Library: $DocumentLibraryName (listId: $docListId, driveId: $($drive.id))" -ForegroundColor Cyan

# --- Enable content type management on the library (must precede content type creation) ---
Write-Host "`n=== Enable Content Type Management ===" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "  [DRY RUN] Would PATCH list to enable contentTypesEnabled" -ForegroundColor DarkGray
} else {
    $patchBody = @{ list = @{ contentTypesEnabled = $true } } | ConvertTo-Json -Depth 3
    Invoke-MgGraphRequest -Method PATCH `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId" `
        -Body $patchBody `
        -ContentType "application/json" | Out-Null
    Write-Host "  [OK] contentTypesEnabled = true" -ForegroundColor Green
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
    @{ name = "DocumentType"; displayName = "Document Type"; choice = @{ choices = $documentTypes } }
    @{ name = "AIConfidence"; displayName = "AI Confidence"; number = @{} }
    @{ name = "AIProcessingStatus"; displayName = "AI Processing Status"; choice = @{ choices = @("Classified","Under Review","Failed","Reviewed") } }
    @{ name = "AIClassifiedDate"; displayName = "AI Classified Date"; dateTime = @{} }
    @{ name = "SuggestedFields"; displayName = "Suggested Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "AIOriginalClassification"; displayName = "AI Original Classification"; text = @{ allowMultipleLines = $true } }
    @{ name = "SourceSystem"; displayName = "Source System"; text = @{} }

    # -- Folder-derived (populate via folder default column values) --
    @{ name = "State"; displayName = "State"; text = @{} }
    @{ name = "PropertyName"; displayName = "Property Name"; text = @{} }
    @{ name = "ProjectName"; displayName = "Project Name"; text = @{} }

    # -- Content: universal --
    @{ name = "DocumentStatus"; displayName = "Document Status"; choice = @{ choices = $documentStatuses } }
    @{ name = "Counterparty"; displayName = "Counterparty"; text = @{} }
    @{ name = "TransactionType"; displayName = "Transaction Type"; choice = @{ choices = $transactionTypes } }
    @{ name = "ExecutionDate"; displayName = "Execution Date"; dateTime = @{} }
    @{ name = "EffectiveDate"; displayName = "Effective Date"; dateTime = @{} }
    @{ name = "ExpirationDate"; displayName = "Expiration Date"; dateTime = @{} }

    # -- Content: property --
    @{ name = "PropertyAddress"; displayName = "Property Address"; text = @{} }
    @{ name = "ParcelID"; displayName = "Parcel ID"; text = @{} }
    @{ name = "County"; displayName = "County"; text = @{} }
    @{ name = "CityJurisdiction"; displayName = "City / ETJ Jurisdiction"; text = @{} }
    @{ name = "Acres"; displayName = "Acres"; number = @{} }
    @{ name = "SquareFootage"; displayName = "Square Footage"; number = @{} }
    @{ name = "LandUse"; displayName = "Land Use"; text = @{} }
    @{ name = "Zoning"; displayName = "Zoning"; text = @{} }
    @{ name = "OpportunityZone"; displayName = "Opportunity Zone"; choice = @{ choices = @("Yes","No","Unknown") } }

    # -- Content: transaction (financial values stored as text — they arrive as formatted strings) --
    @{ name = "Seller"; displayName = "Seller"; text = @{} }
    @{ name = "Buyer"; displayName = "Buyer"; text = @{} }
    @{ name = "Broker"; displayName = "Broker"; text = @{} }
    @{ name = "TitleCompany"; displayName = "Title Company"; text = @{} }
    @{ name = "ClosingDate"; displayName = "Closing Date"; dateTime = @{} }
    @{ name = "PurchasePrice"; displayName = "Purchase Price"; text = @{} }
    @{ name = "EarnestMoney"; displayName = "Earnest Money"; text = @{} }
    @{ name = "DepositAmount"; displayName = "Deposit Amount"; text = @{} }
    @{ name = "ContractValue"; displayName = "Contract Value"; text = @{} }

    # -- Content: ownership --
    @{ name = "EntityName"; displayName = "Entity Name"; text = @{} }
    @{ name = "InvestorFund"; displayName = "Investor / Fund"; text = @{} }

    # -- Type-specific (Design Drawing) --
    @{ name = "Discipline"; displayName = "Discipline"; choice = @{ choices = $disciplines } }
    @{ name = "SheetNumber"; displayName = "Sheet Number"; text = @{} }
    @{ name = "DrawingTitle"; displayName = "Drawing Title"; text = @{} }
)

# --- Create columns on document library ---
Write-Host "`n=== Document Library Columns (v4) ===" -ForegroundColor Cyan

$existingCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value

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
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns" `
        -Body ($col | ConvertTo-Json -Depth 5) `
        -ContentType "application/json" | Out-Null
    Write-Host "  [CREATED] $($col.displayName)" -ForegroundColor Green
    $created++
}

# --- Build column name → id map (re-fetch after creation so new IDs are present) ---
$libColumns = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value
$colIdByName = @{}
foreach ($libCol in $libColumns) {
    if ($libCol.name) { $colIdByName[$libCol.name] = $libCol.id }
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

# --- Get the Document parent content type ID from the site ---
Write-Host "`n=== Site Content Types ===" -ForegroundColor Cyan
$documentCtResponse = Invoke-MgGraphRequest -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes?`$filter=name eq 'Document'"
$documentCtId = $documentCtResponse.value[0].id
if (-not $documentCtId) {
    throw "Could not find 'Document' content type on site '$($site.displayName)'"
}
Write-Host "  Document parent CT ID: $documentCtId" -ForegroundColor DarkGray

# --- Fetch existing site and library content types for idempotency checks ---
$existingSiteCTs = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes").value
$existingSiteCtById = @{}
foreach ($ct in $existingSiteCTs) {
    if ($ct.name) { $existingSiteCtById[$ct.name] = $ct.id }
}

$existingLibCTs = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes").value
$existingLibCtNames = $existingLibCTs | Select-Object -ExpandProperty name

# --- Create/ensure 5 site content types, add columns, add to library ---
$ctResults = @()
$ctCreated = 0; $ctSkipped = 0

foreach ($ctDef in $contentTypeDefs) {
    $ctName = $ctDef.name
    Write-Host ""
    Write-Host "  Content type: $ctName ($($ctDef.columns.Count) columns)" -ForegroundColor Cyan

    # Idempotent: check if the site CT already exists
    if ($existingSiteCtById.ContainsKey($ctName)) {
        $ctId = $existingSiteCtById[$ctName]
        Write-Host "    [EXISTS] Site CT (id: $ctId)" -ForegroundColor Yellow
        $ctSkipped++
    } elseif ($DryRun) {
        Write-Host "    [DRY RUN] Would create site CT '$ctName' (group: IDV Document Types)" -ForegroundColor DarkGray
        $ctId = $null
    } else {
        $ctBody = @{
            name              = $ctName
            parentContentType = @{ id = $documentCtId }
            group             = "IDV Document Types"
        } | ConvertTo-Json -Depth 3
        $newCt = Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes" `
            -Body $ctBody `
            -ContentType "application/json"
        $ctId = $newCt.id
        Write-Host "    [CREATED] Site CT (id: $ctId)" -ForegroundColor Green
        $ctCreated++
    }

    # Add columns to the site CT via `$ref
    $existingCtColNames = @()
    if ($ctId) {
        $existingCtCols = (Invoke-MgGraphRequest -Method GET `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId/columns").value
        $existingCtColNames = $existingCtCols | Where-Object { $_.name } | Select-Object -ExpandProperty name
    }

    $colLinked = 0; $colAlreadyLinked = 0; $colMissing = 0
    foreach ($colName in $ctDef.columns) {
        if ($existingCtColNames -contains $colName) {
            $colAlreadyLinked++
            continue
        }
        $colId = $colIdByName[$colName]
        if (-not $colId) {
            Write-Host "    [WARN] Column '$colName' not found in library — skipped" -ForegroundColor Yellow
            $colMissing++
            continue
        }
        if ($DryRun) {
            Write-Host "    [DRY RUN] Would link column '$colName'" -ForegroundColor DarkGray
            continue
        }
        $refBody = @{
            "@odata.id" = "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns/$colId"
        } | ConvertTo-Json
        Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId/columns/`$ref" `
            -Body $refBody `
            -ContentType "application/json" | Out-Null
        $colLinked++
    }
    if (-not $DryRun -and $ctId) {
        Write-Host "    Columns: $colLinked linked | $colAlreadyLinked already linked | $colMissing missing" -ForegroundColor DarkGray
    }

    # Add site CT to the library
    if ($existingLibCtNames -contains $ctName) {
        Write-Host "    [EXISTS] Already in library" -ForegroundColor Yellow
    } elseif ($DryRun) {
        Write-Host "    [DRY RUN] Would add CT '$ctName' to library" -ForegroundColor DarkGray
    } elseif ($ctId) {
        $addCopyBody = @{
            "@odata.id" = "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId"
        } | ConvertTo-Json
        Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/`$addCopy" `
            -Body $addCopyBody `
            -ContentType "application/json" | Out-Null
        Write-Host "    [ADDED] CT added to library" -ForegroundColor Green
    }

    $ctResults += [PSCustomObject]@{ Name = $ctName; Id = $ctId }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site            : $($site.displayName)"
Write-Host "  Document Library: $DocumentLibraryName (listId: $docListId)"
Write-Host "  Drive ID        : $($drive.id)"
Write-Host "  Columns         : $($columns.Count) defined | $created created | $skipped existing"
Write-Host "  Content Types   : $($contentTypeDefs.Count) defined | $ctCreated created | $ctSkipped existing"
Write-Host ""
Write-Host "Content type IDs:" -ForegroundColor Cyan
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
Write-Host ""
Write-Host "For batch runs, pass the library or folder URL directly:" -ForegroundColor Yellow
Write-Host "  POST /api/batch { `"url`": `"https://<tenant>.sharepoint.com/sites/<site>/$DocumentLibraryName`" }"
Write-Host "  DriveId (for reference): $($drive.id)"
