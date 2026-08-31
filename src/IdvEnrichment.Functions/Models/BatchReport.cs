using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

/// <summary>Slim projection passed from ChunkOrchestrator → BatchOrchestrator → GenerateBatchReport; excludes extracted text to avoid OOM at 100K scale.</summary>
public sealed record BatchDocumentEntry(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("routingDecision")] RoutingDecision RoutingDecision,
    [property: JsonPropertyName("typeConfidence")] double TypeConfidence,
    [property: JsonPropertyName("writeBackSucceeded")] bool WriteBackSucceeded,
    [property: JsonPropertyName("classificationInputTokens")] int ClassificationInputTokens = 0,
    [property: JsonPropertyName("classificationOutputTokens")] int ClassificationOutputTokens = 0,
    [property: JsonPropertyName("extractionInputTokens")] int ExtractionInputTokens = 0,
    [property: JsonPropertyName("extractionOutputTokens")] int ExtractionOutputTokens = 0,
    [property: JsonPropertyName("visionInputTokens")] int VisionInputTokens = 0,
    [property: JsonPropertyName("visionOutputTokens")] int VisionOutputTokens = 0,
    [property: JsonPropertyName("suggestedFieldKeys")] IReadOnlyList<string> SuggestedFieldKeys = null!);

public sealed record ChunkResult(
    [property: JsonPropertyName("results")] IReadOnlyList<BatchDocumentEntry> Results,
    [property: JsonPropertyName("errors")] int Errors);

public sealed record GenerateBatchReportInput(
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("results")] IReadOnlyList<BatchDocumentEntry> Results,
    [property: JsonPropertyName("errors")] int Errors);

public sealed record BatchReport(
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("summary")] BatchSummary Summary,
    [property: JsonPropertyName("confidenceDistribution")] ConfidenceDistribution ConfidenceDistribution,
    [property: JsonPropertyName("documentTypeCounts")] IReadOnlyDictionary<string, int> DocumentTypeCounts,
    [property: JsonPropertyName("topSuggestedFields")] IReadOnlyList<SuggestedFieldEntry> TopSuggestedFields,
    [property: JsonPropertyName("cost")] BatchCost Cost);

public sealed record SuggestedFieldEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("documentCount")] int DocumentCount,
    [property: JsonPropertyName("exampleValues")] IReadOnlyList<string> ExampleValues);

public sealed record BatchSummary(
    [property: JsonPropertyName("totalDocuments")] int TotalDocuments,
    [property: JsonPropertyName("classified")] int Classified,
    [property: JsonPropertyName("underReview")] int UnderReview,
    [property: JsonPropertyName("writeBackFailed")] int WriteBackFailed,
    [property: JsonPropertyName("errors")] int Errors,
    [property: JsonPropertyName("skipped")] int Skipped);

public sealed record ConfidenceDistribution(
    [property: JsonPropertyName("high")] int High,
    [property: JsonPropertyName("medium")] int Medium,
    [property: JsonPropertyName("low")] int Low);

public sealed record BatchCost(
    [property: JsonPropertyName("documentIntelligence")] decimal DocumentIntelligence,
    [property: JsonPropertyName("classificationTokens")] TokenUsage ClassificationTokens,
    [property: JsonPropertyName("extractionTokens")] TokenUsage ExtractionTokens,
    [property: JsonPropertyName("visionTokens")] TokenUsage VisionTokens,
    [property: JsonPropertyName("estimatedTotalUsd")] decimal EstimatedTotalUsd);

public sealed record TokenUsage(
    [property: JsonPropertyName("inputTokens")] long InputTokens,
    [property: JsonPropertyName("outputTokens")] long OutputTokens);
