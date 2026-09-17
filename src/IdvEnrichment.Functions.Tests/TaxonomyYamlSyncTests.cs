using System.Text.Json;
using System.Text.RegularExpressions;
using IdvEnrichment.Functions.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

// Guards against DocumentType drifting from docs/taxonomy/taxonomy.yaml: TaxonomyData.GetDocumentType
// looks types up by exact label match and returns null silently on a miss, so a new enum member (or a
// typo in either place) would fall back to defaults (extraction enabled, no group) rather than fail
// loudly. Plain text containment, not a real YAML parse -- the test project has no YamlDotNet reference.
public class TaxonomyYamlSyncTests
{
    [Fact]
    public void EveryDocumentType_HasAMatchingLabelInTaxonomyYaml()
    {
        var yaml = ReadTaxonomyYaml();

        foreach (var documentType in Enum.GetValues<DocumentType>())
        {
            var label = JsonSerializer.Serialize(documentType).Trim('"');
            Assert.Contains($"label: \"{label}\"", yaml);
        }
    }

    [Fact]
    public void EveryLabelInTaxonomyYaml_HasAMatchingDocumentType()
    {
        // TolerantDocumentTypeConverter sends an unmatched label to Other rather than throwing, so a
        // taxonomy-only label (typo, or a type added here but not in Enums.cs) would silently misclassify
        // every matching document instead of failing loudly. This is the enum-sync gap's other direction.
        var yaml = ReadTaxonomyYaml();
        var knownLabels = Enum.GetValues<DocumentType>()
            .Select(documentType => JsonSerializer.Serialize(documentType).Trim('"'))
            .ToHashSet();

        foreach (Match match in Regex.Matches(yaml, "label: \"([^\"]+)\""))
        {
            Assert.Contains(match.Groups[1].Value, knownLabels);
        }
    }

    private static string ReadTaxonomyYaml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "taxonomy", "taxonomy.yaml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate docs/taxonomy/taxonomy.yaml above " + AppContext.BaseDirectory);
    }
}
