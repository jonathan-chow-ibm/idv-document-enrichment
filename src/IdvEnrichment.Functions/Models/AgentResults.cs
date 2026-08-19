using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

public sealed record TypeClassificationResult(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning);

/// <summary>Extraction result for a single metadata field.</summary>
public sealed record CategoryClassification(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning);

/// <summary>An additional metadata field discovered by Agent 2 outside the defined schema.</summary>
public sealed record SuggestedField(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("confidence")] double Confidence);

/// <summary>Agent 2 output: common fields + type-specific fields + suggestions.</summary>
public sealed record MetadataExtractionResult(
    [property: JsonPropertyName("dealType")] CategoryClassification DealType,
    [property: JsonPropertyName("submarket")] CategoryClassification Submarket,
    [property: JsonPropertyName("counterparty")] CategoryClassification Counterparty,
    [property: JsonPropertyName("confidentiality")] CategoryClassification Confidentiality,
    [property: JsonPropertyName("typeSpecificFields")] IReadOnlyDictionary<string, CategoryClassification> TypeSpecificFields,
    [property: JsonPropertyName("suggestedFields")] IReadOnlyList<SuggestedField> SuggestedFields);
