using System.Text.Encodings.Web;
using System.Text.Json;

namespace IdvEnrichment.Functions.Models;

public sealed class TaxonomyConfig
{
    public List<DocumentTypeDefinition> DocumentTypes { get; set; } = [];
    public MetadataConfig Metadata { get; set; } = new();
    public ConfidenceThresholds ConfidenceThresholds { get; set; } = new();
}

public sealed class DocumentTypeDefinition
{
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DecisionRules { get; set; } = [];
    public List<FieldDefinition> SpecificFields { get; set; } = [];
}

public sealed class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AllowedValues { get; set; } = [];
}

/// <summary>The v4 metadata block, grouped by how each field is populated.</summary>
public sealed class MetadataConfig
{
    public List<FieldSpec> FolderDerived { get; set; } = [];
    public List<FieldSpec> System { get; set; } = [];
    public ContentMetadata Content { get; set; } = new();
}

/// <summary>AI-extracted content fields, grouped for readability. Flattened via <see cref="All"/>.</summary>
public sealed class ContentMetadata
{
    public List<FieldSpec> Universal { get; set; } = [];
    public List<FieldSpec> Property { get; set; } = [];
    public List<FieldSpec> Transaction { get; set; } = [];
    public List<FieldSpec> Ownership { get; set; } = [];

    public IReadOnlyList<FieldSpec> All() =>
        [.. Universal, .. Property, .. Transaction, .. Ownership];
}

public sealed class FieldSpec
{
    public string FieldName { get; set; } = string.Empty;
    public string SharepointColumn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsFreetext { get; set; }
    public List<string> AllowedValues { get; set; } = [];
    public List<string> DecisionRules { get; set; } = [];
    // Drives type-aware SharePoint write-back in WriteMetadataActivity; "text" (default), "dateTime", or "number".
    public string ValueType { get; set; } = "text";
}

public sealed class ConfidenceThresholds
{
    public double Agent1Classification { get; set; } = 0.80;
    // Per-field thresholds for Agent 2 content extraction, keyed by field_name; "default" applies to the rest.
    public Dictionary<string, double> Agent2Content { get; set; } = new();
}

public sealed record TaxonomyData(
    IReadOnlyList<DocumentTypeDefinition> DocumentTypes,
    MetadataConfig Metadata,
    ConfidenceThresholds Thresholds)
{
    // [JsonStringEnumMemberName] on the enum drives the label used here.
    // UnsafeRelaxedJsonEscaping prevents & → &, ensuring labels match taxonomy YAML.
    private static readonly JsonSerializerOptions _labelSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public DocumentTypeDefinition? GetDocumentType(DocumentType documentType)
    {
        var label = JsonSerializer.Serialize(documentType, _labelSerializerOptions).Trim('"');
        return DocumentTypes.FirstOrDefault(d => d.Label == label);
    }

    /// <summary>All AI-extracted content fields, flattened across groups.</summary>
    public IReadOnlyList<FieldSpec> ContentFields() => Metadata.Content.All();

    /// <summary>Field names expected on every document — used to gate routing (empty non-universal fields don't force review).</summary>
    public IReadOnlySet<string> UniversalFieldNames() =>
        Metadata.Content.Universal.Select(f => f.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string? GetGroupForDocumentType(DocumentType documentType)
    {
        var def = GetDocumentType(documentType);
        return def?.Group;
    }
}
