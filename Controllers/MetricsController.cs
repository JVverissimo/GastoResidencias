using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Models;
using project_API.Services;

namespace project_API.Controllers
{
    [ApiController]
    public class MetricsController : ControllerBase
    {
        private readonly MetricsCollector _metricsCollector;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            MetricsCollector metricsCollector,
            ApplicationDbContext context,
            ILogger<MetricsController> logger)
        {
            _metricsCollector = metricsCollector;
            _context = context;
            _logger = logger;
        }

        [HttpGet("/metrics")]
        [HttpGet("/api/metrics")]
        [Produces("text/plain")]
        public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Coleta de métricas solicitada via Pull em /metrics (CorrelationId: {CorrelationId})", HttpContext.TraceIdentifier);

            bool dbUp = false;
            int? pessoasCount = null;
            int? transacoesCount = null;
            decimal? totalReceitas = null;
            decimal? totalDespesas = null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                pessoasCount = await _context.Pessoas.CountAsync(cts.Token);
                transacoesCount = await _context.Transacoes.CountAsync(cts.Token);
                totalReceitas = await _context.Transacoes
                    .Where(t => t.Tipo == TipoTransacao.Receita)
                    .SumAsync(t => (decimal?)t.Valor, cts.Token) ?? 0m;
                totalDespesas = await _context.Transacoes
                    .Where(t => t.Tipo == TipoTransacao.Despesa)
                    .SumAsync(t => (decimal?)t.Valor, cts.Token) ?? 0m;

                dbUp = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Banco de dados indisponível durante coleta de métricas de domínio ({ExceptionType}); retornando métricas de processo e HTTP em memória (CorrelationId: {CorrelationId})",
                    ex.GetType().Name,
                    HttpContext.TraceIdentifier);
            }

            var metricsPayload = _metricsCollector.FormatPrometheusMetrics(
                dbUp,
                pessoasCount,
                transacoesCount,
                totalReceitas,
                totalDespesas);

            return Content(metricsPayload, "text/plain; version=0.0.4; charset=utf-8");
        }
    }
}
