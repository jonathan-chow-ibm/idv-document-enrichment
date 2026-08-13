using System.Text.Json;
using IdvEnrichment.Functions.Models;

namespace IdvEnrichment.Functions.Shared;

public static class MetadataSchemaBuilder
{
    private static readonly Dictionary<string, object> CategoryClassificationSchema = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["value"] = new Dictionary<string, object> { ["type"] = "string" },
            ["confidence"] = new Dictionary<string, object> { ["type"] = "number" },
            ["reasoning"] = new Dictionary<string, object> { ["type"] = "string" },
        },
        ["required"] = new[] { "value", "confidence", "reasoning" },
        ["additionalProperties"] = false,
    };

    private static readonly Dictionary<string, object> SuggestedFieldSchema = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["key"] = new Dictionary<string, object> { ["type"] = "string" },
            ["value"] = new Dictionary<string, object> { ["type"] = "string" },
            ["confidence"] = new Dictionary<string, object> { ["type"] = "number" },
        },
        ["required"] = new[] { "key", "value", "confidence" },
        ["additionalProperties"] = false,
    };

    public static BinaryData BuildSchema(DocumentType documentType, TaxonomyData taxonomy)
    {
        var docTypeDef = taxonomy.GetDocumentType(documentType);

        var typeSpecificProperties = new Dictionary<string, object>();
        var typeSpecificRequired = new List<string>();
        foreach (var field in docTypeDef?.SpecificFields ?? [])
        {
            typeSpecificProperties[field.Name] = new Dictionary<string, object>
            {
                ["$ref"] = "#/$defs/categoryClassification",
            };
            typeSpecificRequired.Add(field.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["dealType"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" },
                ["submarket"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" },
                ["counterparty"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" },
                ["confidentiality"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" },
                ["typeSpecificFields"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = typeSpecificProperties,
                    ["required"] = typeSpecificRequired,
                    ["additionalProperties"] = false,
                },
                ["suggestedFields"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/suggestedField" },
                },
            },
            ["required"] = new[] { "dealType", "submarket", "counterparty", "confidentiality", "typeSpecificFields", "suggestedFields" },
            ["additionalProperties"] = false,
            ["$defs"] = new Dictionary<string, object>
            {
                ["categoryClassification"] = CategoryClassificationSchema,
                ["suggestedField"] = SuggestedFieldSchema,
            },
        };

        return BinaryData.FromString(JsonSerializer.Serialize(schema));
    }
}
