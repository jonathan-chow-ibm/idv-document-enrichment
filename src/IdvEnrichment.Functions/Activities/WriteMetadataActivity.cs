using System.Collections.Concurrent;
using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites.Item.Lists.Item.Items.Item.Fields;
using Microsoft.Kiota.Abstractions;

namespace IdvEnrichment.Functions.Activities;

public sealed class WriteMetadataActivity(
    GraphServiceClient graphClient,
    TaxonomyLoader taxonomyLoader,
    ILogger<WriteMetadataActivity> logger)
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    // keyed by listId → (groupName → contentTypeId); populated once per listId across all invocations
    private static readonly ConcurrentDictionary<string, Lazy<Task<Dictionary<string, string>>>> _ctIdCache = new();

    [Function(nameof(WriteMetadata))]
    public async Task WriteMetadata(
        [ActivityTrigger] WriteMetadataInput input,
        CancellationToken ct = default)
    {
        var result = input.Result;
        var taxonomy = await taxonomyLoader.LoadAsync(ct);

        // PATCH 1: set content type (must precede field writes per ADR-009)
        var group = taxonomy.GetGroupForDocumentType(result.TypeClassification.DocumentType);
        if (group is not null)
        {
            var ctId = await ResolveContentTypeIdAsync(input.SiteId, input.DriveId, group, ct);
            if (ctId is not null)
            {
                try
                {
                    await PatchContentTypeAsync(input.DriveId, input.ItemId, ctId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Content type PATCH failed for item {ItemId} (group: {Group}); proceeding with field write-back", input.ItemId, group);
                }
            }
            else
            {
                logger.LogWarning("Content type ID not resolved for group '{Group}' on list derived from drive {DriveId}; skipping content type PATCH", group, input.DriveId);
            }
        }

        // PATCH 2: write field values (unchanged behaviour)
        var data = new Dictionary<string, object>
        {
            ["DocumentType"] = JsonSerializer.Serialize(result.TypeClassification.DocumentType).Trim('"'),
            ["AIConfidence"] = result.TypeClassification.Confidence,
            ["AIProcessingStatus"] = result.RoutingDecision == RoutingDecision.Write ? "Classified" : "Under Review",
            ["AIClassifiedDate"] = DateTimeOffset.UtcNow.ToString("o"),
        };

        if (result.Metadata is { } meta)
        {
            // Map each extracted content field (keyed by field_name) to its SharePoint column.
            var columnByField = taxonomy.ContentFields()
                .ToDictionary(f => f.FieldName, f => f.SharepointColumn, StringComparer.OrdinalIgnoreCase);

            foreach (var (fieldName, classification) in meta.Fields)
            {
                if (string.IsNullOrWhiteSpace(classification.Value))
                {
                    continue; // skip empty values so we don't clobber columns with blanks
                }

                var column = columnByField.TryGetValue(fieldName, out var col) && !string.IsNullOrEmpty(col)
                    ? col
                    : fieldName; // type-specific fields (e.g., discipline) fall back to their field name
                data[column] = classification.Value;
            }

            data["SuggestedFields"] = JsonSerializer.Serialize(meta.SuggestedFields);
            data["AIOriginalClassification"] = JsonSerializer.Serialize(new { result.TypeClassification, result.Metadata });
        }

        if (result.DrawingClassification is { } drawing)
        {
            if (!string.IsNullOrWhiteSpace(drawing.Discipline))
            {
                data["Discipline"] = drawing.Discipline;
            }

            if (!string.IsNullOrWhiteSpace(drawing.SheetNumber))
            {
                data["SheetNumber"] = drawing.SheetNumber;
            }

            if (!string.IsNullOrWhiteSpace(drawing.DrawingTitle))
            {
                data["DrawingTitle"] = drawing.DrawingTitle;
            }
        }

        var fields = new FieldValueSet { AdditionalData = data };

        // Graph SDK v5 does not expose the Fields sub-path via Drives.Items.ListItem;
        // use the raw-URL constructor on FieldsRequestBuilder to target the correct endpoint.
        var fieldsUrl = $"{GraphBaseUrl}/drives/{input.DriveId}/items/{input.ItemId}/listItem/fields";
        var fieldsBuilder = new FieldsRequestBuilder(fieldsUrl, graphClient.RequestAdapter);
        await fieldsBuilder.PatchAsync(fields, cancellationToken: ct);
    }

    private async Task PatchContentTypeAsync(string driveId, string itemId, string ctId, CancellationToken ct)
    {
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.PATCH,
            URI = new Uri($"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/listItem"),
        };
        var body = new ListItem { ContentType = new ContentTypeInfo { Id = ctId } };
        requestInfo.SetContentFromParsable(graphClient.RequestAdapter, "application/json", body);
        await graphClient.RequestAdapter.SendNoContentAsync(requestInfo, errorMapping: null, cancellationToken: ct);
    }

    private async Task<string?> ResolveContentTypeIdAsync(string siteId, string driveId, string groupName, CancellationToken ct)
    {
        try
        {
            var list = await graphClient.Drives[driveId].List.GetAsync(cancellationToken: ct);
            if (list?.Id is not { } listId) { return null; }

            var lazy = _ctIdCache.GetOrAdd(listId, _ => new Lazy<Task<Dictionary<string, string>>>(
                () => FetchContentTypeIdsAsync(siteId, listId, ct)));

            var ids = await lazy.Value;
            return ids.TryGetValue(groupName, out var ctId) ? ctId : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve content type ID for group '{Group}' (siteId: {SiteId}, driveId: {DriveId})", groupName, siteId, driveId);
            return null;
        }
    }

    private async Task<Dictionary<string, string>> FetchContentTypeIdsAsync(string siteId, string listId, CancellationToken cancellationToken)
    {
        var response = await graphClient.Sites[siteId].Lists[listId].ContentTypes.GetAsync(cancellationToken: cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contentType in response?.Value ?? [])
        {
            if (contentType.Name is not null && contentType.Id is not null)
            {
                result[contentType.Name] = contentType.Id;
            }
        }
        return result;
    }
}
