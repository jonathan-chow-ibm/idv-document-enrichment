using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class WriteMetadataActivityTests
{
    private static TaxonomyData BuildTaxonomy() => new(
        DocumentTypes: [],
        Metadata: new MetadataConfig
        {
            Content = new ContentMetadata
            {
                Universal =
                [
                    new FieldSpec { FieldName = "documentStatus", SharepointColumn = "DocumentStatus" },
                    new FieldSpec { FieldName = "counterparty", SharepointColumn = "Counterparty" },
                ],
                Property =
                [
                    new FieldSpec { FieldName = "propertyAddress", SharepointColumn = "PropertyAddress" },
                    // Intentional case mismatch — SharePoint column is "ParcelID", not the naive "ParcelId".
                    new FieldSpec { FieldName = "parcelId", SharepointColumn = "ParcelID" },
                    new FieldSpec { FieldName = "county", SharepointColumn = "County" },
                    new FieldSpec { FieldName = "acres", SharepointColumn = "Acres", ValueType = "number" },
                    new FieldSpec
                    {
                        FieldName = "opportunityZone",
                        SharepointColumn = "OpportunityZone",
                        AllowedValues = ["Yes", "No", "Unknown"],
                    },
                ],
                Transaction =
                [
                    new FieldSpec { FieldName = "purchasePrice", SharepointColumn = "PurchasePrice" },
                    new FieldSpec { FieldName = "executionDate", SharepointColumn = "ExecutionDate", ValueType = "dateTime" },
                ],
            },
        },
        Thresholds: new ConfidenceThresholds());


    private static EnrichmentResult BuildResult(IReadOnlyDictionary<string, CategoryClassification>? fields, bool classifyOnly = false)
    {
        var metadata = fields is null
            ? null
            : new MetadataExtractionResult(fields, SuggestedFields: []);
        return new EnrichmentResult(
            DocumentId: "doc-1",
            FileName: "sample.pdf",
            Extraction: new ExtractionResult("text", 1, 4, []),
            TypeClassification: new TypeClassificationResult(DocumentType.LetterOfIntent, 0.9, "test"),
            Metadata: metadata,
            ProcessingMetrics: new ProcessingMetrics(),
            RoutingDecision: RoutingDecision.Write,
            LowConfidenceCategories: [],
            ClassifyOnly: classifyOnly);
    }

    [Fact]
    public void BuildFieldsPayload_PopulatesEveryTaxonomyContentColumn()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["documentStatus"] = new("Final", 0.95, "r"),
            ["counterparty"] = new("Acme LLC", 0.9, "r"),
            ["propertyAddress"] = new("123 Main St", 0.9, "r"),
            ["parcelId"] = new("R-12345", 0.9, "r"),
            ["county"] = new("Harris", 0.9, "r"),
            ["purchasePrice"] = new("1000000", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.NotNull(payload.AdditionalData);
        Assert.Equal("Final", payload.AdditionalData["DocumentStatus"]);
        Assert.Equal("Acme LLC", payload.AdditionalData["Counterparty"]);
        Assert.Equal("123 Main St", payload.AdditionalData["PropertyAddress"]);
        Assert.Equal("Harris", payload.AdditionalData["County"]);
        Assert.Equal("1000000", payload.AdditionalData["PurchasePrice"]);
    }

    [Fact]
    public void BuildFieldsPayload_UsesTaxonomyDeclaredColumnName_NotPascalCasedFieldName()
    {
        // Regression: parcelId must land in "ParcelID" (as declared in taxonomy.yaml), not "ParcelId".
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["parcelId"] = new("R-99999", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal("R-99999", payload.AdditionalData!["ParcelID"]);
        Assert.False(payload.AdditionalData.ContainsKey("ParcelId"));
    }

    [Fact]
    public void BuildFieldsPayload_MissingExtractedFields_YieldEmptyStringColumns()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal(string.Empty, payload.AdditionalData!["DocumentStatus"]);
        Assert.Equal(string.Empty, payload.AdditionalData["PropertyAddress"]);
        Assert.Equal(string.Empty, payload.AdditionalData["ParcelID"]);
        Assert.Equal(string.Empty, payload.AdditionalData["PurchasePrice"]);
    }

    [Fact]
    public void BuildFieldsPayload_IncludesSystemColumns()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal("Letter of Intent", payload.AdditionalData!["DocumentType"]);
        Assert.Equal(0.9, payload.AdditionalData["AIConfidence"]);
        Assert.Equal("Classified", payload.AdditionalData["AIProcessingStatus"]);
    }

    [Fact]
    public void BuildFieldsPayload_ClassifyOnly_OmitsAIProcessingStatus()
    {
        // AIProcessingStatus doubles as Power Automate's re-trigger guard -- setting it on a
        // classify-only test run would silently block the document from ever being reprocessed
        // for real. DocumentType/AIConfidence still get written; only the guard column is skipped.
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null, classifyOnly: true), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal("Letter of Intent", payload.AdditionalData!["DocumentType"]);
        Assert.False(payload.AdditionalData.ContainsKey("AIProcessingStatus"));
    }

    [Fact]
    public void BuildFieldsPayload_DateTimeField_ParseableValue_ConvertsToIso8601()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["executionDate"] = new("March 15, 2026", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        var expected = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero).ToString("o");
        Assert.Equal(expected, payload.AdditionalData!["ExecutionDate"]);
    }

    [Fact]
    public void BuildFieldsPayload_DateTimeField_UnparseableValue_IsOmitted()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["executionDate"] = new("not a date", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("ExecutionDate"));
    }

    [Fact]
    public void BuildFieldsPayload_DateTimeField_MissingValue_IsOmitted()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("ExecutionDate"));
    }

    [Theory]
    [InlineData("5.2 acres", 5.2)]
    [InlineData("$1,200,000", 1200000d)]
    public void BuildFieldsPayload_NumberField_ParseableValue_ParsesNoise(string rawValue, double expected)
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["acres"] = new(rawValue, 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal(expected, payload.AdditionalData!["Acres"]);
    }

    [Fact]
    public void BuildFieldsPayload_NumberField_UnparseableValue_IsOmitted()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["acres"] = new("several acres", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("Acres"));
    }

    [Fact]
    public void BuildFieldsPayload_NumberField_MissingValue_IsOmitted()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("Acres"));
    }

    [Fact]
    public void BuildFieldsPayload_ChoiceField_ExactMatch_PassesThrough()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["opportunityZone"] = new("Yes", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal("Yes", payload.AdditionalData!["OpportunityZone"]);
    }

    [Fact]
    public void BuildFieldsPayload_ChoiceField_CaseDifferentMatch_CorrectsToCanonicalCasing()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["opportunityZone"] = new("yes", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal("Yes", payload.AdditionalData!["OpportunityZone"]);
    }

    [Fact]
    public void BuildFieldsPayload_ChoiceField_NonMatchingValue_IsOmitted()
    {
        var fields = new Dictionary<string, CategoryClassification>
        {
            ["opportunityZone"] = new("Maybe", 0.9, "r"),
        };

        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("OpportunityZone"));
    }

    [Fact]
    public void BuildFieldsPayload_ChoiceField_MissingValue_IsOmitted()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.False(payload.AdditionalData!.ContainsKey("OpportunityZone"));
    }

    [Fact]
    public void BuildFieldsPayload_TextField_EmptyOrMissingValue_StillWritesEmptyString()
    {
        var payload = WriteMetadataActivity.BuildFieldsPayload(
            BuildResult(fields: null), BuildTaxonomy(), DateTimeOffset.UnixEpoch);

        Assert.Equal(string.Empty, payload.AdditionalData!["PropertyAddress"]);
        Assert.Equal(string.Empty, payload.AdditionalData["County"]);
    }
}
