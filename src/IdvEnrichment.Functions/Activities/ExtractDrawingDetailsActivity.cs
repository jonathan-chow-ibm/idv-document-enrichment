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

public sealed class ExtractDrawingDetailsActivity(
    AzureOpenAIClient openAiClient,
    IHttpClientFactory httpClientFactory,
    IOptions<PipelineSettings> settings,
    ILogger<ExtractDrawingDetailsActivity> logger)
{
    [Function(nameof(ExtractDrawingDetails))]
    public async Task<DrawingClassification> ExtractDrawingDetails(
        [ActivityTrigger] ExtractDrawingDetailsInput input,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("spreadsheet");
        using var response = await client.GetAsync(input.DocumentUrl, ct);
        response.EnsureSuccessStatusCode();
        var pdfBytes = await response.Content.ReadAsByteArrayAsync(ct);

        byte[] pngBytes;
        try
        {
            pngBytes = PdfPageRenderer.RenderFirstPageToPng(pdfBytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PDF rendering failed for {FileName} — returning empty drawing classification", input.FileName);
            return new DrawingClassification("", "", "", 0, "PDF rendering failed");
        }

        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
        var systemPrompt = PromptRenderer.RenderClassifyDrawing();

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart($"Drawing file: {input.FileName}"),
                ChatMessageContentPart.CreateImagePart(new Uri(dataUrl))),
        };

        var chatClient = openAiClient.GetChatClient(settings.Value.OpenAiMiniDeployment);
        var sw = Stopwatch.StartNew();
        var completion = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            () => chatClient.CompleteChatAsync(
                messages,
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                },
                ct),
            logger, ct);

        var rawJson = completion.Value.Content[0].Text;
        var result = JsonSerializer.Deserialize<DrawingClassification>(rawJson)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize drawing classification response: {rawJson}");

        return result with
        {
            InputTokens = completion.Value.Usage?.InputTokenCount ?? 0,
            OutputTokens = completion.Value.Usage?.OutputTokenCount ?? 0,
            DurationMs = (int)sw.Elapsed.TotalMilliseconds,
        };
    }
}
