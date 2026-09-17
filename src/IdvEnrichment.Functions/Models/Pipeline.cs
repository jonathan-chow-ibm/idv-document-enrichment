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
    [property: JsonPropertyName("attemptNumber")] int AttemptNumber = 1,
    // When true, the document is classified (and vision-overridden where applicable) and that
    // classification is written back, but Agent 2 metadata extraction never runs.
    [property: JsonPropertyName("classifyOnly")] bool ClassifyOnly = false,
    // Caps how many pages are read/analyzed, independent of ClassifyOnly. Null, zero, or negative all
    // mean "no explicit limit" — DocumentOrchestrator still defaults a classify-only run to 1 page unless
    // this overrides it with a positive value.
    [property: JsonPropertyName("maxPages")] int? MaxPages = null);

/// <summary>Input to kick off a batch run. Pass a SharePoint URL — can be a library or a folder within it.</summary>
public sealed record BatchRequest(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("maxConcurrency")] int? MaxConcurrency = null,
    [property: JsonPropertyName("chunkSize")] int? ChunkSize = null,
    [property: JsonPropertyName("itemIds")] IReadOnlyList<string>? ItemIds = null,
    [property: JsonPropertyName("classifyOnly")] bool ClassifyOnly = false,
    // Caps how many pages are read locally or sent to Document Intelligence for every document in the
    // batch. Null, zero, or negative all leave the existing default (full document, or 1 page for a
    // classify-only run) — only a positive value overrides it.
    [property: JsonPropertyName("maxPages")] int? MaxPages = null);

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
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("lastModifiedDateTime")] DateTimeOffset LastModifiedDateTime,
    [property: JsonPropertyName("folderPath")] string? FolderPath = null,
    [property: JsonPropertyName("size")] long Size = 0)
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
    private const string TooLargeMethod = "too-large";
    private const string PasswordProtectedMethod = "password-protected";

    // Sentinel for formats that cannot be parsed; routes the document to human review.
    public static ExtractionResult UnsupportedFormat(string fileName) =>
        new(Text: $"[Unsupported format: {Path.GetExtension(fileName)}]",
            PageCount: 0, TextLength: 0, KeyValuePairs: [], Language: "unknown",
            ExtractionMethod: UnsupportedMethod);

    // Sentinel for files too large to safely process locally or economically via DI; routes to human review.
    public static ExtractionResult TooLargeForProcessing(string fileName, long sizeBytes) =>
        new(Text: $"[File too large for automatic processing: {Path.GetExtension(fileName)}, {sizeBytes / (1024 * 1024)} MB]",
            PageCount: 0, TextLength: 0, KeyValuePairs: [], Language: "unknown",
            ExtractionMethod: TooLargeMethod);

    // Sentinel for PDFs that cannot be opened without a password; routes to human review rather than
    // falling back to Document Intelligence, which cannot open an encrypted PDF either.
    public static ExtractionResult PasswordProtected(string fileName) =>
        new(Text: $"[Password-protected file cannot be processed automatically: {Path.GetExtension(fileName)}]",
            PageCount: 0, TextLength: 0, KeyValuePairs: [], Language: "unknown",
            ExtractionMethod: PasswordProtectedMethod);

    public bool IsUnsupported => ExtractionMethod == UnsupportedMethod;
    public bool IsTooLarge => ExtractionMethod == TooLargeMethod;
    public bool IsPasswordProtected => ExtractionMethod == PasswordProtectedMethod;
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
    [property: JsonPropertyName("writeBackSucceeded")] bool WriteBackSucceeded = true,
    [property: JsonPropertyName("classifyOnly")] bool ClassifyOnly = false);

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

public sealed record ExtractContentInput(
    string DocumentUrl,
    string FileName = "",
    // When set to a positive value, only the first MaxPages pages are read/analyzed — used by
    // classify-only runs (where a representative first page or two is enough signal and a full-document
    // pass would be wasted cost) and by callers that explicitly want to bound extraction to a page count.
    // Null, zero, or negative all read/analyze the full document.
    [property: JsonPropertyName("maxPages")] int? MaxPages = null,
    // True when DocumentUrl points at a PDF that Graph converted from Word/PowerPoint — a
    // converted PDF always has a real text layer, so it is routed through the PDF extraction path.
    [property: JsonPropertyName("convertedToPdf")] bool ConvertedToPdf = false,
    // The unconverted file, set only alongside ConvertedToPdf. Conversion is a cost optimisation, not a
    // requirement, so a converted PDF the media service refuses to produce falls back to this.
    [property: JsonPropertyName("originalUrl")] string? OriginalUrl = null);

public sealed record GetDocumentDownloadUrlInput(
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("fileUrl")] string FileUrl,
    [property: JsonPropertyName("fileName")] string FileName = "");

public sealed record DocumentDownloadResult(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("isConvertedToPdf")] bool IsConvertedToPdf,
    // Set only when Url points at a converted PDF. Graph returns that URL before the media service has
    // produced anything, and it refuses outright for files it cannot convert -- so extraction needs the
    // unconverted file to fall back to, since the refusal only surfaces when the URL is fetched.
    [property: JsonPropertyName("originalUrl")] string? OriginalUrl = null);

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
    [property: JsonPropertyName("drawingClassification")] DrawingClassification? DrawingClassification = null,
    // True when metadata is null because the taxonomy disabled extraction for this type — not because
    // of low confidence or an unresolved type. RouteResultActivity routes this to Write, not Review.
    [property: JsonPropertyName("extractionExcludedByType")] bool ExtractionExcludedByType = false);

public sealed record FilterProcessedInput(
    [property: JsonPropertyName("documents")] IReadOnlyList<LibraryDocument> Documents,
    [property: JsonPropertyName("libraryKey")] string LibraryKey);

public sealed record ChunkRequest(
    [property: JsonPropertyName("documents")] IReadOnlyList<LibraryDocument> Documents,
    [property: JsonPropertyName("batchId")] string BatchId,
    [property: JsonPropertyName("target")] ResolvedSharePointTarget Target,
    [property: JsonPropertyName("maxConcurrency")] int MaxConcurrency,
    [property: JsonPropertyName("classifyOnly")] bool ClassifyOnly = false,
    // Caps how many pages are read locally or sent to Document Intelligence for every document in the
    // chunk. Null, zero, or negative all leave the existing default (full document, or 1 page for a
    // classify-only run) — only a positive value overrides it.
    [property: JsonPropertyName("maxPages")] int? MaxPages = null);

public sealed record ExtractDrawingDetailsInput(
    [property: JsonPropertyName("documentUrl")] string DocumentUrl,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("extractedText")] string ExtractedText = "");

public sealed record ValidateSharePointSchemaInput(
    [property: JsonPropertyName("target")] ResolvedSharePointTarget Target,
    // Classify-only runs never extract metadata (DocumentOrchestrator leaves it null), so
    // WriteMetadataActivity never reaches the taxonomy Choice columns for them -- only the
    // unconditionally-written DocumentType column needs to be in sync.
    [property: JsonPropertyName("classifyOnly")] bool ClassifyOnly = false);

public sealed record WriteMetadataInput(
    [property: JsonPropertyName("siteId")] string SiteId,
    [property: JsonPropertyName("driveId")] string DriveId,
    [property: JsonPropertyName("itemId")] string ItemId,
    [property: JsonPropertyName("result")] EnrichmentResult Result);

public sealed record RecordProcessingResultInput(
    [property: JsonPropertyName("libraryKey")] string LibraryKey,
    [property: JsonPropertyName("documentId")] string DocumentId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("batchId")] string? BatchId = null);

