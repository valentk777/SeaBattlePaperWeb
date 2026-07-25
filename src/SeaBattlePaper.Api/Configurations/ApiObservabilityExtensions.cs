using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SeaBattlePaper.Api.Configurations;

internal static class ApiObservabilityExtensions
{
    internal static WebApplicationBuilder AddApiObservability(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var enabled = config.GetValue("OpenTelemetry:Enabled", true);
        if (!enabled)
            return builder;

        var serviceName = config["OpenTelemetry:ServiceName"] ?? "SeaBattlePaper.Api";
        var serviceVersion = typeof(ApiObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";
        var otlpEndpoint = config["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
        var sampleRatio = config.GetValue("OpenTelemetry:SampleRatio", 0.2);
        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName
            });

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resource);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = new Uri(otlpEndpoint);
                exporter.Protocol = OtlpExportProtocol.Grpc;
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder.AddService(
                serviceName,
                serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio)))
                    .AddAspNetCoreInstrumentation(options => { options.RecordException = true; })
                    .AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = new Uri(otlpEndpoint);
                        exporter.Protocol = OtlpExportProtocol.Grpc;
                    });
            });

        return builder;
    }
}
