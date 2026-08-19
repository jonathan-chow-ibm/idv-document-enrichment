using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

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
