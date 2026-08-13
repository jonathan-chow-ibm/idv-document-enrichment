using System.Text.Json;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Sites.Item.Lists.Item.Items.Item.Fields;

namespace IdvEnrichment.Functions.Activities;

public sealed class WriteMetadataActivity(GraphServiceClient graphClient)
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    [Function(nameof(WriteMetadata))]
    public async Task WriteMetadata(
        [ActivityTrigger] WriteMetadataInput input,
        CancellationToken ct = default)
    {
        var result = input.Result;

        var fields = new FieldValueSet
        {
            AdditionalData = new Dictionary<string, object>
            {
                ["DocumentType"] = JsonSerializer.Serialize(result.TypeClassification.DocumentType).Trim('"'),
                ["DealType"] = result.Metadata?.DealType.Value ?? string.Empty,
                ["Submarket"] = result.Metadata?.Submarket.Value ?? string.Empty,
                ["Counterparty"] = result.Metadata?.Counterparty.Value ?? string.Empty,
                ["Confidentiality"] = result.Metadata?.Confidentiality.Value ?? string.Empty,
                ["AIConfidence"] = result.TypeClassification.Confidence,
                ["AIProcessingStatus"] = result.RoutingDecision == RoutingDecision.Write ? "Classified" : "Under Review",
                ["AIClassifiedDate"] = DateTimeOffset.UtcNow.ToString("o"),
                ["TypeSpecificFields"] = JsonSerializer.Serialize(result.Metadata?.TypeSpecificFields),
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
}
