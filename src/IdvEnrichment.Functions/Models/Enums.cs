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
    // Transaction & Legal
    [JsonStringEnumMemberName("Listing Agreement")] ListingAgreement,
    [JsonStringEnumMemberName("Letter of Intent")] LetterOfIntent,
    [JsonStringEnumMemberName("Purchase & Sale Agreement")] PurchaseAndSaleAgreement,
    [JsonStringEnumMemberName("Lease / Development / JV Agreement")] LeaseDevelopmentJVAgreement,
    [JsonStringEnumMemberName("Closing Document")] ClosingDocument,
    [JsonStringEnumMemberName("Title & Survey")] TitleAndSurvey,
    // Due Diligence & Entitlements
    [JsonStringEnumMemberName("Environmental Report")] EnvironmentalReport,
    [JsonStringEnumMemberName("Plat / Site Plan")] PlatSitePlan,
    [JsonStringEnumMemberName("Permit / Municipal Approval")] PermitMunicipalApproval,
    [JsonStringEnumMemberName("Utility & Easement Agreement")] UtilityEasementAgreement,
    // Financial
    [JsonStringEnumMemberName("Financial Model / Pro Forma")] FinancialModelProForma,
    [JsonStringEnumMemberName("Operating Budget")] OperatingBudget,
    [JsonStringEnumMemberName("Lender / Financing Document")] LenderFinancingDocument,
    // Marketing & Corporate
    [JsonStringEnumMemberName("Marketing Flyer / Brochure")] MarketingFlyerBrochure,
    [JsonStringEnumMemberName("Proposal / Pitch Deck")] ProposalPitchDeck,
    [JsonStringEnumMemberName("Entity / Corporate Governance")] EntityCorporateGovernance,
    Correspondence,
    // Retained from v2
    [JsonStringEnumMemberName("Offer Memorandum")] OfferMemorandum,
    [JsonStringEnumMemberName("Market Report")] MarketReport,
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
