<#
.SYNOPSIS
    Provisions SharePoint metadata columns on a document library
    via Microsoft Graph API. Run after Graph permissions are granted.

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

# --- Column definitions for the document library ---
$columns = @(
    @{ name = "DocumentType"; displayName = "Document Type"; choice = @{ choices = @("Lease Agreement","Offer Memorandum","Market Report","Purchase Agreement","Letter of Intent","Financial Analysis","Due Diligence","Correspondence","Presentation","Other") } }
    @{ name = "DealType"; displayName = "Deal Type"; choice = @{ choices = @("Lease","Sale","Development","Acquisition","Disposition","Financing","Other") } }
    @{ name = "Submarket"; displayName = "Submarket"; choice = @{ choices = @("Northwest Houston","North Houston","Northeast Houston","Katy/West Houston","Southwest Houston","Southeast Houston","Central Houston","Multiple","Unknown") } }
    @{ name = "Counterparty"; displayName = "Counterparty"; text = @{} }
    @{ name = "Confidentiality"; displayName = "Confidentiality"; choice = @{ choices = @("Public","Internal","Confidential","Highly Confidential") } }
    @{ name = "AIConfidence"; displayName = "AI Confidence"; number = @{} }
    @{ name = "AIProcessingStatus"; displayName = "AI Processing Status"; choice = @{ choices = @("Classified","Under Review","Failed","Reviewed") } }
    @{ name = "AIClassifiedDate"; displayName = "AI Classified Date"; dateTime = @{} }
    @{ name = "TypeSpecificFields"; displayName = "Type Specific Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "SuggestedFields"; displayName = "Suggested Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "AIOriginalClassification"; displayName = "AI Original Classification"; text = @{ allowMultipleLines = $true } }
)

# --- Create columns on document library ---
Write-Host "`n=== Document Library Columns ===" -ForegroundColor Cyan

$existingCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value

foreach ($col in $columns) {
    $exists = $existingCols | Where-Object { $_.name -eq $col.name }
    if ($exists) {
        Write-Host "  [EXISTS] $($col.displayName)" -ForegroundColor Yellow
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
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site           : $($site.displayName)"
Write-Host "  Document Library: $DocumentLibraryName (listId: $docListId)"
Write-Host "  Drive ID       : $($drive.id)"
Write-Host ""
Write-Host "For batch runs, pass the library URL directly:" -ForegroundColor Yellow
Write-Host "  POST /api/batch { \`"url\`": \`"https://<tenant>.sharepoint.com/sites/<site>/$DocumentLibraryName\`" }"
Write-Host "  DriveId (for reference): $($drive.id)"
$existingList = $lists.value | Where-Object { $_.displayName -eq $reviewListName }

if ($existingList) {
    Write-Host "  [EXISTS] $reviewListName" -ForegroundColor Yellow
    $reviewListId = $existingList.id
} elseif ($DryRun) {
    Write-Host "  [DRY RUN] Would create list: $reviewListName" -ForegroundColor DarkGray
    $reviewListId = $null
} else {
    $newList = Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists" `
        -Body (@{
            displayName = $reviewListName
            list = @{ template = "genericList" }
        } | ConvertTo-Json -Depth 5) `
        -ContentType "application/json"
    $reviewListId = $newList.id
    Write-Host "  [CREATED] $reviewListName (id: $reviewListId)" -ForegroundColor Green
}

$reviewColumns = @(
    @{ name = "DocumentLink"; displayName = "Document Link"; hyperlinkOrPicture = @{ isPicture = $false } }
    @{ name = "DocumentId"; displayName = "Document ID"; text = @{} }
    @{ name = "SiteId"; displayName = "Site ID"; text = @{} }
    @{ name = "DriveId"; displayName = "Drive ID"; text = @{} }
    @{ name = "DocumentType"; displayName = "Document Type"; choice = @{ choices = @("Lease Agreement","Offer Memorandum","Market Report","Purchase Agreement","Letter of Intent","Financial Analysis","Due Diligence","Correspondence","Presentation","Other") } }
    @{ name = "TypeConfidence"; displayName = "Type Confidence"; number = @{} }
    @{ name = "ProposedDealType"; displayName = "Proposed Deal Type"; choice = @{ choices = @("Lease","Sale","Development","Acquisition","Disposition","Financing","Other") } }
    @{ name = "ProposedSubmarket"; displayName = "Proposed Submarket"; choice = @{ choices = @("Northwest Houston","North Houston","Northeast Houston","Katy/West Houston","Southwest Houston","Southeast Houston","Central Houston","Multiple","Unknown") } }
    @{ name = "ProposedCounterparty"; displayName = "Proposed Counterparty"; text = @{} }
    @{ name = "ProposedConfidentiality"; displayName = "Proposed Confidentiality"; choice = @{ choices = @("Public","Internal","Confidential","Highly Confidential") } }
    @{ name = "TypeSpecificFields"; displayName = "Type Specific Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "SuggestedFields"; displayName = "Suggested Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "ConfidenceScores"; displayName = "Confidence Scores"; text = @{ allowMultipleLines = $true } }
    @{ name = "LowConfidenceFields"; displayName = "Low Confidence Fields"; text = @{ allowMultipleLines = $true } }
    @{ name = "AIReasoning"; displayName = "AI Reasoning"; text = @{ allowMultipleLines = $true } }
    @{ name = "ReviewStatus"; displayName = "Review Status"; choice = @{ choices = @("Pending","Approved","Corrected","Rejected","Skipped") }; defaultValue = @{ value = "Pending" } }
    @{ name = "CorrectionNotes"; displayName = "Correction Notes"; text = @{ allowMultipleLines = $true } }
    @{ name = "BatchId"; displayName = "Batch ID"; text = @{} }
    @{ name = "ProcessingDate"; displayName = "Processing Date"; dateTime = @{} }
)

if ($reviewListId) {
    $existingReviewCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$reviewListId/columns").value

    foreach ($col in $reviewColumns) {
        $exists = $existingReviewCols | Where-Object { $_.name -eq $col.name }
        if ($exists) {
            Write-Host "  [EXISTS] $($col.displayName)" -ForegroundColor Yellow
            continue
        }
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would create: $($col.displayName)" -ForegroundColor DarkGray
            continue
        }
        Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$reviewListId/columns" `
            -Body ($col | ConvertTo-Json -Depth 5) `
            -ContentType "application/json" | Out-Null
        Write-Host "  [CREATED] $($col.displayName)" -ForegroundColor Green
    }
}

# --- Corrections Log List ---
Write-Host "`n=== Corrections Log List ===" -ForegroundColor Cyan

$correctionsListName = "Corrections Log"
$existingCorrections = $lists.value | Where-Object { $_.displayName -eq $correctionsListName }

if ($existingCorrections) {
    Write-Host "  [EXISTS] $correctionsListName" -ForegroundColor Yellow
    $correctionsListId = $existingCorrections.id
} elseif ($DryRun) {
    Write-Host "  [DRY RUN] Would create list: $correctionsListName" -ForegroundColor DarkGray
    $correctionsListId = $null
} else {
    $newList = Invoke-MgGraphRequest -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists" `
        -Body (@{
            displayName = $correctionsListName
            list = @{ template = "genericList" }
        } | ConvertTo-Json -Depth 5) `
        -ContentType "application/json"
    $correctionsListId = $newList.id
    Write-Host "  [CREATED] $correctionsListName (id: $correctionsListId)" -ForegroundColor Green
}

$correctionColumns = @(
    @{ name = "DocumentId"; displayName = "Document ID"; text = @{} }
    @{ name = "FileName"; displayName = "File Name"; text = @{} }
    @{ name = "CorrectionType"; displayName = "Correction Type"; choice = @{ choices = @("document_type","common_field","specific_field","suggested_field") } }
    @{ name = "Category"; displayName = "Category"; text = @{} }
    @{ name = "AIProposedValue"; displayName = "AI Proposed Value"; text = @{} }
    @{ name = "CorrectedValue"; displayName = "Corrected Value"; text = @{} }
    @{ name = "AIConfidence"; displayName = "AI Confidence"; number = @{} }
    @{ name = "AIReasoning"; displayName = "AI Reasoning"; text = @{ allowMultipleLines = $true } }
    @{ name = "CorrectorNotes"; displayName = "Corrector Notes"; text = @{ allowMultipleLines = $true } }
    @{ name = "CorrectedDate"; displayName = "Corrected Date"; dateTime = @{} }
    @{ name = "PromptTuningIteration"; displayName = "Prompt Tuning Iteration"; number = @{} }
)

if ($correctionsListId) {
    $existingCorrCols = (Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$correctionsListId/columns").value

    foreach ($col in $correctionColumns) {
        $exists = $existingCorrCols | Where-Object { $_.name -eq $col.name }
        if ($exists) {
            Write-Host "  [EXISTS] $($col.displayName)" -ForegroundColor Yellow
            continue
        }
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would create: $($col.displayName)" -ForegroundColor DarkGray
            continue
        }
        Invoke-MgGraphRequest -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$correctionsListId/columns" `
            -Body ($col | ConvertTo-Json -Depth 5) `
            -ContentType "application/json" | Out-Null
        Write-Host "  [CREATED] $($col.displayName)" -ForegroundColor Green
    }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site           : $($site.displayName)"
Write-Host "  Document Library: $DocumentLibraryName (listId: $docListId)"
Write-Host "  Drive ID       : $($drive.id)"
Write-Host "  Review List    : $reviewListId"
Write-Host "  Corrections List: $correctionsListId"
Write-Host ""
Write-Host "Add these to local.settings.json:" -ForegroundColor Yellow
Write-Host "  SharePointReviewListSiteId = $siteId"
Write-Host "  SharePointReviewListId     = $reviewListId"
Write-Host ""
Write-Host "For batch runs, pass the library URL directly:" -ForegroundColor Yellow
Write-Host "  POST /api/batch { \"url\": \"https://<tenant>.sharepoint.com/sites/<site>/$DocumentLibraryName\" }"
Write-Host "  DriveId (for reference): $($drive.id)"
