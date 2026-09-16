using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class GetTypeExtractionPolicyActivityTests
{
    private static TaxonomyData BuildTaxonomy(params DocumentTypeDefinition[] documentTypes) =>
        new(documentTypes, new MetadataConfig(), new ConfidenceThresholds());

    [Fact]
    public void IsExtractionEnabled_TypePresentWithFlagFalse_ReturnsFalse()
    {
        var taxonomy = BuildTaxonomy(new DocumentTypeDefinition { Label = "Design Drawing", ExtractionEnabled = false });

        Assert.False(GetTypeExtractionPolicyActivity.IsExtractionEnabled(taxonomy, DocumentType.DesignDrawing));
    }

    [Fact]
    public void IsExtractionEnabled_TypePresentWithFlagTrue_ReturnsTrue()
    {
        var taxonomy = BuildTaxonomy(new DocumentTypeDefinition { Label = "Survey", ExtractionEnabled = true });

        Assert.True(GetTypeExtractionPolicyActivity.IsExtractionEnabled(taxonomy, DocumentType.Survey));
    }

    [Fact]
    public void IsExtractionEnabled_TypePresentWithoutExplicitFlag_DefaultsToTrue()
    {
        // Mirrors a YAML entry with no extraction_enabled key -- DocumentTypeDefinition's own default applies.
        var taxonomy = BuildTaxonomy(new DocumentTypeDefinition { Label = "Lease" });

        Assert.True(GetTypeExtractionPolicyActivity.IsExtractionEnabled(taxonomy, DocumentType.Lease));
    }

    [Fact]
    public void IsExtractionEnabled_TypeAbsentFromTaxonomy_DefaultsToTrue()
    {
        var taxonomy = BuildTaxonomy(new DocumentTypeDefinition { Label = "Survey", ExtractionEnabled = false });

        Assert.True(GetTypeExtractionPolicyActivity.IsExtractionEnabled(taxonomy, DocumentType.Other));
    }
}
