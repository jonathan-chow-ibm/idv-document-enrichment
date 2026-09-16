using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;

namespace IdvEnrichment.Functions.Activities;

public sealed class GetTypeExtractionPolicyActivity(TaxonomyLoader taxonomyLoader)
{
    [Function(nameof(GetTypeExtractionPolicy))]
    public async Task<bool> GetTypeExtractionPolicy(
        [ActivityTrigger] DocumentType documentType,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        return IsExtractionEnabled(taxonomy, documentType);
    }

    // Types absent from the taxonomy, or without the flag set, default to extraction enabled.
    internal static bool IsExtractionEnabled(TaxonomyData taxonomy, DocumentType documentType) =>
        taxonomy.GetDocumentType(documentType)?.ExtractionEnabled ?? true;
}
