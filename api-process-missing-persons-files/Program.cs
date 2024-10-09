using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

string ApiDeploymentName = Environment.GetEnvironmentVariable("ApiDeploymentName", EnvironmentVariableTarget.Process) ?? "";
string ApiEndpoint = Environment.GetEnvironmentVariable("ApiEndpoint", EnvironmentVariableTarget.Process) ?? "";
string ApiKey = Environment.GetEnvironmentVariable("ApiKey", EnvironmentVariableTarget.Process) ?? "";
// Uncomment the SK Add OpenTelemetry if you would like to add OpenTelemetry for Semantic Kernel
// You will also need the AppInsights Key in yhour locak.settings.json file
// Here is a great link that covers this.  https://learn.microsoft.com/en-us/semantic-kernel/concepts/enterprise-readiness/observability/telemetry-with-console
# region SK Add OpenTelemetry
/*
    string AppInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") ?? "";
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
    //.ConfigureOpenApi() // add this line to enable OpenAPI Swagger UI
    // also install the nuget package Microsoft.Azure.Functions.Worker.Extensions.OpenApi and add a using statement at the top
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddTransient<Kernel>(s =>
        {
            var builder = Kernel.CreateBuilder();
            //builder.Services.AddSingleton(loggerFactory); // uncomment this line if using SK OpenTelemetry
            builder.AddAzureOpenAIChatCompletion(
                ApiDeploymentName,
                ApiEndpoint,
                ApiKey
                );

            return builder.Build();
        });
        services.AddSingleton<IChatCompletionService>(sp =>
                     sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

        // ChatHistory and ICompletion aren't really need in this API but leaving it in just for now.
        services.AddSingleton<ChatHistory>(s =>
        {
            var chathistory = new ChatHistory();
            return chathistory;
        });

    })
    .Build();

host.Run();
