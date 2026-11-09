using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Models;
using project_API.Services;

namespace project_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TotaisController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TotaisController> _logger;
        private readonly MetricsCollector _metricsCollector;

        public TotaisController(
            ApplicationDbContext context,
            ILogger<TotaisController> logger,
            MetricsCollector metricsCollector)
        {
            _context = context;
            _logger = logger;
            _metricsCollector = metricsCollector;
        }

        [HttpGet]
        public async Task<IActionResult> GetTotaisGerais()
        {
            _metricsCollector.IncrementConsultaTotais();
            _logger.LogInformation("Iniciando cálculo de totais gerais consolidados (CorrelationId: {CorrelationId})", HttpContext.TraceIdentifier);

            var pessoas = await _context.Pessoas.Include(p => p.Transacoes).ToListAsync();

            var relatorioPessoas = pessoas.Select(p => {
                var totalReceitas = p.Transacoes.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor);
                var totalDespesas = p.Transacoes.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor);
                                
                return new {
                    PessoaId = p.Id,
                    Nome = p.Nome,
                    Idade = p.Idade,
                    Cpf = p.Cpf,
                    TotalReceitas = totalReceitas,
                    TotalDespesas = totalDespesas,
                    Saldo = totalReceitas - totalDespesas
                };
            }).ToList();

            var geralReceitas = relatorioPessoas.Sum(r => r.TotalReceitas);
            var geralDespesas = relatorioPessoas.Sum(r => r.TotalDespesas);
            var saldoLiquidoGeral = geralReceitas - geralDespesas;

            _logger.LogInformation(
                "Totais gerais calculados para {TotalPessoas} pessoas: Receitas={TotalGeralReceitas}, Despesas={TotalGeralDespesas}, Saldo={SaldoLiquidoGeral} (CorrelationId: {CorrelationId})",
                relatorioPessoas.Count,
                geralReceitas,
                geralDespesas,
                saldoLiquidoGeral,
                HttpContext.TraceIdentifier);

            var respostaFinal = new {
                Pessoas = relatorioPessoas,
                ConsolidadoGeral = new {
                    TotalGeralReceitas = geralReceitas,
                    TotalGeralDespesas = geralDespesas,
                    SaldoLiquidoGeral = saldoLiquidoGeral
                }
            };

            return Ok(respostaFinal);
        }
    }
}