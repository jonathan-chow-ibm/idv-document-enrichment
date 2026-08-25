using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

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
    [property: JsonPropertyName("maxConcurrency")] int? MaxConcurrency = null,
    [property: JsonPropertyName("chunkSize")] int? ChunkSize = null);

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
    [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset LastModifiedDateTime,
    [property: JsonPropertyName("folderPath")] string? FolderPath = null)
{
    // Relative path shown to the AI agents: includes folder context for better classification
    public string RelativePath => FolderPath is null ? Name : $"{FolderPath}/{Name}";
}

/// <summary>A key-value pair extracted from a document by Document Intelligence.</summary>
public sealed record DocumentField(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("confidence")] double Confidence);

/// <summary>Output from content extraction; downstream contract is Markdown regardless of extraction path.</summary>
public sealed record ExtractionResult(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("pageCount")] int PageCount,
    [property: JsonPropertyName("textLength")] int TextLength,
    [property: JsonPropertyName("keyValuePairs")] IReadOnlyList<DocumentField> KeyValuePairs,
    [property: JsonPropertyName("language")] string Language = "unknown",
    [property: JsonPropertyName("extractionMethod")] string ExtractionMethod = "document-intelligence",
    [property: JsonPropertyName("durationMs")] int DurationMs = 0)
{
    private const string UnsupportedMethod = "unsupported";

    // Sentinel for formats that cannot be parsed; routes the document to human review.
    public static ExtractionResult UnsupportedFormat(string fileName) =>
        new(Text: $"[Unsupported format: {Path.GetExtension(fileName)}]",
            PageCount: 0, TextLength: 0, KeyValuePairs: [], Language: "unknown",
            ExtractionMethod: UnsupportedMethod);

    public bool IsUnsupported => ExtractionMethod == UnsupportedMethod;
}

/// <summary>Combined output from the full two-agent pipeline.</summary>
public sealed record EnrichmentResult(
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("extraction")] ExtractionResult Extraction,
    [property: JsonPropertyName("typeClassification")] TypeClassificationResult TypeClassification,
    [property: JsonPropertyName("metadata")] MetadataExtractionResult? Metadata,
    [property: JsonPropertyName("processingMetrics")] ProcessingMetrics ProcessingMetrics,
    [property: JsonPropertyName("routingDecision")] RoutingDecision RoutingDecision,
    [property: JsonPropertyName("lowConfidenceCategories")] IReadOnlyList<string> LowConfidenceCategories,
    [property: JsonPropertyName("drawingClassification")] DrawingClassification? DrawingClassification = null,
    [property: JsonPropertyName("writeBackSucceeded")] bool WriteBackSucceeded = true);

public sealed record ProcessingMetrics(
    [property: JsonPropertyName("extractionDurationMs")] int ExtractionDurationMs = 0,
    [property: JsonPropertyName("classificationDurationMs")] int ClassificationDurationMs = 0,
    [property: JsonPropertyName("metadataExtractionDurationMs")] int MetadataExtractionDurationMs = 0,
    [property: JsonPropertyName("totalDurationMs")] int TotalDurationMs = 0,
    [property: JsonPropertyName("classificationInputTokens")] int ClassificationInputTokens = 0,
    [property: JsonPropertyName("classificationOutputTokens")] int ClassificationOutputTokens = 0,
    [property: JsonPropertyName("extractionInputTokens")] int ExtractionInputTokens = 0,
    [property: JsonPropertyName("extractionOutputTokens")] int ExtractionOutputTokens = 0,
    [property: JsonPropertyName("visionInputTokens")] int VisionInputTokens = 0,
    [property: JsonPropertyName("visionOutputTokens")] int VisionOutputTokens = 0);

// --- Activity Inputs ---

public sealed record ExtractContentInput(string DocumentUrl, string FileName = "");

public sealed record GetDocumentDownloadUrlInput(
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("fileUrl")] string FileUrl);

public sealed record ClassifyTypeInput(
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("extractedText")] string ExtractedText,
    [property: JsonPropertyName("keyValuePairs")] IReadOnlyList<DocumentField> KeyValuePairs);

public sealed record ExtractMetadataInput(
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("documentType")] DocumentType DocumentType,
    [property: JsonPropertyName("extractedText")] string ExtractedText,
    [property: JsonPropertyName("keyValuePairs")] IReadOnlyList<DocumentField> KeyValuePairs);

public sealed record RouteResultInput(
    [property: JsonPropertyName("message")] QueueMessage Message,
    [property: JsonPropertyName("typeClassification")] TypeClassificationResult TypeClassification,
    [property: JsonPropertyName("metadata")] MetadataExtractionResult? Metadata,
    [property: JsonPropertyName("extraction")] ExtractionResult Extraction,
    [property: JsonPropertyName("drawingClassification")] DrawingClassification? DrawingClassification = null);

public sealed record FilterProcessedInput(
    [property: JsonPropertyName("documents")] IReadOnlyList<LibraryDocument> Documents,
    [property: JsonPropertyName("batchId")] string BatchId);

public sealed record ChunkRequest(
    [property: JsonPropertyName("documents")] IReadOnlyList<LibraryDocument> Documents,
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("target")] ResolvedSharePointTarget Target,
    [property: JsonPropertyName("maxConcurrency")] int MaxConcurrency);

public sealed record ExtractDrawingDetailsInput(
    [property: JsonPropertyName("documentUrl")] string DocumentUrl,
    [property: JsonPropertyName("fileName")] string FileName);

public sealed record WriteMetadataInput(
    [property: JsonPropertyName("siteId")] string SiteId,
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("result")] EnrichmentResult Result);

public sealed record RecordProcessingResultInput(
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("status")] string Status);

