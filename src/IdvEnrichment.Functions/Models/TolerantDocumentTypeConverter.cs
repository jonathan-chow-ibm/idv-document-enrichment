using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdvEnrichment.Functions.Models;

/// <summary>
/// Deserializes <see cref="DocumentType"/> the same way <c>JsonStringEnumConverter</c> does, except that an
/// unrecognized value becomes <see cref="DocumentType.Other"/> instead of throwing.
/// </summary>
/// <remarks>
/// Agent 1 is called with plain JSON mode (no response schema), so nothing constrains the model to the
/// taxonomy's labels — and taxonomy.yaml explicitly folds real document kinds ("Marketing Flyer",
/// "Proposal/Pitch Deck", "Correspondence", "Permit") into Other, so the model is being asked to reason
/// about categories that have no enum member. Binding straight to the enum threw a JsonException and
/// failed the whole classification, discarding a confident primary answer over an unusable value.
///
/// Other is the right landing place: it is a real taxonomy label so write-back accepts it, and it already
/// forces skipExtraction and routes the document to Review, so a human sees it rather than it being
/// silently mis-filed.
///
/// Wire names come from the enum's JsonStringEnumMemberName attributes rather than being retyped here, so
/// serialization stays byte-identical to the previous converter — WriteMetadataActivity serializes this
/// value straight into a SharePoint Choice column, which rejects the entire PATCH on a mismatch.
/// </remarks>
public sealed class TolerantDocumentTypeConverter : JsonConverter<DocumentType>
{
    private static readonly Dictionary<string, DocumentType> ByWireName =
        BuildWireNames().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<DocumentType, string> ToWireName =
        BuildWireNames().ToDictionary(p => p.Value, p => p.Name);

    /// <summary>Maps a wire value to its <see cref="DocumentType"/>, without the fallback to Other. Callers
    /// use this to tell "the model said Other" apart from "the model said something we don't recognize".</summary>
    public static bool TryParseWireName(string? wireName, out DocumentType documentType)
    {
        documentType = DocumentType.Other;
        return wireName is not null && ByWireName.TryGetValue(wireName, out documentType);
    }

    public override DocumentType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return TryParseWireName(reader.GetString(), out var parsed) ? parsed : DocumentType.Other;
        }

        // The previous converter accepted integer values too; keep that rather than change behaviour here.
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric)
            && Enum.IsDefined(typeof(DocumentType), numeric))
        {
            return (DocumentType)numeric;
        }

        return DocumentType.Other;
    }

    public override void Write(Utf8JsonWriter writer, DocumentType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToWireName.TryGetValue(value, out var name) ? name : nameof(DocumentType.Other));
    }

    private static IEnumerable<(string Name, DocumentType Value)> BuildWireNames() =>
        typeof(DocumentType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (
                Name: field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name,
                Value: (DocumentType)field.GetValue(null)!));
}
