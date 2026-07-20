using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinancialController.Data;
using project_API.Models;

namespace project_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TotaisController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TotaisController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTotaisGerais()
        {
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

            var respostaFinal = new {
                Pessoas = relatorioPessoas,
                ConsolidadoGeral = new {
                    TotalGeralReceitas = geralReceitas,
                    TotalGeralDespesas = geralDespesas,
                    SaldoLiquidoGeral = geralReceitas - geralDespesas
                }
            };

            return Ok(respostaFinal);
        }
    }
}