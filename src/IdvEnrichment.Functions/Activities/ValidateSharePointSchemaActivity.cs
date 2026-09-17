using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.List.Columns;
using Microsoft.Graph.Models;

namespace IdvEnrichment.Functions.Activities;

// Fails a batch before EnumerateLibrary reads a single document if the target library's SharePoint
// columns have drifted from the app's taxonomy.yaml (e.g. after taxonomy.yaml adds a Choice value but
// Provision-SharePointSchema.ps1 hasn't been re-run against this library). Without this, the drift
// surfaces at WriteMetadataActivity as a total PATCH failure per document: TryMatchAllowedValue only
// drops values that DON'T match taxonomy, so a value that's valid in taxonomy but missing from the
// deployed column reaches Graph, which rejects the whole ~20-field body -- the document lands correctly
// typed with zero metadata, not a partial write. This check catches that before a single document pays for it.
public sealed class ValidateSharePointSchemaActivity(
    GraphServiceClient graphClient,
    TaxonomyLoader taxonomyLoader,
    ILogger<ValidateSharePointSchemaActivity> logger)
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    [Function(nameof(ValidateSharePointSchema))]
    public async Task ValidateSharePointSchema(
        [ActivityTrigger] ValidateSharePointSchemaInput input,
        CancellationToken ct = default)
    {
        var target = input.Target;
        var taxonomy = await taxonomyLoader.LoadAsync(ct);

        // The list's own columns, not the site's -- a Choice column's allowed values are stored per
        // list, so a site-column read would see stale values even after this library was reprovisioned.
        // See ADR-009 (WriteMetadataActivity.GetContentTypeMapAsync uses the same /list/ scoping).
        var url = $"{GraphBaseUrl}/drives/{target.DriveId}/list/columns";
        var builder = new ColumnsRequestBuilder(url, graphClient.RequestAdapter);
        var response = await SdkExceptionHelper.RunAsync(
            () => builder.GetAsync(cancellationToken: ct),
            $"Reading list columns for library {target.LibraryName}",
            logger);

        var columns = (response?.Value ?? [])
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .ToDictionary(c => c.Name!, StringComparer.OrdinalIgnoreCase);

        var expected = ExpectedColumns(taxonomy, input.ClassifyOnly);
        var problems = FindSchemaProblems(columns, expected);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"SharePoint library '{target.LibraryName}' is out of sync with the app's taxonomy " +
                $"({string.Join("; ", problems)}). Re-run scripts/Provision-SharePointSchema.ps1 against this library.");
        }

        logger.LogInformation(
            "Schema check passed for library {LibraryName}: {ColumnCount} columns read, {CheckedCount} checked against taxonomy.",
            target.LibraryName, columns.Count, expected.Count);
    }

    // Written directly by WriteMetadataActivity.BuildFieldsPayload outside the taxonomy loop --
    // existence only, since these are text/number/date/JSON columns with no allowed values to
    // compare. A missing one gets the same Graph 400 that rejects the entire fields PATCH as a
    // taxonomy-invalid Choice value does. DocumentType is excluded here because it's checked above
    // with its real allowed values from the enum; AIProcessingStatus is excluded because
    // BuildFieldsPayload skips writing it for classify-only runs (see ProcessingStatusColumn).
    // Keep in sync with the literal AdditionalData[...] keys in BuildFieldsPayload -- there is a
    // guard test for this in WriteMetadataActivityTests.
    internal static readonly string[] RequiredColumns =
    [
        "AIConfidence", "AIClassifiedDate", "SuggestedFields",
        "AIOriginalClassification", "AISuggestedType",
    ];

    // AIProcessingStatus doubles as Power Automate's re-trigger guard, and BuildFieldsPayload
    // deliberately skips writing it for classify-only runs -- so it's only required when a full
    // run would actually write it.
    internal const string ProcessingStatusColumn = "AIProcessingStatus";

    // DocumentType's allowed values come from the enum, not taxonomy.yaml -- taxonomy.yaml's document_types
    // list is keyed by label already, but the enum (via JsonStringEnumMemberName) is what WriteMetadataActivity
    // actually serializes onto the column, so that's the source of truth to check against.
    //
    // classifyOnly narrows the taxonomy Choice columns and AIProcessingStatus out of the check:
    // DocumentOrchestrator leaves Metadata null for classify-only runs, so WriteMetadataActivity's
    // taxonomy.ContentFields() loop reads only empty strings and never reaches a taxonomy Choice
    // column with a real value, and BuildFieldsPayload skips AIProcessingStatus outright for them.
    // Everything in RequiredColumns, plus DocumentType, is written unconditionally either way.
    internal static IReadOnlyList<(string ColumnName, IReadOnlyList<string> ExpectedValues)> ExpectedColumns(
        TaxonomyData taxonomy, bool classifyOnly = false)
    {
        var documentTypeLabels = Enum.GetValues<DocumentType>()
            .Select(v => JsonSerializer.Serialize(v).Trim('"'))
            .ToList();

        var required = RequiredColumns.Select(name => (name, (IReadOnlyList<string>)Array.Empty<string>()));

        if (classifyOnly)
        {
            return [("DocumentType", documentTypeLabels), .. required];
        }

        var choiceColumns = taxonomy.ContentFields()
            .Where(f => f.AllowedValues.Count > 0 && !string.IsNullOrEmpty(f.SharepointColumn))
            .Select(f => (f.SharepointColumn, (IReadOnlyList<string>)f.AllowedValues));

        return
        [
            ("DocumentType", documentTypeLabels),
            .. required,
            (ProcessingStatusColumn, (IReadOnlyList<string>)Array.Empty<string>()),
            .. choiceColumns,
        ];
    }

    // Internal for testability: pure comparison against a Graph column snapshot, no SDK calls.
    internal static IReadOnlyList<string> FindSchemaProblems(
        IReadOnlyDictionary<string, ColumnDefinition> actualColumns,
        IReadOnlyList<(string ColumnName, IReadOnlyList<string> ExpectedValues)> expectedChoiceColumns)
    {
        var problems = new List<string>();

        foreach (var (columnName, expectedValues) in expectedChoiceColumns)
        {
            if (!actualColumns.TryGetValue(columnName, out var column))
            {
                problems.Add($"column '{columnName}' is missing");
                continue;
            }

            var actualValues = column.Choice?.Choices ?? [];
            var missing = expectedValues
                .Where(expected => !actualValues.Any(actual => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missing.Count > 0)
            {
                problems.Add($"column '{columnName}' is missing value(s): {string.Join(", ", missing)}");
            }
        }

        return problems;
    }
}
