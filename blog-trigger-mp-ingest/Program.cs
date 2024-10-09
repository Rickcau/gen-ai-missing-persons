using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;

string ApiDeploymentName = Environment.GetEnvironmentVariable("ApiDeploymentName", EnvironmentVariableTarget.Process) ?? "";
string ApiEndpoint = Environment.GetEnvironmentVariable("ApiEndpoint", EnvironmentVariableTarget.Process) ?? "";
string ApiKey = Environment.GetEnvironmentVariable("ApiKey", EnvironmentVariableTarget.Process) ?? "";
// string AppInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") ?? "";  // Uncomment this if you plan top use OpenTelemetry

// If you would like to leverage OpenTelemetry with SK uncomment the code below
// You will also need the AppInsights Key in your local.settings.json file
// Here is a great link that covers this.  https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/observability/telemetry-with-console
# region SK Add OpenTelemetry
/*
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

var connectionString = AppInsightsConnectionString;
// Using resource builder to add service name to all telemetry items
var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService("TelemetryMyExample");
// Create the OpenTelemetry TracerProvider and MeterProvider
using var traceProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("Microsoft.SemanticKernel*")
    .AddSource("TelemetryMyExample")
    .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString)
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("Microsoft.SemanticKernel*")
    .AddAzureMonitorMetricExporter(options => options.ConnectionString = connectionString)
    .Build();

// Create the OpenTelemetry LoggerFactory
using var loggerFactory = LoggerFactory.Create(builder =>
{
    // Add OpenTelemetry as a logging provider
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString);
        // Format log messages. This is default to false.
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    });
    builder.SetMinimumLevel(LogLevel.Information);
});
*/
# endregion

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddTransient<Kernel>(s =>
        {
            var builder = Kernel.CreateBuilder();
            // builder.Services.AddSingleton(loggerFactory); // Uncomment this line if using SK OpenTelemetry
            builder.AddAzureOpenAIChatCompletion(
                ApiDeploymentName,
                ApiEndpoint,
                ApiKey
                );

            return builder.Build();
        });
        // If you leveage the IChatCompletionService in the future, this is needed (not using it currently)
        services.AddSingleton<IChatCompletionService>(sp =>
                    sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

        // We are not using the ChatHistory in this code but leaving this here just in-case it's needed later.
        services.AddSingleton<ChatHistory>(s =>
        {
            var chathistory = new ChatHistory();
            return chathistory;
        });

    })
    .Build();

host.Run();
