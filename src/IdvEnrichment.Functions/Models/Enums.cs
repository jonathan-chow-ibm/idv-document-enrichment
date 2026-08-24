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

/// <summary>Document types recognized by the taxonomy (v4, from client Metadata Request). Keep in sync with taxonomy.yaml.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentType>))]
public enum DocumentType
{
    // Contracts
    [JsonStringEnumMemberName("PSA - Acquisition")] PsaAcquisition,
    [JsonStringEnumMemberName("PSA - Disposition")] PsaDisposition,
    [JsonStringEnumMemberName("Lease")] Lease,
    [JsonStringEnumMemberName("Lease Amendment")] LeaseAmendment,
    [JsonStringEnumMemberName("Vendor Contract")] VendorContract,
    [JsonStringEnumMemberName("Commission Agreement")] CommissionAgreement,
    [JsonStringEnumMemberName("Loan Agreement")] LoanAgreement,
    [JsonStringEnumMemberName("JV Agreement")] JvAgreement,
    [JsonStringEnumMemberName("Development Agreement")] DevelopmentAgreement,
    [JsonStringEnumMemberName("Letter of Intent")] LetterOfIntent,
    [JsonStringEnumMemberName("Term Sheet")] TermSheet,
    // Drawing Files
    [JsonStringEnumMemberName("Survey")] Survey,
    [JsonStringEnumMemberName("Plat")] Plat,
    [JsonStringEnumMemberName("Design Drawing")] DesignDrawing,
    // Reports
    [JsonStringEnumMemberName("Closing Statement")] ClosingStatement,
    [JsonStringEnumMemberName("Environmental Survey")] EnvironmentalSurvey,
    [JsonStringEnumMemberName("Geotechnical Report")] GeotechnicalReport,
    [JsonStringEnumMemberName("Easement Document")] EasementDocument,
    // Budget Files
    [JsonStringEnumMemberName("Proforma")] Proforma,
    [JsonStringEnumMemberName("Budget / Cost Estimate")] BudgetCostEstimate,
    [JsonStringEnumMemberName("Bid Tab")] BidTab,
    [JsonStringEnumMemberName("Draw Request")] DrawRequest,
    [JsonStringEnumMemberName("Operating Budget")] OperatingBudget,
    Other,
}


