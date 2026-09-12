using IdvEnrichment.Functions.Models;
using System.Text.Json;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class TolerantDocumentTypeConverterTests
{
    [Theory]
    [InlineData("PSA - Acquisition", DocumentType.PsaAcquisition)]
    [InlineData("Design Drawing", DocumentType.DesignDrawing)]
    [InlineData("Budget / Cost Estimate", DocumentType.BudgetCostEstimate)]
    [InlineData("Other", DocumentType.Other)]
    public void Read_KnownTaxonomyLabel_Deserializes(string wireName, DocumentType expected)
    {
        var result = JsonSerializer.Deserialize<DocumentType>($"\"{wireName}\"");

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Proposal/Pitch Deck")]
    [InlineData("Marketing Flyer")]
    [InlineData("Correspondence")]
    [InlineData("")]
    public void Read_TypeOutsideTaxonomy_FallsBackToOtherInsteadOfThrowing(string wireName)
    {
        // The whole point: Agent 1 has no response schema, so it can return any string. This used to throw
        // a JsonException and fail the entire classification -- including a confident primary answer.
        var result = JsonSerializer.Deserialize<DocumentType>($"\"{wireName}\"");

        Assert.Equal(DocumentType.Other, result);
    }

    [Fact]
    public void Read_UnknownCandidateInsideResult_DoesNotFailTheClassification()
    {
        var json = """
            {"documentType":"Lease","confidence":0.55,"reasoning":"lease-like",
             "candidates":[{"documentType":"Proposal/Pitch Deck","confidence":0.3}]}
            """;

        var result = JsonSerializer.Deserialize<TypeClassificationResult>(json);

        Assert.NotNull(result);
        Assert.Equal(DocumentType.Lease, result.DocumentType);
        Assert.Equal("Proposal/Pitch Deck", Assert.Single(result.Candidates!).DocumentType);
    }

    [Theory]
    [InlineData(DocumentType.PsaAcquisition, "PSA - Acquisition")]
    [InlineData(DocumentType.DesignDrawing, "Design Drawing")]
    [InlineData(DocumentType.BudgetCostEstimate, "Budget / Cost Estimate")]
    [InlineData(DocumentType.Other, "Other")]
    public void Write_EmitsTheTaxonomyLabel(DocumentType value, string expected)
    {
        // WriteMetadataActivity serializes this straight into a SharePoint Choice column, which rejects the
        // entire fields PATCH on an unrecognized value -- so serialization must stay byte-identical to the
        // JsonStringEnumConverter this replaced.
        var json = JsonSerializer.Serialize(value);

        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void Write_EveryEnumMember_RoundTripsBackToItself()
    {
        foreach (var value in Enum.GetValues<DocumentType>())
        {
            var json = JsonSerializer.Serialize(value);

            Assert.Equal(value, JsonSerializer.Deserialize<DocumentType>(json));
        }
    }

    [Fact]
    public void TryParseWireName_DistinguishesOtherFromUnrecognized()
    {
        // Callers need to tell "the model said Other" apart from "the model said something we don't know",
        // which the converter's fallback deliberately erases.
        Assert.True(TolerantDocumentTypeConverter.TryParseWireName("Other", out var other));
        Assert.Equal(DocumentType.Other, other);

        Assert.False(TolerantDocumentTypeConverter.TryParseWireName("Proposal/Pitch Deck", out _));
        Assert.False(TolerantDocumentTypeConverter.TryParseWireName(null, out _));
    }
}
