using System.Text.Json;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites.Item.Lists.Item.Items.Item.Fields;

namespace IdvEnrichment.Functions.Activities;

public sealed class WriteMetadataActivity(GraphServiceClient graphClient, TaxonomyLoader taxonomyLoader)
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    [Function(nameof(WriteMetadata))]
    public async Task WriteMetadata(
        [ActivityTrigger] WriteMetadataInput input,
        CancellationToken ct = default)
    {
        var result = input.Result;
        var taxonomy = await taxonomyLoader.LoadAsync(ct);

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
                data["DrawingDiscipline"] = drawing.Discipline;
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
}
