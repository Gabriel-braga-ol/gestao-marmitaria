using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Features.Cardapio;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace GestaoMarmitaria.Api.Features.Cardapio
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
                FalhasBanco.EhViolacaoDeIndiceUnico(ex, "IX_ItensCardapio_Data_ProdutoId"))
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
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var item = await _context.ItensCardapio
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (item is null) return NotFound();

                item.Disponivel = dto.Disponivel!.Value;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
            {
                return Conflict(new
                {
                    mensagem = "Ocorreu um conflito de concorrência ao atualizar a disponibilidade. Tente novamente."
                });
            }
        }
    }
}