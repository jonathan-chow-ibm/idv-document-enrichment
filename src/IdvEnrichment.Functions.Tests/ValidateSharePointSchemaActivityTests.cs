using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Microsoft.Graph.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ValidateSharePointSchemaActivityTests
{
    private static readonly IReadOnlyList<(string ColumnName, IReadOnlyList<string> ExpectedValues)> Expected =
    [
        ("DocumentStatus", ["Draft", "Executed", "Final", "Superseded"]),
    ];

    private static Dictionary<string, ColumnDefinition> BuildColumns(string name, params string[] choices) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [name] = new ColumnDefinition { Name = name, Choice = new ChoiceColumn { Choices = [.. choices] } },
        };

    [Fact]
    public void FindSchemaProblems_AllExpectedValuesPresent_ReturnsEmpty()
    {
        var columns = BuildColumns("DocumentStatus", "Draft", "Executed", "Final", "Superseded");

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Empty(problems);
    }

    [Fact]
    public void FindSchemaProblems_ExtraValuesPresent_IsNotAProblem()
    {
        // A library with MORE Choice values than the app knows about is fine -- only missing
        // values (values the app might write that the column would reject) matter.
        var columns = BuildColumns("DocumentStatus", "Draft", "Executed", "Final", "Superseded", "Archived");

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Empty(problems);
    }

    [Fact]
    public void FindSchemaProblems_CaseDifferentValue_IsNotAProblem()
    {
        // WriteMetadataActivity's TryMatchAllowedValue is itself case-insensitive, so the column
        // only needs a case-insensitive match, not an exact one.
        var columns = BuildColumns("DocumentStatus", "draft", "executed", "final", "superseded");

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Empty(problems);
    }

    [Fact]
    public void FindSchemaProblems_MissingColumn_ReportsColumnMissing()
    {
        var columns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Equal(["column 'DocumentStatus' is missing"], problems);
    }

    [Fact]
    public void FindSchemaProblems_ColumnMissingAValue_ReportsTheMissingValue()
    {
        var columns = BuildColumns("DocumentStatus", "Draft", "Executed", "Final");

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Equal(["column 'DocumentStatus' is missing value(s): Superseded"], problems);
    }

    [Fact]
    public void FindSchemaProblems_ColumnIsNotAChoiceColumn_TreatsAsNoAllowedValues()
    {
        // A text/number column with the same name as an expected Choice column (misconfiguration)
        // has a null Choice facet -- every expected value is reported missing rather than throwing.
        var columns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentStatus"] = new ColumnDefinition { Name = "DocumentStatus", Text = new TextColumn() },
        };

        var problems = ValidateSharePointSchemaActivity.FindSchemaProblems(columns, Expected);

        Assert.Equal(
            ["column 'DocumentStatus' is missing value(s): Draft, Executed, Final, Superseded"], problems);
    }

    [Fact]
    public void ExpectedChoiceColumns_IncludesDocumentTypeUsingWireLabels_NotEnumIdentifiers()
    {
        var taxonomy = new TaxonomyData(DocumentTypes: [], Metadata: new MetadataConfig(), Thresholds: new ConfidenceThresholds());

        var expected = ValidateSharePointSchemaActivity.ExpectedChoiceColumns(taxonomy);

        var documentType = expected.Single(e => e.ColumnName == "DocumentType");
        Assert.Contains("PSA - Acquisition", documentType.ExpectedValues);
        Assert.DoesNotContain("PsaAcquisition", documentType.ExpectedValues);
    }

    [Fact]
    public void ExpectedChoiceColumns_OnlyIncludesContentFieldsWithAllowedValues()
    {
        var taxonomy = new TaxonomyData(
            DocumentTypes: [],
            Metadata: new MetadataConfig
            {
                Content = new ContentMetadata
                {
                    Universal =
                    [
                        new FieldSpec { FieldName = "documentStatus", SharepointColumn = "DocumentStatus", AllowedValues = ["Draft"] },
                        new FieldSpec { FieldName = "counterparty", SharepointColumn = "Counterparty" },
                    ],
                },
            },
            Thresholds: new ConfidenceThresholds());

        var expected = ValidateSharePointSchemaActivity.ExpectedChoiceColumns(taxonomy);

        Assert.Contains(expected, e => e.ColumnName == "DocumentStatus");
        Assert.DoesNotContain(expected, e => e.ColumnName == "Counterparty");
    }

    [Fact]
    public void ExpectedChoiceColumns_ClassifyOnly_OnlyChecksDocumentType()
    {
        // DocumentOrchestrator leaves Metadata null for classify-only runs, so WriteMetadataActivity
        // never reaches a taxonomy Choice column with a real value for them -- only the unconditionally
        // written DocumentType column can actually fail, so that's all this should check.
        var taxonomy = new TaxonomyData(
            DocumentTypes: [],
            Metadata: new MetadataConfig
            {
                Content = new ContentMetadata
                {
                    Universal =
                    [
                        new FieldSpec { FieldName = "documentStatus", SharepointColumn = "DocumentStatus", AllowedValues = ["Draft"] },
                    ],
                },
            },
            Thresholds: new ConfidenceThresholds());

        var expected = ValidateSharePointSchemaActivity.ExpectedChoiceColumns(taxonomy, classifyOnly: true);

        Assert.Equal(["DocumentType"], expected.Select(e => e.ColumnName));
    }
}
