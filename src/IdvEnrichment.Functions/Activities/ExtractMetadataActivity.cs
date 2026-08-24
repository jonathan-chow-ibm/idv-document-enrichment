using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace IdvEnrichment.Functions.Activities;

public sealed class ExtractMetadataActivity(
    AzureOpenAIClient openAiClient,
    TaxonomyLoader taxonomyLoader,
    IOptions<PipelineSettings> settings,
    ILogger<ExtractMetadataActivity> logger)
{
    [Function(nameof(ExtractMetadata))]
    public async Task<MetadataExtractionResult> ExtractMetadata(
        [ActivityTrigger] ExtractMetadataInput input,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        var systemPrompt = PromptRenderer.RenderExtractMetadata(input.DocumentType, taxonomy);
        var userPrompt = PromptRenderer.RenderUserDocument(
            input.FileName,
            TextUtils.TruncateForExtraction(input.ExtractedText),
            input.KeyValuePairs);

        var schema = MetadataSchemaBuilder.BuildSchema(input.DocumentType, taxonomy);
        var chatClient = openAiClient.GetChatClient(settings.Value.OpenAiDeployment);
        var sw = Stopwatch.StartNew();
        var completion = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            () => chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt),
                ],
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        "metadata",
                        schema,
                        jsonSchemaIsStrict: true),
                },
                ct),
            logger, ct);

        var reason = completion.Value.FinishReason;
        if (reason == ChatFinishReason.ContentFilter)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI content filter rejected document '{input.DocumentId}'.");
        }

        if (reason == ChatFinishReason.Length)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI response exceeded max tokens for document '{input.DocumentId}'.");
        }

        if (completion.Value.Content.Count == 0)
        {
            logger.LogWarning("OpenAI returned empty content for document {DocumentId}", input.DocumentId);
            throw new InvalidOperationException(
                $"Azure OpenAI returned empty content for document '{input.DocumentId}'.");
        }

        var rawJson = completion.Value.Content[0].Text;
        var result = JsonSerializer.Deserialize<MetadataExtractionResult>(rawJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize metadata extraction response for document '{input.DocumentId}': {rawJson}");
        return result with
        {
            InputTokens = completion.Value.Usage?.InputTokenCount ?? 0,
            OutputTokens = completion.Value.Usage?.OutputTokenCount ?? 0,
            DurationMs = (int)sw.Elapsed.TotalMilliseconds,
        };
    }
}
