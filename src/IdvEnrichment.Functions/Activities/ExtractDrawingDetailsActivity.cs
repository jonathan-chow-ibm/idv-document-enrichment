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

        var systemPrompt = PromptRenderer.RenderClassifyDrawing();

        var contentParts = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(
                $"Drawing file: {input.FileName}\n\n" +
                (string.IsNullOrWhiteSpace(input.ExtractedText)
                    ? "No OCR text available."
                    : $"OCR-extracted text from this drawing:\n{TextUtils.TruncateForClassification(input.ExtractedText, 2000)}\n\nUse the OCR text for exact strings (firm names, titles, sheet numbers). Use the image for layout context.")),
            ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(pngBytes), "image/png"),
        };
        var userMessage = new UserChatMessage(contentParts);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            userMessage,
        };

        var chatClient = openAiClient.GetChatClient(settings.Value.OpenAiMiniDeployment);
        var sw = Stopwatch.StartNew();
        var completion = await SdkExceptionHelper.RunAsync(
            () => OpenAiRetryHelper.ExecuteWithRetryAsync(
                callCt => chatClient.CompleteChatAsync(
                    messages,
                    new ChatCompletionOptions
                    {
                        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                    },
                    callCt),
                logger, ct),
            $"Drawing classification for {input.FileName}",
            logger);

        if (completion.Value.Content.Count == 0)
        {
            var reason = completion.Value.FinishReason;
            if (reason == ChatFinishReason.ContentFilter)
            {
                logger.LogWarning("Vision content filter triggered for {FileName}", input.FileName);
            }
            else if (reason == ChatFinishReason.Length)
            {
                logger.LogWarning("Vision response truncated for {FileName}", input.FileName);
            }
            else
            {
                logger.LogWarning("Vision returned empty content for {FileName}", input.FileName);
            }

            return new DrawingClassification("", "", "", 0.0, "Vision call returned no content");
        }

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
