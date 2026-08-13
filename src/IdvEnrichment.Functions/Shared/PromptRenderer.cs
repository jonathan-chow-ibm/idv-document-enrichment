using System.Reflection;
using System.Text.Json;
using HandlebarsDotNet;
using IdvEnrichment.Functions.Models;

namespace IdvEnrichment.Functions.Shared;

public static class PromptRenderer
{
    private static readonly HandlebarsTemplate<object, object> _classifyType;
    private static readonly HandlebarsTemplate<object, object> _userDocument;
    private static readonly HandlebarsTemplate<object, object> _extractMetadata;

    static PromptRenderer()
    {
        _classifyType = Handlebars.Compile(LoadTemplate("ClassifyType"));
        _userDocument = Handlebars.Compile(LoadTemplate("UserDocument"));
        _extractMetadata = Handlebars.Compile(LoadTemplate("ExtractMetadata"));
    }

    public static string RenderClassifyType(IReadOnlyList<DocumentTypeDefinition> types)
        => _classifyType(new { documentTypes = types });

    public static string RenderUserDocument(
        string fileName,
        string extractedText,
        IReadOnlyList<DocumentField> kvPairs)
        => _userDocument(new
        {
            fileName,
            extractedText,
            keyValuePairs = kvPairs,
            textLength = extractedText.Length,
        });

    public static string RenderExtractMetadata(DocumentType documentType, TaxonomyData taxonomy)
    {
        var docTypeLabel = System.Text.Json.JsonSerializer.Serialize(documentType).Trim('"');
        var docTypeDef = taxonomy.GetDocumentType(documentType);
        return _extractMetadata(new
        {
            documentType = docTypeLabel,
            commonCategories = taxonomy.CommonCategories,
            specificFields = docTypeDef?.SpecificFields ?? [],
        });
    }

    private static string LoadTemplate(string name)
    {
        var resourceName = $"IdvEnrichment.Functions.Prompts.{name}.hbs";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
