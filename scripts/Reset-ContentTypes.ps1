<#
.SYNOPSIS
    Resets documents in one or more SharePoint document libraries back to the library's default
    ("Document") content type, and optionally clears the metadata values the enrichment pipeline
    wrote — a clean reset that leaves the provisioned schema in place.

    Use this to:
      * Discard a bad batch and re-run the pipeline against the same library (-ClearFields).
      * Undo a batch that typed documents with the wrong content type.
      * Clear content type usage BEFORE running Remove-SharePointSchema.ps1 — SharePoint
        refuses to delete a list content type while items still reference it.

    Schema (columns and content types) is never modified. Use Remove-SharePointSchema.ps1 for
    a full teardown.

    Folders are never touched — only items whose current content type derives from Document
    (id prefix 0x0101) are eligible. Folders are 0x0120.

.PARAMETER SiteUrl
    SharePoint site URL. Example: "contoso.sharepoint.com:/sites/ActiveProjects"

.PARAMETER DocumentLibraryName
    Display name of a single target document library. Mutually exclusive with -AllLibraries.

.PARAMETER AllLibraries
    Discover and process every visible document library on the site. Mutually exclusive with
    -DocumentLibraryName. SharePoint system libraries (Site Assets, Style Library, Site Pages,
    Form Templates, _catalogs, Preservation Hold) are never processed on any code path — they
    are matched by server-relative URL, not display name. Hidden libraries are skipped.

.PARAMETER ExcludeLibraries
    Additional libraries to treat as non-project content. Defaults match
    Provision-SharePointSchema.ps1 so the same invocation targets the same set of libraries.

.PARAMETER ListLibraries
    List the site's document libraries showing which would be targeted and which skipped (and
    why), then exit without changing anything. Use this before a real run.

.PARAMETER ClearFields
    Also null out the metadata values written by the pipeline, producing an as-uploaded state.

    Resetting content type alone does NOT clear values — they stay in the list row, invisible in
    the Document form but still returned by the API, visible in any view including those columns,
    and still indexed by search (so they keep grounding Copilot). -ClearFields is what removes them.

    The column set is derived from taxonomy.yaml rather than hardcoded, so it cannot drift from
    the schema. It is the 6 AI operational columns plus metadata.content.{universal,property,
    transaction,ownership} — exactly what WriteMetadataActivity.BuildFieldsPayload writes.
    Deliberately excluded:
      * metadata.folder_derived (State, PropertyName, ProjectName) — these come from SharePoint
        folder default column values, not the AI, and must survive a reset.
      * metadata.system (Title, Author, Modified, SourceSystem) — not written by the pipeline.
        Author and Modified are read-only built-ins; including them would 400 the whole PATCH.

.PARAMETER TaxonomyPath
    Path to taxonomy.yaml, used to derive the -ClearFields column set. Defaults to
    docs/taxonomy/taxonomy.yaml relative to this script.

.PARAMETER ClearColumns
    Explicit internal column names to clear, bypassing taxonomy.yaml. For targeted cleanup of a
    specific bad column. Implies -ClearFields.

.PARAMETER TargetContentTypeName
    Fallback name of the content type to reset items to. Default: "Document".

    This is only consulted if Graph does not report a default content type for the library. The
    primary target is whichever non-IDV content type has order.default = true — that is the type
    a document actually receives on upload, so resetting to it restores the as-uploaded state.
    Second fallback (localized tenants, where the name is not "Document") is the shortest content
    type id under the 0x0101 prefix, which is the base Document type.

.PARAMETER OnlyIdvContentTypes
    Restrict the content type reset to items currently assigned a content type in the
    "IDV Document Types" group. Without this switch, ANY document-derived content type that isn't
    the target is reset, including content types the client created themselves. Has no effect on
    -ClearFields, which is scoped by column rather than by content type.

.PARAMETER Limit
    Process at most this many items per library. Use for a smoke test before a full run.

.PARAMETER FailureReportPath
    Write per-item failures to this CSV. Default: no file, failures are printed only.

.PARAMETER DryRun
    Report what would change — per content type, and how many items carry values to clear —
    without making changes.

.EXAMPLE
    # Clean reset, dry run first — ALWAYS do this
    .\Reset-ContentTypes.ps1 -SiteUrl "contoso.sharepoint.com:/sites/ActiveProjects" -AllLibraries -ClearFields -DryRun

.EXAMPLE
    # Smoke test the clean reset on 5 documents
    .\Reset-ContentTypes.ps1 -SiteUrl "..." -DocumentLibraryName "TX DFW Risinger" -ClearFields -Limit 5

.EXAMPLE
    # Full clean reset, ready for a pipeline re-run
    .\Reset-ContentTypes.ps1 -SiteUrl "..." -AllLibraries -ClearFields `
        -FailureReportPath ./reports/ct-reset-failures.csv

.EXAMPLE
    # Content type only, no value clearing (prepares for Remove-SharePointSchema.ps1)
    .\Reset-ContentTypes.ps1 -SiteUrl "..." -AllLibraries -OnlyIdvContentTypes
#>
[CmdletBinding(DefaultParameterSetName = "Single")]
param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [Parameter(ParameterSetName = "Single")]
    [string]$DocumentLibraryName = "Documents",

    [Parameter(ParameterSetName = "All")]
    [switch]$AllLibraries,

    [Parameter(ParameterSetName = "All")]
    [string[]]$ExcludeLibraries = @('Documents', 'Template', 'Images', 'Pages'),

    [switch]$ListLibraries,

    [switch]$ClearFields,

    [string]$TaxonomyPath,

    [string[]]$ClearColumns,

    [string]$TargetContentTypeName = "Document",

    [switch]$OnlyIdvContentTypes,

    [int]$Limit = 0,

    [string]$FailureReportPath,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$ctGroup = "IDV Document Types"

if ($ClearColumns) { $ClearFields = $true }

# --- Columns written by the pipeline ----------------------------------------------------------
# The fixed operational columns set in WriteMetadataActivity.BuildFieldsPayload. AIProcessingStatus
# is included deliberately: it is Power Automate's re-trigger guard, so a reset that leaves it as
# "Classified" would cause the trigger flow to skip these documents forever on the re-run.
$aiColumns = @(
    "DocumentType", "AIConfidence", "AIProcessingStatus", "AIClassifiedDate",
    "SuggestedFields", "AIOriginalClassification"
)

# Parse metadata.content.{universal,property,transaction,ownership} out of taxonomy.yaml. Reading
# the taxonomy rather than hardcoding avoids adding a fourth place the field set has to be kept in
# sync (see docs/runbook-configuration.md). Only the `content:` block is read — folder_derived and
# system columns are not pipeline-written and must not be cleared.
function Get-TaxonomyContentColumns {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Taxonomy not found at '$Path'. Pass -TaxonomyPath, or -ClearColumns to bypass it."
    }

    $inContent = $false
    $columns = @()
    foreach ($line in (Get-Content -LiteralPath $Path)) {
        if ($line -match '^\s*#') { continue }
        if ($line -match '^  content:\s*$') { $inContent = $true; continue }
        # Any following key at 0- or 2-space indent closes the content block.
        if ($inContent -and $line -match '^(\S|  \S)') { break }
        if ($inContent -and $line -match 'sharepoint_column:\s*"?([A-Za-z0-9_]+)"?') {
            $columns += $Matches[1]
        }
    }

    if ($columns.Count -eq 0) {
        throw "Parsed 0 content columns from '$Path'. The taxonomy layout may have changed — pass -ClearColumns explicitly."
    }
    return $columns
}

$clearColumnSet = @()
if ($ClearFields) {
    if ($ClearColumns) {
        $clearColumnSet = @($ClearColumns | Select-Object -Unique)
        Write-Host "Clear set: $($clearColumnSet.Count) column(s) from -ClearColumns" -ForegroundColor Cyan
    } else {
        if (-not $TaxonomyPath) {
            $TaxonomyPath = Join-Path (Split-Path -Parent $PSScriptRoot) "docs/taxonomy/taxonomy.yaml"
        }
        $contentColumns = Get-TaxonomyContentColumns -Path $TaxonomyPath
        $clearColumnSet = @($aiColumns + $contentColumns | Select-Object -Unique)
        Write-Host "Clear set: $($clearColumnSet.Count) columns ($($aiColumns.Count) operational + $($contentColumns.Count) content from $(Split-Path -Leaf $TaxonomyPath))" -ForegroundColor Cyan
    }
    Write-Verbose "Clear set: $($clearColumnSet -join ', ')"
}

# --- Connect ---
# Sites.ReadWrite.All is enough to PATCH an item's contentType and fields; Sites.Manage.All is
# requested so this can run in the same session as Provision-/Remove-SharePointSchema.ps1.
Connect-MgGraph -Scopes "Sites.ReadWrite.All", "Sites.Manage.All"

$site = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$SiteUrl"
$siteId = $site.id
Write-Host "Site: $($site.displayName) ($siteId)" -ForegroundColor Cyan

# --- Graph call with throttle handling -------------------------------------------------------
# Up to two PATCHes per document means a full library run issues tens of thousands of writes, well
# into SharePoint's throttling range. Invoke-MgGraphRequest surfaces 429 as a terminating error, so
# retry here rather than letting a long run die on a transient throttle.
function Invoke-GraphWithRetry {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Uri,
        [string]$Body,
        [int]$MaxAttempts = 5
    )

    for ($attempt = 1; ; $attempt++) {
        try {
            if ($Body) {
                return Invoke-MgGraphRequest -Method $Method -Uri $Uri -Body $Body -ContentType "application/json" -ErrorAction Stop
            }
            return Invoke-MgGraphRequest -Method $Method -Uri $Uri -ErrorAction Stop
        }
        catch {
            $status = $null
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }

            if ($attempt -ge $MaxAttempts -or @(429, 500, 502, 503, 504) -notcontains $status) { throw }

            # Honour Retry-After when SharePoint sends it — it knows the real backoff window.
            $wait = 0
            try {
                $retryAfter = $_.Exception.Response.Headers.RetryAfter
                if ($retryAfter.Delta) {
                    $wait = [int]$retryAfter.Delta.TotalSeconds
                } elseif ($retryAfter.Date) {
                    $wait = [int](([datetimeoffset]$retryAfter.Date) - [datetimeoffset]::UtcNow).TotalSeconds
                }
            } catch { }
            if ($wait -le 0) { $wait = [Math]::Min(60, [Math]::Pow(2, $attempt)) }

            Write-Host "      [RETRY $attempt/$MaxAttempts] HTTP $status — waiting $($wait)s" -ForegroundColor DarkYellow
            Start-Sleep -Seconds $wait
        }
    }
}

# --- System libraries: never processed, regardless of parameters ------------------------------
# Matched on the library's server-relative URL rather than its display name — display names are
# localized and users rename libraries, so a name-based rule silently stops protecting.
# Kept identical to Provision-SharePointSchema.ps1 so both scripts agree on what is a target.
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
$allSiteLists = (Invoke-GraphWithRetry -Method GET -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists").value
$allDocLibraries = @($allSiteLists | Where-Object { $_.list.template -eq "documentLibrary" })

if ($ListLibraries) {
    Write-Host "`nDocument libraries on this site:" -ForegroundColor Cyan
    foreach ($lib in ($allDocLibraries | Sort-Object displayName)) {
        $reason = if (Test-IsSystemLibrary $lib) { "system (never processed)" }
                  elseif ($lib.list.hidden)      { "hidden (skipped)" }
                  elseif ($AllLibraries -and $ExcludeLibraries -contains $lib.displayName) { "in -ExcludeLibraries" }
                  else { "" }
        $colour = if ($reason) { "DarkGray" } else { "White" }
        $label  = if ($reason) { "  [skip]   {0,-38} {1}" } else { "  [target] {0,-38} {1}" }
        Write-Host ($label -f $lib.displayName, $reason) -ForegroundColor $colour
    }
    Write-Host ""
    return
}

# Applied to every code path: a system library is never a target.
$systemLibraries = @($allDocLibraries | Where-Object { Test-IsSystemLibrary $_ })
$candidateLists  = @($allDocLibraries | Where-Object { -not (Test-IsSystemLibrary $_) })
if ($systemLibraries.Count -gt 0) {
    Write-Host "System libraries skipped: $($systemLibraries.displayName -join ', ')" -ForegroundColor DarkGray
}

if ($AllLibraries) {
    $targetLists = @($candidateLists | Where-Object { -not $_.list.hidden })
    if ($ExcludeLibraries.Count -gt 0) {
        $targetLists = @($targetLists | Where-Object { $ExcludeLibraries -notcontains $_.displayName })
    }
    if ($targetLists.Count -eq 0) { throw "No visible document libraries found on this site after exclusions." }
    Write-Host "Libraries discovered ($($targetLists.Count)):" -ForegroundColor Cyan
    foreach ($l in $targetLists) { Write-Host "  - $($l.displayName)" -ForegroundColor White }
} else {
    $targetLists = @($candidateLists | Where-Object { $_.displayName -eq $DocumentLibraryName })
    if ($targetLists.Count -eq 0) {
        # Distinguish "system library, refused" from "no such library" — the fix differs.
        if ($systemLibraries.displayName -contains $DocumentLibraryName) {
            throw "'$DocumentLibraryName' is a SharePoint system library and is never processed."
        }
        throw "Library '$DocumentLibraryName' not found. Available: $($candidateLists.displayName -join ', ')"
    }
    Write-Host "Library: $($targetLists[0].displayName)" -ForegroundColor Cyan
}

Write-Host "Mode: reset content type$(if ($ClearFields) { ' + clear values' })$(if ($DryRun) { '  [DRY RUN — nothing will be modified]' })" -ForegroundColor $(if ($DryRun) { "Yellow" } else { "White" })

# --- Resolve the content type to reset to -----------------------------------------------------
# What a document gets on upload is the library's DEFAULT content type — the visible type at
# position 1 in the library's content type order. On a stock library that is the built-in
# Document (0x0101), and addCopy appends rather than reorders, so provisioning the IDV types
# leaves Document as the default. Resetting to the default therefore restores documents to
# exactly the state an upload produces.
#
# Graph reports this directly via contentType.order.default, so ask rather than infer. The two
# fallbacks cover libraries where Graph returns a null `order` on list content types:
#   1. name match on -TargetContentTypeName ("Document")
#   2. shortest id under the 0x0101 prefix — addCopy extends that prefix with a hex suffix, so
#      the shortest is the base type. Survives a localized tenant where the name isn't "Document".
function Resolve-TargetContentType {
    param($ListContentTypes)

    # A library whose default was re-pointed at an IDV type is a deliberate configuration choice,
    # but resetting TO an IDV type would defeat the purpose of this script — so exclude the group
    # here and report the discrepancy to the caller instead.
    $eligible = @($ListContentTypes | Where-Object { $_.group -ne $ctGroup })

    $default = @($eligible | Where-Object { $_.order.default -eq $true })
    if ($default.Count -gt 0) { return @{ CT = $default[0]; Source = "order.default (position $($default[0].order.position))" } }

    $named = @($eligible | Where-Object { $_.name -eq $TargetContentTypeName })
    if ($named.Count -gt 0) { return @{ CT = $named[0]; Source = "name match" } }

    $derived = @($eligible | Where-Object { $_.id -like '0x0101*' } | Sort-Object { $_.id.Length })
    if ($derived.Count -gt 0) { return @{ CT = $derived[0]; Source = "shortest 0x0101 id" } }

    return $null
}

# --- Process each library ---------------------------------------------------------------------
$summary  = @()
$failures = @()

foreach ($targetList in $targetLists) {
    $docListId   = $targetList.id
    $docListName = $targetList.displayName
    Write-Host "`n=== Library: $docListName ===" -ForegroundColor Cyan

    $libCTs = (Invoke-GraphWithRetry -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/contentTypes").value

    $resolved = Resolve-TargetContentType -ListContentTypes $libCTs
    if (-not $resolved) {
        Write-Host "  [SKIP] No non-IDV content type to reset to. Present: $(($libCTs.name | Sort-Object) -join ', ')" -ForegroundColor Yellow
        $summary += [PSCustomObject]@{ Library = $docListName; Eligible = 0; Cleared = 0; Reset = 0; Failed = 0; Note = "no target content type" }
        continue
    }
    $baseCT = $resolved.CT
    Write-Host "  Target content type: $($baseCT.name)  ($($baseCT.id))  [via $($resolved.Source)]" -ForegroundColor DarkGray

    # If the library's default has been re-pointed at an IDV type, new uploads already arrive typed
    # and this reset diverges from the as-uploaded state. Surface it rather than silently proceeding.
    $libDefault = @($libCTs | Where-Object { $_.order.default -eq $true })
    if ($libDefault.Count -gt 0 -and $libDefault[0].group -eq $ctGroup) {
        Write-Host "  [WARN] Library default content type is '$($libDefault[0].name)' (an IDV type)." -ForegroundColor Yellow
        Write-Host "         New uploads land on it, so this reset will not match the upload default." -ForegroundColor Yellow
    }

    $idvCtIds = @($libCTs | Where-Object { $_.group -eq $ctGroup } | Select-Object -ExpandProperty id)
    if ($OnlyIdvContentTypes -and $idvCtIds.Count -eq 0) {
        Write-Host "  [NOTE] -OnlyIdvContentTypes set but library has no '$ctGroup' content types — no content type resets here." -ForegroundColor DarkGray
    }

    # --- Narrow the clear set to columns this library can actually accept ---
    # A column missing from the library, or a read-only one, makes Graph reject the ENTIRE fields
    # PATCH — which would silently clear nothing. Intersect with the live column set instead.
    $libClearColumns = @()
    if ($ClearFields) {
        $libColumns = (Invoke-GraphWithRetry -Method GET `
            -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/columns").value

        $writable = @{}
        foreach ($lc in $libColumns) {
            if ($lc.name -and -not $lc.readOnly) { $writable[$lc.name] = $true }
        }
        $libClearColumns = @($clearColumnSet | Where-Object { $writable.ContainsKey($_) })
        $missing = @($clearColumnSet | Where-Object { -not $writable.ContainsKey($_) })

        if ($libClearColumns.Count -eq 0) {
            Write-Host "  [NOTE] None of the $($clearColumnSet.Count) clear-set columns exist as writable columns here — content type only." -ForegroundColor Yellow
        } else {
            Write-Host "  Clearing $($libClearColumns.Count) of $($clearColumnSet.Count) columns$(if ($missing.Count) { " ($($missing.Count) absent/read-only: $($missing -join ', '))" })" -ForegroundColor DarkGray
        }
    }

    # --- Enumerate items (all pages) ---
    # Enumerate fully before writing: a mid-enumeration PATCH can shift the paging window, and a
    # complete list is what makes -DryRun and the breakdown accurate. contentType is part of the
    # default listItem projection. Selecting only the columns in play keeps the payload small AND
    # lets us tell an already-clean item from a dirty one, so a re-run is cheap.
    $selectFields = @('FileLeafRef') + $libClearColumns
    $expand = "fields(`$select=$($selectFields -join ','))"
    $uri = "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/items?`$expand=$expand&`$top=200"

    Write-Host "  Enumerating items" -NoNewline
    $eligible = [System.Collections.Generic.List[object]]::new()
    $scanned = 0
    $nonDocument = 0

    while ($uri) {
        $page = Invoke-GraphWithRetry -Method GET -Uri $uri
        foreach ($item in $page.value) {
            $scanned++
            $itemCtId = $item.contentType.id
            if (-not $itemCtId) { continue }
            if ($itemCtId -notlike '0x0101*') { $nonDocument++; continue }   # folders (0x0120) etc.

            # Content type needs resetting?
            $needsCt = $itemCtId -ne $baseCT.id
            if ($needsCt -and $OnlyIdvContentTypes -and $idvCtIds -notcontains $itemCtId) { $needsCt = $false }

            # Any values to clear? Graph omits empty fields entirely, so presence means populated.
            $dirty = @()
            foreach ($col in $libClearColumns) {
                $val = $item.fields.$col
                if ($null -ne $val -and "$val" -ne '') { $dirty += $col }
            }

            if (-not $needsCt -and $dirty.Count -eq 0) { continue }

            $eligible.Add([PSCustomObject]@{
                Id         = $item.id
                Name       = $item.fields.FileLeafRef
                CtId       = $itemCtId
                CtName     = $item.contentType.name
                NeedsCt    = $needsCt
                DirtyCols  = $dirty
            })
        }
        $uri = $page.'@odata.nextLink'
        Write-Host "." -NoNewline
    }
    Write-Host " $scanned scanned ($nonDocument folders/non-document items left alone)" -ForegroundColor DarkGray

    if ($eligible.Count -eq 0) {
        Write-Host "  Nothing to do — already clean." -ForegroundColor DarkGray
        $summary += [PSCustomObject]@{ Library = $docListName; Eligible = 0; Cleared = 0; Reset = 0; Failed = 0; Note = "" }
        continue
    }

    $ctCount    = @($eligible | Where-Object { $_.NeedsCt }).Count
    $clearCount = @($eligible | Where-Object { $_.DirtyCols.Count -gt 0 }).Count
    Write-Host "  $($eligible.Count) item(s) to change: $ctCount content type reset, $clearCount with values to clear" -ForegroundColor DarkGray

    if ($ctCount -gt 0) {
        Write-Host "  Current content types:" -ForegroundColor DarkGray
        foreach ($grp in ($eligible | Where-Object { $_.NeedsCt } | Group-Object CtName | Sort-Object Count -Descending)) {
            Write-Host ("    {0,-24} {1,7}" -f $grp.Name, $grp.Count) -ForegroundColor DarkGray
        }
    }
    if ($clearCount -gt 0) {
        Write-Host "  Populated columns:" -ForegroundColor DarkGray
        $colCounts = @{}
        foreach ($e in $eligible) { foreach ($c in $e.DirtyCols) { $colCounts[$c] = 1 + [int]$colCounts[$c] } }
        foreach ($c in ($colCounts.GetEnumerator() | Sort-Object Value -Descending)) {
            Write-Host ("    {0,-24} {1,7}" -f $c.Key, $c.Value) -ForegroundColor DarkGray
        }
    }

    $work = if ($Limit -gt 0) { @($eligible | Select-Object -First $Limit) } else { @($eligible) }
    if ($Limit -gt 0 -and $eligible.Count -gt $Limit) {
        Write-Host "  -Limit $Limit — processing $($work.Count) of $($eligible.Count) eligible items." -ForegroundColor Yellow
    }

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would change $($work.Count) item(s)." -ForegroundColor Yellow
        $summary += [PSCustomObject]@{ Library = $docListName; Eligible = $eligible.Count; Cleared = 0; Reset = 0; Failed = 0; Note = "dry run" }
        continue
    }

    # --- Apply ---
    # Order is clear-then-retype, the reverse of the pipeline's write order (ADR-009), for two reasons:
    #   1. While the item still holds its IDV content type, every clear-set column is unambiguously
    #      valid for it.
    #   2. If the clear fails, the item stays visibly typed as e.g. "Contracts" — a discoverable bad
    #      state. The reverse order would leave a Document-typed item holding stale values that are
    #      invisible in the form but still indexed by search.
    # One body per library, not per item: nulling an already-null column is a no-op.
    $clearBody = $null
    if ($libClearColumns.Count -gt 0) {
        $payload = [ordered]@{}
        foreach ($col in $libClearColumns) { $payload[$col] = $null }
        $clearBody = $payload | ConvertTo-Json -Depth 3
    }
    $ctBody = @{ contentType = @{ id = $baseCT.id } } | ConvertTo-Json -Depth 3

    $cleared = 0; $reset = 0; $failed = 0; $index = 0

    foreach ($doc in $work) {
        $index++
        if ($index % 25 -eq 0 -or $index -eq $work.Count) {
            Write-Progress -Activity "Resetting $docListName" `
                -Status "$index of $($work.Count) — $cleared cleared, $reset retyped, $failed failed" `
                -PercentComplete ([Math]::Min(100, $index * 100 / $work.Count))
        }

        # 1. Clear values
        if ($clearBody -and $doc.DirtyCols.Count -gt 0) {
            try {
                Invoke-GraphWithRetry -Method PATCH `
                    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/items/$($doc.Id)/fields" `
                    -Body $clearBody | Out-Null
                $cleared++
            }
            catch {
                $failed++
                $failures += [PSCustomObject]@{
                    Library = $docListName; ItemId = $doc.Id; FileName = $doc.Name
                    FromCT = $doc.CtName; Stage = "clear-fields"; Error = $_.Exception.Message
                }
                Write-Host "    [FAIL clear] $($doc.Name) (item $($doc.Id)): $($_.Exception.Message)" -ForegroundColor Red
                continue    # leave the content type as-is so the failure stays visible in the library
            }
        }

        # 2. Reset content type
        if ($doc.NeedsCt) {
            try {
                Invoke-GraphWithRetry -Method PATCH `
                    -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/lists/$docListId/items/$($doc.Id)" `
                    -Body $ctBody | Out-Null
                $reset++
            }
            catch {
                $failed++
                $failures += [PSCustomObject]@{
                    Library = $docListName; ItemId = $doc.Id; FileName = $doc.Name
                    FromCT = $doc.CtName; Stage = "set-content-type"; Error = $_.Exception.Message
                }
                Write-Host "    [FAIL retype] $($doc.Name) (item $($doc.Id)): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    Write-Progress -Activity "Resetting $docListName" -Completed

    Write-Host "  [DONE] $cleared cleared, $reset retyped, $failed failed." -ForegroundColor Green
    $summary += [PSCustomObject]@{ Library = $docListName; Eligible = $eligible.Count; Cleared = $cleared; Reset = $reset; Failed = $failed; Note = "" }
}

# --- Summary ---
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Site : $($site.displayName)"
Write-Host "  Mode : $(if ($DryRun) { 'DRY RUN' } else { 'APPLIED' }) — content type$(if ($ClearFields) { ' + values' })$(if ($OnlyIdvContentTypes) { ' (IDV content types only)' })"
Write-Host ""
$summary | Format-Table -AutoSize Library, Eligible, Cleared, Reset, Failed, Note | Out-Host

$totalCleared = ($summary | Measure-Object -Property Cleared -Sum).Sum
$totalReset   = ($summary | Measure-Object -Property Reset   -Sum).Sum
$totalFailed  = ($summary | Measure-Object -Property Failed  -Sum).Sum
Write-Host "  Values cleared : $totalCleared"
Write-Host "  Retyped        : $totalReset"
Write-Host "  Failed         : $totalFailed" -ForegroundColor $(if ($totalFailed -gt 0) { "Red" } else { "Gray" })

if ($failures.Count -gt 0 -and $FailureReportPath) {
    $dir = Split-Path -Parent $FailureReportPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $failures | Export-Csv -Path $FailureReportPath -NoTypeInformation -Encoding UTF8
    Write-Host "  Failures written to: $FailureReportPath" -ForegroundColor Yellow
}

Write-Host ""
if (-not $ClearFields) {
    Write-Host "Content types reset, but column VALUES remain — still API-readable, view-visible," -ForegroundColor DarkGray
    Write-Host "and search-indexed. Re-run with -ClearFields for a clean reset." -ForegroundColor DarkGray
} else {
    Write-Host "Clean reset complete. Schema is intact, so the pipeline can be re-run against these" -ForegroundColor DarkGray
    Write-Host "libraries without re-provisioning." -ForegroundColor DarkGray
}
