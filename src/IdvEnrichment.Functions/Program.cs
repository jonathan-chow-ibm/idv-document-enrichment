using IdvEnrichment.Functions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    })
    .Build();

await host.RunAsync();
