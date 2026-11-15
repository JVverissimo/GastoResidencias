using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Helpers;
using project_API.Models;
using project_API.Services;

namespace project_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransacoesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TransacoesController> _logger;
        private readonly MetricsCollector _metricsCollector;

        public TransacoesController(
            ApplicationDbContext context,
            ILogger<TransacoesController> logger,
            MetricsCollector metricsCollector)
        {
            _context = context;
            _logger = logger;
            _metricsCollector = metricsCollector;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transacao>>> GetTransacoes()
        {
            _logger.LogInformation("Consultando histórico de transações (CorrelationId: {CorrelationId})", HttpContext.TraceIdentifier);
            var transacoes = await _context.Transacoes.Include(t => t.Pessoa).ToListAsync();
            _logger.LogInformation("Histórico de transações retornado com {TotalTransacoes} registros (CorrelationId: {CorrelationId})", transacoes.Count, HttpContext.TraceIdentifier);
            return transacoes;
        }

        [HttpPost]
        public async Task<ActionResult<Transacao>> PostTransacao(Transacao transacao)
        {
            var cpfMascarado = SensitiveDataMasker.MaskCpf(transacao.PessoaCpf);

            _logger.LogInformation(
                "Iniciando registro de transação do tipo {TipoTransacao} no valor de {Valor} para CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                transacao.Tipo,
                transacao.Valor,
                cpfMascarado,
                HttpContext.TraceIdentifier);

            var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Cpf == transacao.PessoaCpf);
            if (pessoa == null)
            {
                _metricsCollector.RecordTransacaoRejeitada("pessoa_inexistente");
                _logger.LogWarning(
                    "Falha ao registrar transação: pessoa com CPF {CpfMascarado} não encontrada no cadastro (CorrelationId: {CorrelationId})",
                    cpfMascarado,
                    HttpContext.TraceIdentifier);
                return BadRequest($"A pessoa informada com o CPF '{transacao.PessoaCpf}' não existe no cadastro.");
            }

            if (pessoa.Idade < 18 && transacao.Tipo == TipoTransacao.Receita)
            {
                _metricsCollector.RecordTransacaoRejeitada("menor_idade_receita");
                _logger.LogWarning(
                    "Regra de negócio violada: menor de idade ({Idade} anos, PessoaId {PessoaId}, CPF {CpfMascarado}) tentou registrar receita (CorrelationId: {CorrelationId})",
                    pessoa.Idade,
                    pessoa.Id,
                    cpfMascarado,
                    HttpContext.TraceIdentifier);
                return BadRequest($"A pessoa '{pessoa.Nome}' é menor de idade ({pessoa.Idade} anos). Menores de 18 anos só podem registrar despesas.");
            }

            _context.Transacoes.Add(transacao);
            await _context.SaveChangesAsync();
            var volumeSessaoTotal = _metricsCollector.RecordTransacaoRegistrada(
                transacao.Tipo == TipoTransacao.Receita,
                transacao.Valor);

            _logger.LogInformation(
                "Transação registrada com sucesso com Id {TransacaoId}, Tipo {TipoTransacao}, Valor {Valor}, VolumeAcumuladoSessao {VolumeAcumuladoSessao} para PessoaId {PessoaId} e CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                transacao.Id,
                transacao.Tipo,
                transacao.Valor,
                volumeSessaoTotal,
                pessoa.Id,
                cpfMascarado,
                HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetTransacoes), new { id = transacao.Id }, transacao);
        }
    }
}