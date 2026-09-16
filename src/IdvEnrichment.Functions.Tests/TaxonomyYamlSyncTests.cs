using System.Text.Json;
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
