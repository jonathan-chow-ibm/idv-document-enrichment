namespace IdvEnrichment.Functions.Models;

public sealed class TaxonomyConfig
{
    public List<DocumentTypeDefinition> DocumentTypes { get; set; } = [];
    public List<CategoryDefinition> CommonCategories { get; set; } = [];
    public ConfidenceThresholds ConfidenceThresholds { get; set; } = new();
}

public sealed class DocumentTypeDefinition
{
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FieldDefinition> SpecificFields { get; set; } = [];
}

public sealed class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CategoryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string SharepointColumn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsFreetext { get; set; }
    public List<AllowedValue> AllowedValues { get; set; } = [];
    public List<string> DecisionRules { get; set; } = [];
}

public sealed class AllowedValue
{
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ConfidenceThresholds
{
    public double Agent1Classification { get; set; } = 0.80;
    public Dictionary<string, double> Agent2Common { get; set; } = new();
    public double Agent2SpecificDefault { get; set; } = 0.80;
}

public sealed record TaxonomyData(
    IReadOnlyList<DocumentTypeDefinition> DocumentTypes,
    IReadOnlyList<CategoryDefinition> CommonCategories,
    ConfidenceThresholds Thresholds)
{
    // [JsonStringEnumMemberName] on the enum drives the label used here
    public DocumentTypeDefinition? GetDocumentType(DocumentType documentType)
    {
        var label = System.Text.Json.JsonSerializer.Serialize(documentType).Trim('"');
        return DocumentTypes.FirstOrDefault(d => d.Label == label);
    }
}
