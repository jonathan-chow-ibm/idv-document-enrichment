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

        // One flat `fields` object keyed by field_name: all taxonomy content fields plus any
        // document-type-specific fields (e.g., Design Drawing → discipline). Each value is a
        // {value, confidence, reasoning} classification.
        var fieldProperties = new Dictionary<string, object>();
        var fieldRequired = new List<string>();

        foreach (var field in taxonomy.ContentFields())
        {
            fieldProperties[field.FieldName] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" };
            fieldRequired.Add(field.FieldName);
        }

        foreach (var field in docTypeDef?.SpecificFields ?? [])
        {
            fieldProperties[field.Name] = new Dictionary<string, object> { ["$ref"] = "#/$defs/categoryClassification" };
            fieldRequired.Add(field.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["fields"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = fieldProperties,
                    ["required"] = fieldRequired,
                    ["additionalProperties"] = false,
                },
                ["suggestedFields"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["$ref"] = "#/$defs/suggestedField" },
                },
            },
            ["required"] = new[] { "fields", "suggestedFields" },
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
