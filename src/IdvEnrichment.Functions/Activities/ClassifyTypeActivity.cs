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

public sealed class ClassifyTypeActivity(
    AzureOpenAIClient openAiClient,
    TaxonomyLoader taxonomyLoader,
    IOptions<PipelineSettings> settings,
    ILogger<ClassifyTypeActivity> logger)
{
    [Function(nameof(ClassifyType))]
    public async Task<TypeClassificationResult> ClassifyType(
        [ActivityTrigger] ClassifyTypeInput input,
        CancellationToken ct = default)
    {
        var taxonomy = await taxonomyLoader.LoadAsync(ct);
        var systemPrompt = PromptRenderer.RenderClassifyType(taxonomy.DocumentTypes);
        var userPrompt = PromptRenderer.RenderUserDocument(
            input.FileName,
            TextUtils.TruncateForClassification(input.ExtractedText),
            input.KeyValuePairs);

        var chatClient = openAiClient.GetChatClient(settings.Value.OpenAiMiniDeployment);
        var sw = Stopwatch.StartNew();
        var completion = await SdkExceptionHelper.RunAsync(
            () => OpenAiRetryHelper.ExecuteWithRetryAsync(
                callCt => chatClient.CompleteChatAsync(
                    [
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt),
                    ],
                    new ChatCompletionOptions
                    {
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                    },
                    callCt),
                logger, ct),
            $"Type classification for document {input.DocumentId}",
            logger);

        if (completion.Value.Content.Count == 0)
        {
            var reason = completion.Value.FinishReason;
            if (reason == ChatFinishReason.ContentFilter)
            {
                throw new InvalidOperationException($"Content filter rejected document '{input.DocumentId}'.");
            }

            if (reason == ChatFinishReason.Length)
            {
                throw new InvalidOperationException($"Response truncated for document '{input.DocumentId}'.");
            }

            throw new InvalidOperationException("OpenAI returned an empty content list.");
        }

        var rawJson = completion.Value.Content[0].Text;
        var result = JsonSerializer.Deserialize<TypeClassificationResult>(rawJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize type classification response: {rawJson}");
        return result with
        {
            InputTokens = completion.Value.Usage?.InputTokenCount ?? 0,
            OutputTokens = completion.Value.Usage?.OutputTokenCount ?? 0,
            DurationMs = (int)sw.Elapsed.TotalMilliseconds,
            UnrecognizedType = DetermineUnrecognizedType(rawJson),
        };
    }

    // Internal for testability. TolerantDocumentTypeConverter already coerced the deserialized result's
    // DocumentType to Other by the time we get here, so the model's actual wording has to be recovered
    // straight from the raw response -- not from the parsed record.
    internal static string? DetermineUnrecognizedType(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("documentType", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var rawLabel = typeElement.GetString();
        return string.IsNullOrWhiteSpace(rawLabel) || TolerantDocumentTypeConverter.TryParseWireName(rawLabel, out _)
            ? null
            : rawLabel;
    }
}
