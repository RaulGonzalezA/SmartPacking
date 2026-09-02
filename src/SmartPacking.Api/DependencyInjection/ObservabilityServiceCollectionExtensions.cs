using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace SmartPacking.Api.DependencyInjection;

public static class ObservabilityServiceCollectionExtensions
{
    public static WebApplicationBuilder AddSmartPackingObservability(this WebApplicationBuilder builder)
    {
        var seqUrl = builder.Configuration["Observability:SeqUrl"];
        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"];

        builder.Host.UseSerilog((_, _, loggerConfiguration) =>
        {
            loggerConfiguration.Enrich.FromLogContext().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                loggerConfiguration.WriteTo.Seq(seqUrl, formatProvider: CultureInfo.InvariantCulture);
            }
        });
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("SmartPacking.Api"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }
}
