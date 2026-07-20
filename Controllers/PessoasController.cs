using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Models;

namespace project_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PessoasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PessoasController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pessoa>>> GetPessoas()
        {
            return await _context.Pessoas.ToListAsync();
        }
        [HttpPost]
        public async Task<ActionResult<Pessoa>> PostPessoa(Pessoa pessoa)
        {
            var cpfLimpo = new string(pessoa.Cpf?.Where(char.IsDigit).ToArray());
            
            if (string.IsNullOrEmpty(cpfLimpo) || cpfLimpo.Length != 11)
            {
                return BadRequest("O CPF deve conter exatamente 11 dígitos numéricos.");
            }
            
            var cpfJaExiste = await _context.Pessoas.AnyAsync(p => p.Cpf == pessoa.Cpf);
            if (cpfJaExiste)
            {
                return BadRequest($"O CPF '{pessoa.Cpf}' já está cadastrado no sistema.");
            }

            _context.Pessoas.Add(pessoa);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPessoas), new { id = pessoa.Id }, pessoa);
        }
        [HttpDelete("por-cpf/{cpf}")]
        public async Task<IActionResult> DeletePessoaPorCpf(string cpf)
        {
            var p = await _context.Pessoas.FirstOrDefaultAsync(pessoa => pessoa.Cpf == cpf);
            if (p == null)
            {
                return NotFound($"Nenhuma pessoa encontrada com o CPF '{cpf}'.");
            }

            _context.Pessoas.Remove(p);
            await _context.SaveChangesAsync();

            return Ok($"Pessoa com CPF {cpf} e todas as suas transações foram removidas com sucesso.");
        }
    }
}