<#
.SYNOPSIS
    Provisions the taxonomy v4 metadata columns and 5 content types on a SharePoint
    document library via Microsoft Graph API. Run after Graph permissions are granted.

    Columns and content types mirror docs/taxonomy/taxonomy.yaml (v4) and ADR-009.
    Review is handled inline via a filtered library view on AIProcessingStatus (ADR-006).

    Idempotent. Re-run after editing $columns, $contentTypeDefs or any choice set and the change is
    deployed; a no-change re-run performs reads only. Two things are reconciled beyond creation,
    both because a site-scope change does NOT reach libraries that already have the column or
    content type — each library holds its own copy:

      * Choice value lists — new values are added to the site column AND to every target library's
        own copy.
      * Content type column membership — a column newly added to $contentTypeDefs is added to each
        target library's content types, not just the site ones.

    Choice reconciliation is additive by default: script values are guaranteed present and any
    deployed-only values are preserved, since one may still be set on existing documents. Use
    -PruneChoices for an exact match. Column types and other facets are NOT reconciled, and columns
    are never removed, because both can destroy existing data.

    Run with -DryRun first. It reports what it inspected (a [CHECK] line per content type) as well
    as what it would change, and its summary counters tally planned work.

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.PARAMETER DocumentLibraryName
    Display name of a single target document library. Mutually exclusive with -AllLibraries.

.PARAMETER AllLibraries
    Discover and provision every visible document library on the site. Mutually exclusive
    with -DocumentLibraryName. SharePoint system libraries (Site Assets, Style Library, Site
    Pages, Form Templates, _catalogs, Preservation Hold) are never provisioned on any code path
    — they are matched by server-relative URL, not display name. Hidden libraries are skipped.

.PARAMETER ExcludeLibraries
    Additional libraries to treat as non-project content. Judgement calls only — system
    libraries are already excluded structurally. Defaults to "Documents", "Template", "Images",
    "Pages", because on these sites project content lives in per-project libraries; override it
    for a site where "Documents" is the real library.

.PARAMETER ListLibraries
    List the site's document libraries showing which would be targeted and which skipped (and
    why), then exit without changing anything. Use this before a real run.

.PARAMETER PruneChoices
    Make Choice lists an exact match to the script by also REMOVING deployed values the script no
    longer defines. Off by default: a removed value may still be set on existing documents, leaving
    those items holding a value the column no longer offers.

.PARAMETER DryRun
    Report what would be created, updated and added without making changes. Prints a [CHECK] line
    per library content type showing columns seen / wanted / missing, so "nothing to do" is
    distinguishable from "the comparison never ran".

.EXAMPLE
    # Single library
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -DocumentLibraryName "Documents"
    # All libraries on the site
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -AllLibraries
    # Dry run across all libraries
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -AllLibraries -DryRun
    # See what would be targeted before running anything
    .\Provision-SharePointSchema.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -ListLibraries
#>
[CmdletBinding(DefaultParameterSetName = "Single")]
param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [Parameter(ParameterSetName = "Single")]
    [string]$DocumentLibraryName = "Documents",

    [Parameter(ParameterSetName = "All")]
    [switch]$AllLibraries,

    # Judgement calls only — libraries that MIGHT hold project content but don't on these sites.
    # SharePoint's own system libraries are excluded structurally by $systemLibraryPaths below
    # and don't belong here. "Documents" is listed because on the IDV project sites content
    # lives in per-project libraries, but it stays overridable for sites where it doesn't.
    [Parameter(ParameterSetName = "All")]
    [string[]]$ExcludeLibraries = @('Documents', 'Template', 'Images', 'Pages'),

    # List the site's document libraries with their target/skip classification, then exit.
    [switch]$ListLibraries,

    # Exact-match Choice lists: also REMOVE deployed values the script no longer defines. Off by
    # default because a removed value may still be set on existing documents, which would leave
    # those items holding a value the column no longer offers.
    [switch]$PruneChoices,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# --- Connect ---
# Reuse an existing Graph session when the caller already has one with the scopes we need. An
# unconditional Connect-MgGraph forces a second interactive prompt when this script runs inside an
# already-authenticated session — with device-code sign-in that means the operator is asked to sign
# in twice for one run.
$requiredScopes = @("Sites.ReadWrite.All", "Sites.Manage.All")
$graphContext = Get-MgContext
if (-not $graphContext -or @($requiredScopes | Where-Object { $graphContext.Scopes -notcontains $_ }).Count -gt 0) {
    Connect-MgGraph -Scopes $requiredScopes
} else {
    Write-Host "Using existing Graph session: $($graphContext.Account)" -ForegroundColor DarkGray
}

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

# --- System libraries: never provisioned, regardless of parameters ----------------------------
# Matched on the library's server-relative URL rather than its display name. Display names are
# localized ("Documents" is "Documentos" on an es-ES tenant) and users rename libraries, so a
# name-based rule silently stops protecting; these URLs are fixed by SharePoint.
# Lists carrying the Graph `system` facet are already omitted from GET /sites/{id}/lists unless
# `system` is in $select, so this only needs to catch the built-ins Graph still returns.
# "Documents" is deliberately NOT here — it is a legitimate content library on other sites and
# is the -DocumentLibraryName default. Excluding it is a per-site judgement (-ExcludeLibraries).
$systemLibraryPaths = @(
    '/SiteAssets', '/Style Library', '/FormServerTemplates', '/SitePages',
    '/SiteCollectionDocuments', '/PreservationHoldLibrary', '/Preservation Hold Library',
    '/_catalogs', '/Custom Office Templates', '/DO_NOT_DELETE_SPLIST_SITECOLLECTION_AGGREGATED_CONTENTTYPES'
)

function Test-IsSystemLibrary {
    param($List)
    if ($List.system) { return $true }
    if (-not $List.webUrl) { return $false }
    $path = [System.Uri]::UnescapeDataString(([System.Uri]$List.webUrl).AbsolutePath)
    foreach ($systemPath in $systemLibraryPaths) {
        if ($path -eq $systemPath -or $path.EndsWith($systemPath) -or $path -like "*$systemPath/*") { return $true }
    }
    return $false
}

# --- Build target library list ---
$allSiteLists = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists").value
$allDocLibraries = @($allSiteLists | Where-Object { $_.list.template -eq "documentLibrary" })

if ($ListLibraries) {
    Write-Host "`nDocument libraries on this site:" -ForegroundColor Cyan
    foreach ($lib in ($allDocLibraries | Sort-Object displayName)) {
        $reason = if (Test-IsSystemLibrary $lib) { "system (never provisioned)" }
                  elseif ($lib.list.hidden)      { "hidden (skipped)" }
                  elseif ($ExcludeLibraries -contains $lib.displayName) { "in -ExcludeLibraries" }
                  else { "" }
        $colour = if ($reason) { "DarkGray" } else { "White" }
        $label  = if ($reason) { "  [skip]   {0,-38} {1}" } else { "  [target] {0,-38} {1}" }
        Write-Host ($label -f $lib.displayName, $reason) -ForegroundColor $colour
    }
    Write-Host ""
    return
}

# Applied to every code path: a system library is never a provisioning target.
$systemLibraries = @($allDocLibraries | Where-Object { Test-IsSystemLibrary $_ })
$candidateLists  = @($allDocLibraries | Where-Object { -not (Test-IsSystemLibrary $_) })
if ($systemLibraries.Count -gt 0) {
    Write-Host "System libraries skipped: $($systemLibraries.displayName -join ', ')" -ForegroundColor DarkGray
}

if ($AllLibraries) {
    # Discover all visible document libraries; hidden ones are not provisioning targets either
    $targetLists = $candidateLists | Where-Object { -not $_.list.hidden }
    if ($ExcludeLibraries.Count -gt 0) {
        $targetLists = $targetLists | Where-Object { $ExcludeLibraries -notcontains $_.displayName }
    }
    if (-not $targetLists) { throw "No visible document libraries found on this site after exclusions." }
    Write-Host "Libraries discovered ($(@($targetLists).Count)):" -ForegroundColor Cyan
    foreach ($l in $targetLists) { Write-Host "  - $($l.displayName)" -ForegroundColor White }
} else {
    # @() matters: a single Graph list is a Hashtable, so an unwrapped $targetLists.Count would
    # report its KEY count (13) instead of 1 in the summary.
    $targetLists = @($candidateLists | Where-Object { $_.displayName -eq $DocumentLibraryName })
    if (-not $targetLists) {
        # Distinguish "system library, refused" from "no such library" — the fix differs.
        if ($systemLibraries.displayName -contains $DocumentLibraryName) {
            throw "'$DocumentLibraryName' is a SharePoint system library and is never provisioned."
        }
        throw "Library '$DocumentLibraryName' not found. Available: $($candidateLists.displayName -join ', ')"
    }
    Write-Host "Library: $($targetLists.displayName)" -ForegroundColor Cyan
}

# --- Choice value sets (keep in sync with taxonomy.yaml v4) ---
$documentTypes = @(
    "PSA - Acquisition", "PSA - Disposition", "Lease", "Lease Amendment", "Vendor Contract",
    "Commission Agreement", "Loan Agreement", "JV Agreement", "Development Agreement",
    "Letter of Intent", "Term Sheet",
    "Survey", "Plat", "Design Drawing",
    "Closing Statement", "Environmental Survey", "Geotechnical Report", "CMT", "Easement Document",
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
    # Populated only when Agent 1's primary pick doesn't match a taxonomy label (see UnrecognizedType
    # on TypeClassificationResult) -- the model's own wording, so a taxonomy gap is visible on the
    # document itself rather than buried in the batch report's Low-Confidence table.
    @{ name = "AISuggestedType"; displayName = "AI Suggested Type"; group = "IDV Document Columns"; text = @{} }
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

# --- Choice convergence ------------------------------------------------------------------------
# Creating a column is not enough to keep it correct: adding a value to $documentTypes and re-running
# used to report [EXISTS] and change nothing, leaving SharePoint's Choice column without the value.
# WriteMetadataActivity writes DocumentType unguarded, so one missing value makes Graph reject the
# ENTIRE fields PATCH and the document loses all metadata. Hence this converge pass.
#
# Two scopes must be patched, not one. A Choice list is copied into each library when the column
# arrives there, and patching the SITE column does NOT propagate to libraries that already have it
# — verified 2026-09-17: after the site column went to 25 values, all 69 provisioned libraries still
# reported 24. The identical GUID across site/list/content-type scope is field lineage, not a shared
# object. The pipeline writes to LIST columns, so a site-only patch looks right in the admin UI and
# fixes nothing.
#
# Scope limit: choices only. Other facets (type changes, text→number, allowMultipleLines) are NOT
# reconciled — those can destroy existing data and belong in a considered migration, not an
# idempotent provisioning run.
$desiredChoices = @{}
foreach ($col in $columns) {
    if ($col.ContainsKey('choice')) { $desiredChoices[$col.name] = @($col.choice.choices) }
}

# Returns the choice array to write, or $null when the deployed column already satisfies the script.
# Additive by default: script values are guaranteed present, deployed-only extras are preserved
# (a value may be in use by existing documents). -PruneChoices makes it an exact match instead.
function Get-ConvergedChoices {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Desired,
        # AllowEmptyString because a malformed facet can contain a blank entry; binding must not fail
        # mid-run over it. Blanks are filtered below.
        [Parameter(Mandatory)][AllowEmptyCollection()][AllowNull()][AllowEmptyString()][string[]]$Deployed
    )

    # Drop nulls/blanks: a column with no choice facet arrives as @($null), which would otherwise be
    # carried through as a bogus empty choice value.
    $have    = @($Deployed | Where-Object { -not [string]::IsNullOrEmpty($_) })
    $missing = @($Desired | Where-Object { $have -notcontains $_ })
    $extra   = @($have    | Where-Object { $Desired -notcontains $_ })

    # Compared as sets: ordering alone never triggers a write, otherwise any difference in the order
    # SharePoint returns would make every run rewrite every column. Order still converges to the
    # script's whenever a real value difference forces the patch.
    if ($missing.Count -eq 0 -and ($extra.Count -eq 0 -or -not $PruneChoices)) { return $null }

    # Script order wins; kept extras are appended so their existing values stay selectable.
    $result = if ($PruneChoices) { @($Desired) } else { @($Desired) + $extra }
    return ,$result
}

# PATCHes a column's choices in place, preserving the rest of the choice facet — sending only
# `choices` can reset allowTextEntry/displayAs to defaults.
function Set-ColumnChoices {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)]$DeployedColumn,
        [Parameter(Mandatory)][string[]]$Choices
    )

    $body = @{
        choice = @{
            allowTextEntry = [bool]$DeployedColumn.choice.allowTextEntry
            choices        = $Choices
            displayAs      = $DeployedColumn.choice.displayAs
        }
    } | ConvertTo-Json -Depth 5

    Invoke-MgGraphRequest -Method PATCH -Uri $Uri -Body $body -ContentType "application/json" | Out-Null
}

# --- Create or converge site columns (site-level so they can be linked to site content types) ---
Write-Host "`n=== Site Columns (v4) ===" -ForegroundColor Cyan

$existingCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns").value

$created = 0; $skipped = 0; $siteChoicesUpdated = 0
foreach ($col in $columns) {
    $exists = $existingCols | Where-Object { $_.name -eq $col.name }
    if ($exists) {
        # Existing column: reconcile its choice list rather than assuming it is still correct.
        $desired = $desiredChoices[$col.name]
        if ($desired) {
            $converged = Get-ConvergedChoices -Desired $desired -Deployed @($exists.choice.choices)
            if ($converged) {
                $added   = @($converged | Where-Object { @($exists.choice.choices) -notcontains $_ })
                $removed = @(@($exists.choice.choices) | Where-Object { $converged -notcontains $_ })
                $delta   = @()
                if ($added.Count)   { $delta += "+$($added -join ', ')" }
                if ($removed.Count) { $delta += "-$($removed -join ', ')" }

                if ($DryRun) {
                    Write-Host "  [DRY RUN] Would update choices: $($col.displayName)  ($($delta -join ' '))" -ForegroundColor DarkGray
                    $siteChoicesUpdated++
                } else {
                    Set-ColumnChoices -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/columns/$($exists.id)" `
                        -DeployedColumn $exists -Choices $converged
                    Write-Host "  [UPDATED] $($col.displayName)  ($($delta -join ' '))" -ForegroundColor Green
                    $siteChoicesUpdated++
                }
                continue
            }
        }
        Write-Host "  [EXISTS] $($col.displayName)" -ForegroundColor Yellow
        $skipped++
        continue
    }
    if ($DryRun) {
        Write-Host "  [DRY RUN] Would create: $($col.displayName)" -ForegroundColor DarkGray
        $created++
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
    "SuggestedFields", "AIOriginalClassification", "AISuggestedType", "SourceSystem",
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

$listChoicesUpdated = 0
$listColumnsAdded   = 0

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

    # Fetch existing library CTs once per library. This is a read, so it runs in dry run too —
    # without it, dry run reports "would addCopy" for content types the library already has.
    # $expand=columns costs nothing extra here and gives the column-membership pass below the data it
    # needs without 5 more requests per library.
    $existingLibCTs = (Invoke-MgGraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes?`$expand=columns").value
    $existingLibCtNames = @($existingLibCTs | Where-Object { $_.name } | Select-Object -ExpandProperty name)

    foreach ($ctResult in $ctResults) {
        $ctName = $ctResult.Name
        $ctId   = $ctResult.Id

        if ($existingLibCtNames -contains $ctName) {
            Write-Host "    [EXISTS] $ctName" -ForegroundColor Yellow
        } elseif ($DryRun) {
            # $ctId is null for site CTs a real run would have created first — report them
            # anyway, otherwise a dry run against an unprovisioned site says nothing here.
            Write-Host "    [DRY RUN] Would addCopy '$ctName'" -ForegroundColor DarkGray
        } elseif (-not $ctId) {
            Write-Host "    [SKIP] $ctName — site content type id unknown" -ForegroundColor Yellow
        } else {
            $addCopyBody = @{ contentType = "https://graph.microsoft.com/v1.0/sites/$siteId/contentTypes/$ctId" } | ConvertTo-Json
            Invoke-MgGraphRequest -Method POST `
                -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/addCopy" `
                -Body $addCopyBody -ContentType "application/json" | Out-Null
            Write-Host "    [ADDED] $ctName" -ForegroundColor Green
        }
    }

    # --- Converge this library's COLUMN MEMBERSHIP ---
    # Adding a column to a site content type does not reach libraries whose content types were
    # already copied by addCopy. Verified 2026-09-17: after AISuggestedType was created and linked to
    # all 5 site content types, 70 of 72 libraries still had no such list column — only the two that
    # got a fresh addCopy in the same run did. ValidateSharePointSchemaActivity reads
    # /drives/{driveId}/list/columns, so those 70 would fail a batch's pre-flight check while this
    # script reported success.
    #
    # Two approaches do NOT work, so don't reach for them again:
    #   * PATCH the site content type with propagateChanges=true — returns 200 and pushes nothing.
    #   * POST .../lists/{id}/contentTypes/{ctId}/columnLinks — 404; columnLinks is site-scope only.
    # What works is POSTing a columnDefinition to the LIST content type bound to the site column,
    # which keeps the library's column tied to the site column rather than creating a local one.
    $libColumns = (Invoke-MgGraphRequest -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value

    # Membership is compared PER CONTENT TYPE, never against the library's flat column list. A column
    # can be present as a list column while still missing from most content types — that is exactly
    # what a partial earlier fix leaves behind, and gating on list-column presence silently skips it.
    # Scoped by NAME against $contentTypeDefs, deliberately not by content type `group`: the group
    # property is not reliably returned alongside $expand=columns, and filtering on it silently
    # matched nothing — the pass reported "0 added" while doing no work at all. The name lookup below
    # is the real scope, so a content type we don't define (Document, Folder) falls out on its own.
    if ($existingLibCtNames.Count -gt 0) {
        $linked = 0
        foreach ($libCt in @($existingLibCTs)) {
            $wantedForCt = ($contentTypeDefs | Where-Object { $_.name -eq $libCt.name }).columns
            if (-not $wantedForCt) { continue }

            # Normally populated by $expand=columns above; fall back to a direct read if it wasn't,
            # rather than treating "no data" as "nothing missing".
            $ctCols = @($libCt.columns | Where-Object { $_.name } | Select-Object -ExpandProperty name)
            $ctColsSource = 'expand'
            if ($ctCols.Count -eq 0) {
                $ctCols = @((Invoke-MgGraphRequest -Method GET `
                    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/$($libCt.id)/columns").value |
                    Where-Object { $_.name } | Select-Object -ExpandProperty name)
                $ctColsSource = 'direct read'
            }

            $missingForCt = @($wantedForCt | Where-Object { $ctCols -notcontains $_ })

            # Report what was inspected, not just what changed: a silent "0 added" cannot be told
            # apart from "the comparison never ran", which is how the two earlier bugs here hid.
            if ($DryRun) {
                Write-Host ("    [CHECK] CT '{0}': {1} columns via {2}, {3} wanted, {4} missing" -f `
                    $libCt.name, $ctCols.Count, $ctColsSource, @($wantedForCt).Count, $missingForCt.Count) -ForegroundColor DarkGray
            }

            foreach ($colName in $missingForCt) {
                $siteColId = $colIdByName[$colName]
                if (-not $siteColId) {
                    Write-Host "    [WARN] '$colName' has no site column — cannot add to '$($libCt.name)'" -ForegroundColor Yellow
                    continue
                }
                if ($DryRun) {
                    Write-Host "    [DRY RUN] Would add '$colName' to CT '$($libCt.name)'" -ForegroundColor DarkGray
                    $linked++      # counted so the summary reports planned work, not 0
                    continue
                }
                $bindBody = @{
                    'sourceColumn@odata.bind' = "https://graph.microsoft.com/v1.0/sites/$siteId/columns/$siteColId"
                } | ConvertTo-Json
                Invoke-MgGraphRequest -Method POST `
                    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes/$($libCt.id)/columns" `
                    -Body $bindBody -ContentType "application/json" | Out-Null
                Write-Host "    [ADDED COLUMN] '$colName' -> CT '$($libCt.name)'" -ForegroundColor Green
                $linked++
            }
        }
        $listColumnsAdded += $linked
        if (-not $DryRun -and $linked -gt 0) {
            # Re-read so the choice pass below sees columns this pass just introduced.
            $libColumns = (Invoke-MgGraphRequest -Method GET `
                -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value
        }
    }

    # --- Converge this library's copy of each Choice column ---
    # The site-level patch above does not reach here (see the Choice convergence notes), and this is
    # the scope the pipeline actually writes to. Read-only when already in sync, so a second run
    # costs no writes.
    if ($desiredChoices.Count -gt 0) {
        $libUpdated = 0; $libInSync = 0; $libAbsent = 0
        foreach ($colName in $desiredChoices.Keys) {
            $libCol = $libColumns | Where-Object { $_.name -eq $colName }
            if (-not $libCol) {
                # Expected on a library that has no IDV content types yet — addCopy above brings the
                # columns in, and the next run converges them.
                $libAbsent++
                continue
            }

            $converged = Get-ConvergedChoices -Desired $desiredChoices[$colName] -Deployed @($libCol.choice.choices)
            if (-not $converged) { $libInSync++; continue }

            $added = @($converged | Where-Object { @($libCol.choice.choices) -notcontains $_ })
            if ($DryRun) {
                Write-Host "    [DRY RUN] Would update '$colName' choices ($($added.Count) added)" -ForegroundColor DarkGray
                $libUpdated++
            } else {
                Set-ColumnChoices -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns/$($libCol.id)" `
                    -DeployedColumn $libCol -Choices $converged
                Write-Host "    [UPDATED] '$colName' choices ($($added -join ', '))" -ForegroundColor Green
                $libUpdated++
            }
        }
        $listChoicesUpdated += $libUpdated
        if ($libAbsent -gt 0 -and -not $DryRun) {
            Write-Host "    Choice columns: $libInSync in sync, $libUpdated updated, $libAbsent not on library yet" -ForegroundColor DarkGray
        }
    }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site            : $($site.displayName)"
Write-Host "  Libraries       : $(@($targetLists).Count) targeted ($(@($targetLists).displayName -join ', '))"
# In dry run the counters below tally PLANNED work. They are incremented on the dry-run branches on
# purpose: a summary reading "0 added" under a screen full of "Would add" lines is worse than no
# summary at all, and that discrepancy has already masked two real bugs in this pass.
$verb = if ($DryRun) { " would be" } else { "" }
Write-Host "  Mode            : $(if ($DryRun) { 'DRY RUN — nothing was changed' } else { 'APPLIED' })" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "Green" })
Write-Host "  Columns         : $($columns.Count) defined | $created $(if ($DryRun) { 'to create' } else { 'created' }) | $skipped existing"
Write-Host "  Choice sync     : $siteChoicesUpdated site + $listChoicesUpdated library column(s)$verb updated$(if ($PruneChoices) { ' (prune enabled)' })"
Write-Host "  Column members  : $listColumnsAdded column(s)$verb added to library content types"
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
