using GestaoMarmitaria.Api.Data;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CardapioController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CardapioController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id:int}")]
        [ActionName(nameof(GetItemCardapioPorIdAsync))]
        public async Task<IActionResult> GetItemCardapioPorIdAsync(int id)
        {
            var resposta = await _context.ItensCardapio
                .AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => new ItemCardapioRespostaDto
                {
                    Id = i.Id,
                    Data = i.Data,
                    ProdutoId = i.ProdutoId,
                    NomeProduto = i.Produto.Nome,
                    Preco = i.Produto.Preco,
                    Disponivel = i.Disponivel
                })
                .FirstOrDefaultAsync();

            if (resposta is null) return NotFound();

            return Ok(resposta);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CriarItemCardapioAsync(CriarItemCardapioDto dto)
        {
            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == dto.ProdutoId);

            if (produto is null)
            {
                return BadRequest(new { mensagem = "O produto informado não existe." });
            }

            if (produto.Tipo != TipoProduto.Marmita)
            {
                return BadRequest(new
                {
                    mensagem = "Somente marmitas podem ser incluídas no cardápio diário."
                });
            }

            if (!produto.Ativo)
            {
                return BadRequest(new { mensagem = "O produto está inativo." });
            }

            var data = dto.Data!.Value;

            var jaExiste = await _context.ItensCardapio
                .AnyAsync(i => i.Data == data && i.ProdutoId == dto.ProdutoId);

            if (jaExiste)
            {
                return Conflict(new
                {
                    mensagem = "O produto já está no cardápio dessa data."
                });
            }

            var item = new ItemCardapio
            {
                Data = data,
                ProdutoId = produto.Id
            };

            _context.ItensCardapio.Add(item);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_ItensCardapio_Data_ProdutoId"
                })
            {
                return Conflict(new
                {
                    mensagem = "O produto já está no cardápio dessa data."
                });
            }

            var resposta = new ItemCardapioRespostaDto
            {
                Id = item.Id,
                Data = item.Data,
                ProdutoId = item.ProdutoId,
                NomeProduto = produto.Nome,
                Preco = produto.Preco,
                Disponivel = item.Disponivel
            };

            return CreatedAtAction(
                nameof(GetItemCardapioPorIdAsync),
                new { id = item.Id },
                resposta);
        }

        [HttpGet]
        public async Task<IActionResult> GetCardapioAsync([FromQuery, Required] DateOnly? data)
        {
            var itens = await _context.ItensCardapio
                .AsNoTracking()
                .Where(i => i.Data == data!.Value)
                .OrderBy(i => i.Produto.Nome)
                .ThenBy(i => i.Id)
                .Select(i => new ItemCardapioRespostaDto
                {
                    Id = i.Id,
                    Data = i.Data,
                    ProdutoId = i.ProdutoId,
                    NomeProduto = i.Produto.Nome,
                    Preco = i.Produto.Preco,
                    Disponivel = i.Disponivel
                })
                .ToListAsync();

            return Ok(itens);
        }

        [HttpPatch("{id:int}/disponibilidade")]
        public async Task<IActionResult> AtualizarDisponibilidadeAsync(int id, AtualizarDisponibilidadeCardapioDto dto)
        {
            var item = await _context.ItensCardapio
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item is null) return NotFound();

            item.Disponivel = dto.Disponivel!.Value;

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}