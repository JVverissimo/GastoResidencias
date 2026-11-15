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
    public class PessoasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PessoasController> _logger;
        private readonly MetricsCollector _metricsCollector;

        public PessoasController(
            ApplicationDbContext context,
            ILogger<PessoasController> logger,
            MetricsCollector metricsCollector)
        {
            _context = context;
            _logger = logger;
            _metricsCollector = metricsCollector;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pessoa>>> GetPessoas()
        {
            _logger.LogInformation("Consultando lista de pessoas cadastradas (CorrelationId: {CorrelationId})", HttpContext.TraceIdentifier);
            var pessoas = await _context.Pessoas.ToListAsync();
            _logger.LogInformation("Lista de pessoas retornada com {TotalPessoas} registros (CorrelationId: {CorrelationId})", pessoas.Count, HttpContext.TraceIdentifier);
            return pessoas;
        }
        [HttpPost]
        public async Task<ActionResult<Pessoa>> PostPessoa(Pessoa pessoa)
        {
            var cpfMascarado = SensitiveDataMasker.MaskCpf(pessoa.Cpf);

            _logger.LogInformation(
                "Iniciando cadastro de nova pessoa com Nome {Nome}, Idade {Idade} e CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                pessoa.Nome,
                pessoa.Idade,
                cpfMascarado,
                HttpContext.TraceIdentifier);

            var cpfLimpo = new string(pessoa.Cpf?.Where(char.IsDigit).ToArray());
            
            if (string.IsNullOrEmpty(cpfLimpo) || cpfLimpo.Length != 11)
            {
                _metricsCollector.RecordPessoaRejeitada("cpf_invalido");
                _logger.LogWarning(
                    "Falha na validação de cadastro de pessoa: CPF {CpfMascarado} inválido com {QuantidadeDigitos} dígitos (CorrelationId: {CorrelationId})",
                    cpfMascarado,
                    cpfLimpo?.Length ?? 0,
                    HttpContext.TraceIdentifier);
                return BadRequest("O CPF deve conter exatamente 11 dígitos numéricos.");
            }
            
            var cpfJaExiste = await _context.Pessoas.AnyAsync(p => p.Cpf == pessoa.Cpf);
            if (cpfJaExiste)
            {
                _metricsCollector.RecordPessoaRejeitada("cpf_duplicado");
                _logger.LogWarning(
                    "Tentativa de cadastro com CPF duplicado {CpfMascarado} para Nome {Nome} (CorrelationId: {CorrelationId})",
                    cpfMascarado,
                    pessoa.Nome,
                    HttpContext.TraceIdentifier);
                return BadRequest($"O CPF '{pessoa.Cpf}' já está cadastrado no sistema.");
            }

            _context.Pessoas.Add(pessoa);
            await _context.SaveChangesAsync();
            _metricsCollector.IncrementPessoaCadastrada();

            _logger.LogInformation(
                "Pessoa cadastrada com sucesso com Id {PessoaId}, Nome {Nome} e CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                pessoa.Id,
                pessoa.Nome,
                cpfMascarado,
                HttpContext.TraceIdentifier);

            return CreatedAtAction(nameof(GetPessoas), new { id = pessoa.Id }, pessoa);
        }
        [HttpDelete("por-cpf/{cpf}")]
        public async Task<IActionResult> DeletePessoaPorCpf(string cpf)
        {
            var cpfMascarado = SensitiveDataMasker.MaskCpf(cpf);

            _logger.LogInformation(
                "Solicitação de exclusão de pessoa recebida para CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                cpfMascarado,
                HttpContext.TraceIdentifier);

            var p = await _context.Pessoas.FirstOrDefaultAsync(pessoa => pessoa.Cpf == cpf);
            if (p == null)
            {
                _metricsCollector.RecordPessoaRejeitada("cpf_nao_encontrado");
                _logger.LogWarning(
                    "Pessoa não encontrada para exclusão com CPF {CpfMascarado} (CorrelationId: {CorrelationId})",
                    cpfMascarado,
                    HttpContext.TraceIdentifier);
                return NotFound($"Nenhuma pessoa encontrada com o CPF '{cpf}'.");
            }

            _context.Pessoas.Remove(p);
            await _context.SaveChangesAsync();
            _metricsCollector.IncrementPessoaRemovida();

            _logger.LogInformation(
                "Pessoa com Id {PessoaId} e CPF {CpfMascarado} e suas transações foram removidas com sucesso (CorrelationId: {CorrelationId})",
                p.Id,
                cpfMascarado,
                HttpContext.TraceIdentifier);

            return Ok($"Pessoa com CPF {cpf} e todas as suas transações foram removidas com sucesso.");
        }
    }
}