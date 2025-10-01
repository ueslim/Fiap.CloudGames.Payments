using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;

namespace FIAP.CloudGames.Core.Observability;

public static class ObservabilityConfig
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config, string serviceName)
    {
        var baseEndpoint = config["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4318";
        var tracesEndpoint = $"{baseEndpoint.TrimEnd('/')}/v1/traces";
        var metricsEndpoint = $"{baseEndpoint.TrimEnd('/')}/v1/metrics";

        services.AddOpenTelemetry()
                .ConfigureResource(r => r
                .AddService(serviceName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", config["ASPNETCORE_ENVIRONMENT"] ?? "Development"),
                    new KeyValuePair<string, object>("service.instance.id", Environment.MachineName)
                }))
                .WithTracing(t => t
                // ASP.NET Core (enriquece com correlation_id do header)
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;

                    o.EnrichWithHttpRequest = (activity, request) =>
                    {
                        if (request.Headers.TryGetValue("X-Correlation-Id", out var cid) && !string.IsNullOrWhiteSpace(cid))
                            activity.SetTag("correlation_id", cid.ToString());
                        else if (!string.IsNullOrEmpty(request.HttpContext.TraceIdentifier))
                            activity.SetTag("correlation_id", request.HttpContext.TraceIdentifier);
                    };

                    o.EnrichWithHttpResponse = (activity, response) =>
                    {
                        if (response.Headers.TryGetValue("X-Correlation-Id", out var cid) && !string.IsNullOrWhiteSpace(cid))
                            activity.SetTag("correlation_id", cid.ToString());
                    };
                })
                // HttpClient (propaga correlation_id do Activity atual)
                .AddHttpClientInstrumentation((HttpClientTraceInstrumentationOptions o) =>
                {
                    o.RecordException = true;

                    o.EnrichWithHttpRequestMessage = (activity, request) =>
                    {
                        var cid = Activity.Current?.GetTagItem("correlation_id")?.ToString();
                        if (!string.IsNullOrEmpty(cid)) activity.SetTag("correlation_id", cid);
                    };

                    o.EnrichWithHttpResponseMessage = (activity, response) =>
                    {
                        var cid = Activity.Current?.GetTagItem("correlation_id")?.ToString();
                        if (!string.IsNullOrEmpty(cid)) activity.SetTag("correlation_id", cid);
                    };

                    o.EnrichWithException = (activity, ex) =>
                    {
                        activity.SetTag("otel.status_code", "ERROR");
                        activity.SetTag("exception.type", ex.GetType().FullName);
                        activity.SetTag("exception.message", ex.Message);
                    };
                })

                // Exportador OTLP (traces)
                .AddOtlpExporter(o =>
                {
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Endpoint = new Uri(tracesEndpoint);   
                }))
                .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                // Exportador OTLP (metrics)
                .AddOtlpExporter(o =>
                {
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.Endpoint = new Uri(metricsEndpoint);
                }));

        return services;
    }
}
