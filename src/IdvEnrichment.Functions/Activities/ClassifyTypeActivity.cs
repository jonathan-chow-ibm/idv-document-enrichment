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
        var completion = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            () => chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(userPrompt),
                ],
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                },
                ct),
            logger, ct);

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
        return JsonSerializer.Deserialize<TypeClassificationResult>(rawJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize type classification response: {rawJson}");
    }
}
