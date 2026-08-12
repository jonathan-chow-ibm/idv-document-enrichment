using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ProcessingSource>))]
public enum ProcessingSource
{
    Trigger,
    Batch,
}

[JsonConverter(typeof(JsonStringEnumConverter<RoutingDecision>))]
public enum RoutingDecision
{
    Write,
    Review,
}

/// <summary>Document types recognized by the taxonomy. Keep in sync with taxonomy.yaml.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentType>))]
public enum DocumentType
{
    [JsonStringEnumMemberName("Lease Agreement")] LeaseAgreement,
    [JsonStringEnumMemberName("Offer Memorandum")] OfferMemorandum,
    [JsonStringEnumMemberName("Market Report")] MarketReport,
    [JsonStringEnumMemberName("Purchase Agreement")] PurchaseAgreement,
    [JsonStringEnumMemberName("Letter of Intent")] LetterOfIntent,
    [JsonStringEnumMemberName("Financial Analysis")] FinancialAnalysis,
    [JsonStringEnumMemberName("Due Diligence")] DueDiligence,
    Correspondence,
    Presentation,
    Other,
}

/// <summary>Message placed on the processing queue for each document.</summary>
public sealed record QueueMessage(
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("siteId")] string SiteId,
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("fileUrl")] string FileUrl,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("modifiedDateTime")] DateTimeOffset ModifiedDateTime,
    [property: JsonPropertyName("source")] ProcessingSource Source,
    [property: JsonPropertyName("batchId")] string? BatchId = null,
    [property: JsonPropertyName("attemptNumber")] int AttemptNumber = 1);

/// <summary>Input to kick off a batch run. Pass a SharePoint URL — can be a library or a folder within it.</summary>
public sealed record BatchRequest(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("maxConcurrency")] int? MaxConcurrency = null);

/// <summary>Resolved SharePoint target returned by the resolve activity.</summary>
public sealed record ResolvedSharePointTarget(
    [property: JsonPropertyName("siteId")] string SiteId,
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("folderPath")] string? FolderPath,
    [property: JsonPropertyName("siteName")] string SiteName,
    [property: JsonPropertyName("libraryName")] string LibraryName);

/// <summary>Document metadata returned by enumerate_library activity.</summary>
public sealed record LibraryDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset LastModifiedDateTime);

/// <summary>A key-value pair extracted from a document by Document Intelligence.</summary>
public sealed record DocumentField(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("confidence")] double Confidence);

/// <summary>Output from Azure AI Document Intelligence extraction.</summary>
public sealed record ExtractionResult(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("pageCount")] int PageCount,
    [property: JsonPropertyName("textLength")] int TextLength,
    [property: JsonPropertyName("keyValuePairs")] IReadOnlyList<DocumentField> KeyValuePairs,
    [property: JsonPropertyName("language")] string Language = "unknown");

// --- Activity Inputs/Outputs ---

public sealed record ExtractContentInput(string DocumentUrl);

public sealed record ClassifyTypeInput(
    string DocumentId,
    string FileName,
    string ExtractedText,
    IReadOnlyList<DocumentField> KeyValuePairs);

public sealed record ExtractMetadataInput(
    string DocumentId,
    string FileName,
    DocumentType DocumentType,
    string ExtractedText,
    IReadOnlyList<DocumentField> KeyValuePairs);

public sealed record RouteResultInput(
    QueueMessage Message,
    TypeClassificationResult TypeClassification,
    MetadataExtractionResult? Metadata);

// --- Agent 1: Document Type Classification ---

public sealed record TypeClassificationResult(
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning);

// --- Agent 2: Type-Specific Metadata Extraction ---

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

// --- Combined Pipeline Result ---

public sealed record ProcessingMetrics(
    [property: JsonPropertyName("extractionDurationMs")] int ExtractionDurationMs = 0,
    [property: JsonPropertyName("classificationDurationMs")] int ClassificationDurationMs = 0,
    [property: JsonPropertyName("metadataExtractionDurationMs")] int MetadataExtractionDurationMs = 0,
    [property: JsonPropertyName("totalDurationMs")] int TotalDurationMs = 0,
    [property: JsonPropertyName("classificationInputTokens")] int ClassificationInputTokens = 0,
    [property: JsonPropertyName("classificationOutputTokens")] int ClassificationOutputTokens = 0,
    [property: JsonPropertyName("extractionInputTokens")] int ExtractionInputTokens = 0,
    [property: JsonPropertyName("extractionOutputTokens")] int ExtractionOutputTokens = 0);

/// <summary>Combined output from the full two-agent pipeline.</summary>
public sealed record EnrichmentResult(
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("extraction")] ExtractionResult Extraction,
    [property: JsonPropertyName("typeClassification")] TypeClassificationResult TypeClassification,
    [property: JsonPropertyName("metadata")] MetadataExtractionResult? Metadata,
    [property: JsonPropertyName("processingMetrics")] ProcessingMetrics ProcessingMetrics,
    [property: JsonPropertyName("routingDecision")] RoutingDecision RoutingDecision,
    [property: JsonPropertyName("lowConfidenceCategories")] IReadOnlyList<string> LowConfidenceCategories);

// --- Error Tracking ---

public enum ProcessingStep
{
    Fetch,
    Extract,
    ClassifyType,
    ExtractMetadata,
    Route,
    WriteMetadata,
}

/// <summary>Stored in Azure Table Storage for failed/errored documents.</summary>
public sealed record ProcessingError(
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("step")] ProcessingStep Step,
    [property: JsonPropertyName("errorMessage")] string ErrorMessage,
    [property: JsonPropertyName("retryCount")] int RetryCount,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("rawResponse")] string? RawResponse = null,
    [property: JsonPropertyName("resolved")] bool Resolved = false);

// --- Batch Report ---

public sealed record BatchReport(
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("summary")] BatchSummary Summary,
    [property: JsonPropertyName("confidenceDistribution")] ConfidenceDistribution ConfidenceDistribution,
    [property: JsonPropertyName("errorsByStep")] IReadOnlyDictionary<ProcessingStep, int> ErrorsByStep,
    [property: JsonPropertyName("documentTypeCounts")] IReadOnlyDictionary<string, int> DocumentTypeCounts,
    [property: JsonPropertyName("cost")] BatchCost Cost);

public sealed record BatchSummary(
    [property: JsonPropertyName("totalDocuments")] int TotalDocuments,
    [property: JsonPropertyName("classified")] int Classified,
    [property: JsonPropertyName("underReview")] int UnderReview,
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
    [property: JsonPropertyName("estimatedTotalUsd")] decimal EstimatedTotalUsd);

public sealed record TokenUsage(
    [property: JsonPropertyName("inputTokens")] long InputTokens,
    [property: JsonPropertyName("outputTokens")] long OutputTokens);
