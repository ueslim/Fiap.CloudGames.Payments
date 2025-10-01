using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace FIAP.CloudGames.Core.Observability
{
    public static class BusTracePropagation
    {
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        public const string CorrelationHeader = "X-Correlation-Id";

        public static void Inject(IDictionary<string, object> headers, string? correlationId = null)
        {
            var ctx = new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current);
            Propagator.Inject(ctx, headers, (carrier, key, value) => carrier[key] = value);

            correlationId ??= Activity.Current?.GetTagItem("correlation_id")?.ToString();

            if (!string.IsNullOrWhiteSpace(correlationId))
                headers[CorrelationHeader] = correlationId!;
        }

        public static Activity? StartConsumerActivity(string operationName, IDictionary<string, object>? headers, ActivitySource source)
        {
            try
            {
                var carrier = headers ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                Propagator.Extract(default, carrier, (c, key) =>
                {
                    if (c.TryGetValue(key, out var val))
                    {
                        if (val is string s) return new[] { s };
                        if (val is byte[] b) return new[] { System.Text.Encoding.UTF8.GetString(b) };
                    }
                    return [];
                });
            }
            catch { }

            var parentCtx = Activity.Current?.Context ?? default;
            var activity = source.StartActivity(operationName, ActivityKind.Consumer, parentCtx);

            if (headers != null && headers.TryGetValue(CorrelationHeader, out var cidObj) && cidObj is string cid && !string.IsNullOrWhiteSpace(cid))
            {
                activity?.SetTag("correlation_id", cid);
            }

            return activity;
        }
    }
}