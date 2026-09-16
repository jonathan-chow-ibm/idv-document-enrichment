using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class RouteResultActivityTests
{
    private static TaxonomyData BuildTaxonomy() =>
        new(
            DocumentTypes: [],
            Metadata: new MetadataConfig
            {
                Content = new ContentMetadata
                {
                    Universal = [new FieldSpec { FieldName = "documentStatus" }],
                },
            },
            Thresholds: new ConfidenceThresholds
            {
                Agent2Content = new Dictionary<string, double> { ["default"] = 0.75 },
            });

    [Fact]
    public void DetermineRouting_NullMetadata_ExcludedByType_RoutesToWrite()
    {
        var (decision, lowConfidence) = RouteResultActivity.DetermineRouting(
            metadata: null, BuildTaxonomy(), defaultConfidenceThreshold: 0.75, extractionExcludedByType: true);

        Assert.Equal(RoutingDecision.Write, decision);
        Assert.Empty(lowConfidence);
    }

    [Fact]
    public void DetermineRouting_NullMetadata_NotExcludedByType_RoutesToReview()
    {
        // Preserves prior behavior: a null Metadata not explained by the taxonomy exclusion is a failure
        // or unresolved-type skip, and must still land in Review.
        var (decision, lowConfidence) = RouteResultActivity.DetermineRouting(
            metadata: null, BuildTaxonomy(), defaultConfidenceThreshold: 0.75);

        Assert.Equal(RoutingDecision.Review, decision);
        Assert.Empty(lowConfidence);
    }

    [Fact]
    public void DetermineRouting_HighConfidenceMetadata_RoutesToWrite()
    {
        var metadata = new MetadataExtractionResult(
            Fields: new Dictionary<string, CategoryClassification>
            {
                ["documentStatus"] = new("Executed", 0.9, "signed"),
            },
            SuggestedFields: []);

        var (decision, lowConfidence) = RouteResultActivity.DetermineRouting(
            metadata, BuildTaxonomy(), defaultConfidenceThreshold: 0.75);

        Assert.Equal(RoutingDecision.Write, decision);
        Assert.Empty(lowConfidence);
    }

    [Fact]
    public void DetermineRouting_LowConfidenceUniversalField_RoutesToReview()
    {
        var metadata = new MetadataExtractionResult(
            Fields: new Dictionary<string, CategoryClassification>
            {
                ["documentStatus"] = new("Draft", 0.5, "unclear"),
            },
            SuggestedFields: []);

        var (decision, lowConfidence) = RouteResultActivity.DetermineRouting(
            metadata, BuildTaxonomy(), defaultConfidenceThreshold: 0.75);

        Assert.Equal(RoutingDecision.Review, decision);
        Assert.Equal(["documentStatus"], lowConfidence);
    }

    [Fact]
    public void DetermineRouting_EmptyNonUniversalField_IsNotApplicable_DoesNotForceReview()
    {
        var metadata = new MetadataExtractionResult(
            Fields: new Dictionary<string, CategoryClassification>
            {
                ["documentStatus"] = new("Executed", 0.9, "signed"),
                ["parcelId"] = new("", 0.0, "not present on this document"),
            },
            SuggestedFields: []);

        var (decision, lowConfidence) = RouteResultActivity.DetermineRouting(
            metadata, BuildTaxonomy(), defaultConfidenceThreshold: 0.75);

        Assert.Equal(RoutingDecision.Write, decision);
        Assert.Empty(lowConfidence);
    }
}
