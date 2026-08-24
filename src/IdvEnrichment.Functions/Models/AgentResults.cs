using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

public sealed record TypeClassificationResult(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("inputTokens")] int InputTokens = 0,
    [property: JsonPropertyName("outputTokens")] int OutputTokens = 0,
    [property: JsonPropertyName("durationMs")] int DurationMs = 0);

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

/// <summary>Agent 2 output: taxonomy-driven content fields (keyed by field_name) plus discovered suggestions.</summary>
public sealed record MetadataExtractionResult(
    [property: JsonPropertyName("fields")] IReadOnlyDictionary<string, CategoryClassification> Fields,
    [property: JsonPropertyName("suggestedFields")] IReadOnlyList<SuggestedField> SuggestedFields,
    [property: JsonPropertyName("inputTokens")] int InputTokens = 0,
    [property: JsonPropertyName("outputTokens")] int OutputTokens = 0,
    [property: JsonPropertyName("durationMs")] int DurationMs = 0);

/// <summary>Vision-based classification of a design drawing's first page.</summary>
public sealed record DrawingClassification(
    [property: JsonPropertyName("discipline")] string Discipline,
    [property: JsonPropertyName("sheetNumber")] string SheetNumber,
    [property: JsonPropertyName("drawingTitle")] string DrawingTitle,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("inputTokens")] int InputTokens = 0,
    [property: JsonPropertyName("outputTokens")] int OutputTokens = 0,
    [property: JsonPropertyName("durationMs")] int DurationMs = 0);
