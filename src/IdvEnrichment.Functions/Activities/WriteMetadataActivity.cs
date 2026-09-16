using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
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

    // A few explicit formats for likely AI output ("March 15, 2026", "3/15/2026") in case plain TryParse misses them.
    private static readonly string[] DateTimeFormats =
    [
        "MMMM d, yyyy",
        "MMM d, yyyy",
        "M/d/yyyy",
        "M/d/yy",
        "yyyy-MM-dd",
    ];

    private static readonly Regex NumberUnitWordsPattern =
        new(@"\b(acres?|square\s*feet|sq\.?\s*ft\.?|sf)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumberNoiseCharsPattern = new(@"[\$,\s]", RegexOptions.Compiled);

    private static string GetFieldValue(IReadOnlyDictionary<string, CategoryClassification>? fields, string name)
        => fields is not null && fields.TryGetValue(name, out var f) ? f.Value ?? string.Empty : string.Empty;


    [Function(nameof(WriteMetadata))]
    public async Task WriteMetadata(
        [ActivityTrigger] WriteMetadataInput input,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);

        // Content type PATCH first so type-specific columns are valid before the fields PATCH. Any Graph
        // failure is logged and swallowed — the fields PATCH still runs and the document lands on the default type.
        await TrySetContentTypeAsync(input, taxonomy, ct);

        var fields = BuildFieldsPayload(input.Result, taxonomy, DateTimeOffset.UtcNow);

        // Graph SDK v5 does not expose the Fields sub-path via Drives.Items.ListItem;
        // use the raw-URL constructor on FieldsRequestBuilder to target the correct endpoint.
        var fieldsUrl = $"{GraphBaseUrl}/drives/{input.DriveId}/items/{input.ItemId}/listItem/fields";
        var fieldsBuilder = new FieldsRequestBuilder(fieldsUrl, graphClient.RequestAdapter);
        await SdkExceptionHelper.RunAsync(
            () => fieldsBuilder.PatchAsync(fields, cancellationToken: ct),
            $"Writing metadata fields for item {input.ItemId} on drive {input.DriveId}",
            logger);
    }

    // Internal for testability: pure mapping from an EnrichmentResult + taxonomy to the SharePoint fields PATCH body.
    internal static FieldValueSet BuildFieldsPayload(
        EnrichmentResult result,
        TaxonomyData taxonomy,
        DateTimeOffset classifiedAt)
    {
        var metadataFields = result.Metadata?.Fields;

        var data = new Dictionary<string, object>
        {
            ["DocumentType"] = JsonSerializer.Serialize(result.TypeClassification.DocumentType).Trim('"'),
            ["AIConfidence"] = result.TypeClassification.Confidence,
            ["AIClassifiedDate"] = classifiedAt.ToString("o"),
            ["SuggestedFields"] = JsonSerializer.Serialize(result.Metadata?.SuggestedFields),
            ["AIOriginalClassification"] = JsonSerializer.Serialize(new { result.TypeClassification, result.Metadata }),
        };

        // AIProcessingStatus doubles as Power Automate's re-trigger guard (see
        // docs/architecture/power-automate-integration.md) — "Classified"/"Under Review" tell the real
        // trigger flow this document was already handled and to skip it forever. A classify-only test
        // run must never set that guard, or the document would silently never get a real pipeline pass.
        if (!result.ClassifyOnly)
        {
            data["AIProcessingStatus"] = result.RoutingDecision == RoutingDecision.Write ? "Classified" : "Under Review";
        }

        // Populate every taxonomy content column using the taxonomy's declared field_name → sharepoint_column
        // mapping (e.g., parcelId → ParcelID). See ADR-009 for the schema-driven writeback rationale.
        // Each value is converted per the column's SharePoint type; an unmatched Choice value or an unparseable
        // DateTime/Number would otherwise cause Graph to reject the entire PATCH, so those are omitted instead.
        foreach (var spec in taxonomy.ContentFields())
        {
            if (string.IsNullOrEmpty(spec.SharepointColumn))
            {
                continue;
            }

            var rawValue = GetFieldValue(metadataFields, spec.FieldName);

            if (spec.AllowedValues.Count > 0)
            {
                if (TryMatchAllowedValue(spec.AllowedValues, rawValue, out var canonicalValue))
                {
                    data[spec.SharepointColumn] = canonicalValue;
                }
            }
            else if (spec.ValueType == "dateTime")
            {
                if (TryParseDateTime(rawValue, out var dateValue))
                {
                    data[spec.SharepointColumn] = dateValue.ToString("o");
                }
            }
            else if (spec.ValueType == "number")
            {
                if (TryParseNumber(rawValue, out var numberValue))
                {
                    data[spec.SharepointColumn] = numberValue;
                }
            }
            else
            {
                data[spec.SharepointColumn] = rawValue;
            }
        }

        return new FieldValueSet { AdditionalData = data };
    }

    // Case-insensitive match against a Choice column's allowed values; returns the allowed list's own casing
    // so Graph sees an exact match regardless of how the AI extracted the value.
    private static bool TryMatchAllowedValue(IReadOnlyList<string> allowedValues, string rawValue, out string canonicalValue)
    {
        foreach (var candidate in allowedValues)
        {
            if (string.Equals(candidate, rawValue, StringComparison.OrdinalIgnoreCase))
            {
                canonicalValue = candidate;
                return true;
            }
        }

        canonicalValue = string.Empty;
        return false;
    }

    private static bool TryParseDateTime(string rawValue, out DateTimeOffset value)
    {
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = default;
            return false;
        }

        return DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, styles, out value)
            || DateTimeOffset.TryParseExact(rawValue, DateTimeFormats, CultureInfo.InvariantCulture, styles, out value);
    }

    private static bool TryParseNumber(string rawValue, out double value)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = default;
            return false;
        }

        var withoutUnitWords = NumberUnitWordsPattern.Replace(rawValue, string.Empty);
        var cleaned = NumberNoiseCharsPattern.Replace(withoutUnitWords, string.Empty);
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }


    private async Task TrySetContentTypeAsync(WriteMetadataInput input, TaxonomyData taxonomy, CancellationToken ct)
    {
        try
        {
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
        catch (Exception ex) when (ex is ODataError or HttpRequestException)
        {
            logger.LogWarning(ex,
                "Content type PATCH failed for item {ItemId} on drive {DriveId}; continuing with fields PATCH.",
                input.ItemId, input.DriveId);
        }
    }

    private async Task<Dictionary<string, string>> GetContentTypeMapAsync(string driveId)
    {
        var lazy = ContentTypeCache.GetOrAdd(driveId, id =>
            new Lazy<Task<Dictionary<string, string>>>(
                () => LoadContentTypeMapAsync(id),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value;
        }
        catch
        {
            // Lazy<Task<>> caches faulted Tasks and ConcurrentDictionary caches the Lazy, so a transient Graph
            // failure would otherwise permanently disable content-type PATCH for this drive. Evict the entry so
            // the next call retries. The KeyValuePair overload avoids racing with a concurrent successful reload.
            ((ICollection<KeyValuePair<string, Lazy<Task<Dictionary<string, string>>>>>)ContentTypeCache)
                .Remove(new KeyValuePair<string, Lazy<Task<Dictionary<string, string>>>>(driveId, lazy));
            throw;
        }
    }

    private async Task<Dictionary<string, string>> LoadContentTypeMapAsync(string driveId)
    {
        // CancellationToken.None: the map is process-wide cached; a single caller's cancellation
        // must not poison the cache for every subsequent document on the same drive.
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
}
