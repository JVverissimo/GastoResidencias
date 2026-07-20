using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Models;

namespace project_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransacoesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TransacoesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transacao>>> GetTransacoes()
        {
            return await _context.Transacoes.Include(t => t.Pessoa).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Transacao>> PostTransacao(Transacao transacao)
        {

            var pessoa = await _context.Pessoas.FirstOrDefaultAsync(p => p.Cpf == transacao.PessoaCpf);
            if (pessoa == null)
            {
                return BadRequest($"A pessoa informada com o CPF '{transacao.PessoaCpf}' não existe no cadastro.");
            }

            if (pessoa.Idade < 18 && transacao.Tipo == TipoTransacao.Receita)
            {
                return BadRequest($"A pessoa '{pessoa.Nome}' é menor de idade ({pessoa.Idade} anos). Menores de 18 anos só podem registrar despesas.");
            }

            _context.Transacoes.Add(transacao);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTransacoes), new { id = transacao.Id }, transacao);
        }
    }
}