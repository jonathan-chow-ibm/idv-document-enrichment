using System.Collections.Concurrent;
using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites.Item.Lists.Item.ContentTypes;
using Microsoft.Graph.Sites.Item.Lists.Item.Items.Item.Fields;
using Microsoft.Kiota.Abstractions;

namespace IdvEnrichment.Functions.Activities;

public sealed class WriteMetadataActivity(
    GraphServiceClient graphClient,
    TaxonomyLoader taxonomyLoader,
    ILogger<WriteMetadataActivity> logger)
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    // Content type name → id map, cached per driveId. See ADR-009.
    private static readonly ConcurrentDictionary<string, Lazy<Task<Dictionary<string, string>>>> ContentTypeCache = new();

    // Taxonomy field_name values (camelCase) for content.universal fields; must stay in sync with
    // docs/taxonomy/taxonomy.yaml. Anything not in this set falls through to TypeSpecificFields JSON.
    private static readonly HashSet<string> UniversalFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "documentStatus",
        "counterparty",
        "transactionType",
    };

    private static string GetFieldValue(IReadOnlyDictionary<string, CategoryClassification>? fields, string name)
        => fields is not null && fields.TryGetValue(name, out var f) ? f.Value ?? string.Empty : string.Empty;

    [Function(nameof(WriteMetadata))]
    public async Task WriteMetadata(
        [ActivityTrigger] WriteMetadataInput input,
        CancellationToken ct = default)
    {
        var result = input.Result;
        var metadataFields = result.Metadata?.Fields;

        // Content type PATCH first so type-specific columns are valid before the fields PATCH. Any failure
        // is logged and swallowed — the fields PATCH still runs and the document lands on the default type.
        await TrySetContentTypeAsync(input, ct);

        var fields = new FieldValueSet
        {
            AdditionalData = new Dictionary<string, object>
            {
                ["DocumentType"] = JsonSerializer.Serialize(result.TypeClassification.DocumentType).Trim('"'),
                ["DocumentStatus"] = GetFieldValue(metadataFields, "documentStatus"),
                ["Counterparty"] = GetFieldValue(metadataFields, "counterparty"),
                ["TransactionType"] = GetFieldValue(metadataFields, "transactionType"),
                ["AIConfidence"] = result.TypeClassification.Confidence,
                ["AIProcessingStatus"] = result.RoutingDecision == RoutingDecision.Write ? "Classified" : "Under Review",
                ["AIClassifiedDate"] = DateTimeOffset.UtcNow.ToString("o"),
                ["SuggestedFields"] = JsonSerializer.Serialize(result.Metadata?.SuggestedFields),
                ["AIOriginalClassification"] = JsonSerializer.Serialize(new { result.TypeClassification, result.Metadata }),
            }
        };

        // Graph SDK v5 does not expose the Fields sub-path via Drives.Items.ListItem;
        // use the raw-URL constructor on FieldsRequestBuilder to target the correct endpoint.
        var fieldsUrl = $"{GraphBaseUrl}/drives/{input.DriveId}/items/{input.ItemId}/listItem/fields";
        var fieldsBuilder = new FieldsRequestBuilder(fieldsUrl, graphClient.RequestAdapter);
        await fieldsBuilder.PatchAsync(fields, cancellationToken: ct);
    }

    private async Task TrySetContentTypeAsync(WriteMetadataInput input, CancellationToken ct)
    {
        try
        {
            var taxonomy = await taxonomyLoader.LoadAsync(ct);
            var group = taxonomy.GetGroupForDocumentType(input.Result.TypeClassification.DocumentType);
            if (string.IsNullOrWhiteSpace(group))
            {
                logger.LogWarning(
                    "Skipping content type PATCH for item {ItemId}: no taxonomy group for DocumentType {DocumentType}.",
                    input.ItemId, input.Result.TypeClassification.DocumentType);
                return;
            }

            var map = await GetContentTypeMapAsync(input.DriveId);
            if (!map.TryGetValue(group, out var contentTypeId) || string.IsNullOrEmpty(contentTypeId))
            {
                logger.LogWarning(
                    "Skipping content type PATCH for item {ItemId}: content type {Group} not found in drive {DriveId}.",
                    input.ItemId, group, input.DriveId);
                return;
            }

            // Graph SDK v5 doesn't expose PatchAsync on the drive-scoped ListItemRequestBuilder,
            // so send the PATCH via the shared request adapter (same pipeline the typed builders use).
            var listItemUrl = $"{GraphBaseUrl}/drives/{input.DriveId}/items/{input.ItemId}/listItem";
            var body = new ListItem { ContentType = new ContentTypeInfo { Id = contentTypeId } };
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.PATCH,
                URI = new Uri(listItemUrl),
            };
            requestInfo.Headers.TryAdd("Accept", "application/json");
            requestInfo.SetContentFromParsable(graphClient.RequestAdapter, "application/json", body);
            await graphClient.RequestAdapter.SendNoContentAsync(requestInfo, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Content type PATCH failed for item {ItemId} on drive {DriveId}; continuing with fields PATCH.",
                input.ItemId, input.DriveId);
        }
    }

    private Task<Dictionary<string, string>> GetContentTypeMapAsync(string driveId)
    {
        var lazy = ContentTypeCache.GetOrAdd(driveId, id =>
            new Lazy<Task<Dictionary<string, string>>>(
                () => LoadContentTypeMapAsync(id),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private async Task<Dictionary<string, string>> LoadContentTypeMapAsync(string driveId)
    {
        // CancellationToken.None: the map is process-wide cached; a single caller's cancellation
        // must not poison the cache for every subsequent document on the same drive.
        try
        {
            var url = $"{GraphBaseUrl}/drives/{driveId}/list/contentTypes";
            var builder = new ContentTypesRequestBuilder(url, graphClient.RequestAdapter);
            var response = await builder.GetAsync(cancellationToken: CancellationToken.None);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var contentType in response?.Value ?? [])
            {
                if (!string.IsNullOrEmpty(contentType.Name) && !string.IsNullOrEmpty(contentType.Id))
                {
                    map[contentType.Name] = contentType.Id;
                }
            }
            return map;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load content types for drive {DriveId}; content type PATCH will be skipped for items on this drive.",
                driveId);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
