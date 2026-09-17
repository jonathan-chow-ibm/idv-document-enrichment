using IdvEnrichment.Functions.Activities;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ClassifyTypeActivityTests
{
    [Fact]
    public void DetermineUnrecognizedType_KnownLabel_ReturnsNull()
    {
        var rawJson = """{"documentType":"Lease","confidence":0.9,"reasoning":"clear"}""";

        var result = ClassifyTypeActivity.DetermineUnrecognizedType(rawJson);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineUnrecognizedType_LiteralOther_ReturnsNull()
    {
        // "Other" is itself a taxonomy label, so the model deliberately choosing it isn't an
        // unrecognized guess worth recovering.
        var rawJson = """{"documentType":"Other","confidence":0.5,"reasoning":"unclear"}""";

        var result = ClassifyTypeActivity.DetermineUnrecognizedType(rawJson);

        Assert.Null(result);
    }

    [Fact]
    public void DetermineUnrecognizedType_LabelOutsideTaxonomy_ReturnsRawWording()
    {
        var rawJson = """{"documentType":"Marketing Flyer","confidence":0.9,"reasoning":"clearly a flyer"}""";

        var result = ClassifyTypeActivity.DetermineUnrecognizedType(rawJson);

        Assert.Equal("Marketing Flyer", result);
    }

    [Fact]
    public void DetermineUnrecognizedType_MissingDocumentTypeProperty_ReturnsNull()
    {
        var rawJson = """{"confidence":0.9,"reasoning":"clear"}""";

        var result = ClassifyTypeActivity.DetermineUnrecognizedType(rawJson);

        Assert.Null(result);
    }
}
