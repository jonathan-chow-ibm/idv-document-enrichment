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

public enum ProcessingStep
{
    Fetch,
    Extract,
    ClassifyType,
    ExtractMetadata,
    Route,
    WriteMetadata,
}
