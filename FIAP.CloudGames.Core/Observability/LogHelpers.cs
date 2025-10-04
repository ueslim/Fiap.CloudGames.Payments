using System.Diagnostics;

namespace FIAP.CloudGames.Core.Observability
{
    public static class LogHelpers
    {
        public static string GetCorrelationId() =>
            Activity.Current?.GetTagItem("correlation_id")?.ToString()
            ?? Activity.Current?.TraceId.ToString()
            ?? "-";
    }
}