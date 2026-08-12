using System.ComponentModel.DataAnnotations;

namespace IdvEnrichment.Functions.Configuration;

/// <summary>Pipeline configuration bound from environment variables and app settings.</summary>
public sealed class PipelineSettings
{
    // Agent 1 (classification) uses mini; Agent 2 (extraction) uses full GPT-4o
    [Required]
    [Url]
    public string OpenAiEndpoint { get; init; } = string.Empty;

    public string OpenAiDeployment { get; init; } = "gpt-4o";

    public string OpenAiMiniDeployment { get; init; } = "gpt-4o-mini";

    [Required]
    [Url]
    public string DocIntelligenceEndpoint { get; init; } = string.Empty;

    [Required]
    public string TaxonomyBlobUrl { get; init; } = string.Empty; // accepts https:// blob URL or local file path for dev

    [Range(0.0, 1.0)]
    public double ConfidenceThresholdDefault { get; init; } = 0.80;

    [Range(1, 100)]
    public int BatchMaxConcurrency { get; init; } = 10;
}
