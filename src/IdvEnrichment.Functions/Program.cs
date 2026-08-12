using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using IdvEnrichment.Functions.Configuration;
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

        services.AddSingleton(_ => new GraphServiceClient(credential));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PipelineSettings>>().Value;
            return new DocumentIntelligenceClient(
                new Uri(settings.DocIntelligenceEndpoint), credential);
        });
    })
    .Build();

await host.RunAsync();
