using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace FIAP.CloudGames.WebAPI.Core.Middleware
{
    public sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx)
        {
            var correlationId =
                (ctx.Request.Headers.TryGetValue(HeaderName, out var h) && !string.IsNullOrWhiteSpace(h))
                ? h.ToString()
                : Guid.NewGuid().ToString();

            // devolve no response
            ctx.Response.OnStarting(() =>
            {
                ctx.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            // coloca no Activity + Serilog
            Activity.Current?.SetTag("correlation_id", correlationId);
            using (Serilog.Context.LogContext.PushProperty("correlation_id", correlationId))
            {
                await _next(ctx);
            }
        }
    }

    public static class CorrelationIdExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
