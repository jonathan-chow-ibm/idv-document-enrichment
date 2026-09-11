using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

public sealed record TypeClassificationResult(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("inputTokens")] int InputTokens = 0,
    [property: JsonPropertyName("outputTokens")] int OutputTokens = 0,
    [property: JsonPropertyName("durationMs")] int DurationMs = 0,
    // Populated by Agent 1 only when it couldn't confidently settle on one type -- alternative
    // types it considered, so "Other"/low-confidence results are actionable instead of a dead end.
    [property: JsonPropertyName("candidates")] IReadOnlyList<ClassificationCandidate>? Candidates = null);

/// <summary>An alternative document type Agent 1 considered but didn't settle on.</summary>
public sealed record ClassificationCandidate(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("confidence")] double Confidence);

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
    // "Plat" | "Survey" | "Design Drawing" — lets the vision override pick the correct document type
    [property: JsonPropertyName("drawingType")] string DrawingType = "",
    [property: JsonPropertyName("inputTokens")] int InputTokens = 0,
    [property: JsonPropertyName("outputTokens")] int OutputTokens = 0,
    [property: JsonPropertyName("durationMs")] int DurationMs = 0);
