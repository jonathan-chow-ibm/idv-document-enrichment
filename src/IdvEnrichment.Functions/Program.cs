using Azure.AI.DocumentIntelligence;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using IdvEnrichment.Functions.Configuration;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services
            .AddOptions<PipelineSettings>()
            .Bind(context.Configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var credential = new DefaultAzureCredential();

        services.AddSingleton<TokenCredential>(_ => credential);

        services.AddSingleton(_ => new GraphServiceClient(credential));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PipelineSettings>>().Value;
            return new DocumentIntelligenceClient(
                new Uri(settings.DocIntelligenceEndpoint), credential);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PipelineSettings>>().Value;
            return new AzureOpenAIClient(new Uri(settings.OpenAiEndpoint), credential);
        });

        services.AddSingleton<TaxonomyLoader>();

        services.AddSingleton(_ =>
        {
            var connectionString = context.Configuration["AzureWebJobsStorage"]
                ?? throw new InvalidOperationException("AzureWebJobsStorage is not configured.");
            return new TableServiceClient(connectionString);
        });
    })
    .Build();

await host.RunAsync();
