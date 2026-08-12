using System.ComponentModel.DataAnnotations;

namespace IdvEnrichment.Functions.Configuration;

/// <summary>Pipeline configuration bound from environment variables and app settings.</summary>
public sealed class PipelineSettings
{
    // Agent 1 (classification) uses mini; Agent 2 (extraction) uses full GPT-4o
    [Required]
    public string OpenAiEndpoint { get; init; } = string.Empty;

    public string OpenAiDeployment { get; init; } = "gpt-4o";

    public string OpenAiMiniDeployment { get; init; } = "gpt-4o-mini";

    public string OpenAiApiVersion { get; init; } = "2024-12-01-preview";

    [Required]
    public string DocIntelligenceEndpoint { get; init; } = string.Empty;

    public string TaxonomyBlobUrl { get; init; } = string.Empty;

    [Range(0.0, 1.0)]
    public double ConfidenceThresholdDefault { get; init; } = 0.80;

    [Range(1, 100)]
    public int BatchMaxConcurrency { get; init; } = 10;
}
