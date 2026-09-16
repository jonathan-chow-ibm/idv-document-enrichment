using Azure.Core;
using Azure.Storage.Blobs;
using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IdvEnrichment.Functions.Shared;

public sealed class TaxonomyLoader(IOptions<PipelineSettings> settings, TokenCredential credential, ILogger<TaxonomyLoader> logger)
{
    private readonly Lazy<Task<TaxonomyData>> _data =
        new(() => LoadInternalAsync(settings.Value, credential, logger),
            LazyThreadSafetyMode.PublicationOnly);

    public Task<TaxonomyData> LoadAsync(CancellationToken ct = default) => _data.Value;

    private static async Task<TaxonomyData> LoadInternalAsync(
        PipelineSettings settings, TokenCredential credential, ILogger logger, CancellationToken ct = default)
    {
        string yaml;
        if (settings.TaxonomyBlobUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var blobClient = new BlobClient(new Uri(settings.TaxonomyBlobUrl), credential);
            var response = await SdkExceptionHelper.RunAsync(
                () => blobClient.DownloadContentAsync(ct),
                $"Taxonomy blob download from {settings.TaxonomyBlobUrl}",
                logger);
            yaml = response.Value.Content.ToString();
        }
        else
        {
            yaml = await File.ReadAllTextAsync(settings.TaxonomyBlobUrl, ct);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<TaxonomyConfig>(yaml);
        return new TaxonomyData(config.DocumentTypes, config.Metadata, config.ConfidenceThresholds);
    }
}
