using System.Diagnostics;
using project_API.Helpers;
using project_API.Services;

namespace project_API.Middleware
{
    public class CorrelationIdMiddleware
    {
        public const string CorrelationIdHeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private readonly MetricsCollector _metricsCollector;

        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger,
            MetricsCollector metricsCollector)
        {
            _next = next;
            _logger = logger;
            _metricsCollector = metricsCollector;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ObterOuGerarCorrelationId(context);

            context.TraceIdentifier = correlationId;
            context.Items[CorrelationIdHeaderName] = correlationId;

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                return Task.CompletedTask;
            });

            var rawPathAndQuery = $"{context.Request.Path.Value}{context.Request.QueryString.Value}";
            var maskedPath = SensitiveDataMasker.MaskUrl(rawPathAndQuery);
            var maskedRouteOnly = SensitiveDataMasker.MaskUrl(context.Request.Path.Value);

            var scopeState = new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["HttpMethod"] = context.Request.Method,
                ["RequestPath"] = maskedPath
            };

            using (_logger.BeginScope(scopeState))
            {
                var stopwatch = Stopwatch.StartNew();
                _metricsCollector.IncrementRequestsInProgress();

                _logger.LogInformation(
                    "Requisição recebida {HttpMethod} {RequestPath} com CorrelationId {CorrelationId}",
                    context.Request.Method,
                    maskedPath,
                    correlationId);

                try
                {
                    await _next(context);
                    stopwatch.Stop();

                    var (ratePerSec60s, errorPct) = _metricsCollector.RecordHttpRequest(
                        context.Request.Method,
                        maskedRouteOnly,
                        context.Response.StatusCode,
                        stopwatch.Elapsed.TotalMilliseconds);

                    _logger.LogInformation(
                        "Requisição finalizada {HttpMethod} {RequestPath} com status {StatusCode} em {ElapsedMs}ms | RED Rate60s={RequestRatePerSec:F2}req/s ErrorPct={ErrorPercentage:F2}% (CorrelationId: {CorrelationId})",
                        context.Request.Method,
                        maskedPath,
                        context.Response.StatusCode,
                        stopwatch.ElapsedMilliseconds,
                        ratePerSec60s,
                        errorPct,
                        correlationId);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    var (ratePerSec60s, errorPct) = _metricsCollector.RecordHttpUnhandledException(
                        context.Request.Method,
                        maskedRouteOnly,
                        stopwatch.Elapsed.TotalMilliseconds);

                    _logger.LogError(
                        "Erro não tratado ({ExceptionType}: {ExceptionMessage}) ao processar {HttpMethod} {RequestPath} após {ElapsedMs}ms | RED Rate60s={RequestRatePerSec:F2}req/s ErrorPct={ErrorPercentage:F2}% (CorrelationId: {CorrelationId})",
                        ex.GetType().Name,
                        SensitiveDataMasker.SanitizeText(ex.Message),
                        context.Request.Method,
                        maskedPath,
                        stopwatch.ElapsedMilliseconds,
                        ratePerSec60s,
                        errorPct,
                        correlationId);

                    throw;
                }
                finally
                {
                    _metricsCollector.DecrementRequestsInProgress();
                }
            }
        }

        private static string ObterOuGerarCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValues))
            {
                var existingId = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(existingId))
                {
                    return existingId.Trim();
                }
            }

            return Guid.NewGuid().ToString();
        }
    }
}
