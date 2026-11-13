using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GestaoMarmitaria.Api.Features.Cardapio
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CardapioController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ICardapioService _cardapioService;

        public CardapioController(
            AppDbContext context,
            ICardapioService cardapioService)
        {
            _context = context;
            _cardapioService = cardapioService;
        }

        [HttpGet("{id:int}")]
        [ActionName(nameof(GetItemCardapioPorIdAsync))]
        public async Task<IActionResult> GetItemCardapioPorIdAsync(
            int id,
            CancellationToken cancellationToken = default)
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
                .FirstOrDefaultAsync(cancellationToken);

            if (resposta is null) return NotFound();

            return Ok(resposta);
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpPost]
        public async Task<IActionResult> CriarItemCardapioAsync(
            CriarItemCardapioDto dto,
            CancellationToken cancellationToken = default)
        {
            var resultado = await _cardapioService.CriarItemCardapioAsync(
                dto,
                cancellationToken);

            return resultado.Situacao switch
            {
                SituacaoCriacaoItemCardapio.Validacao =>
                    BadRequest(new { mensagem = resultado.Mensagem }),
                SituacaoCriacaoItemCardapio.Conflito =>
                    Conflict(new { mensagem = resultado.Mensagem }),
                _ => CreatedAtAction(
                    nameof(GetItemCardapioPorIdAsync),
                    new { id = resultado.Item!.Id },
                    resultado.Item)
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetCardapioAsync(
            [FromQuery(Name = "data"), Required] DateOnly? dataCardapio,
            CancellationToken cancellationToken = default)
        {
            var itens = await _context.ItensCardapio
                .AsNoTracking()
                .Where(i => i.Data == dataCardapio!.Value)
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
                .ToListAsync(cancellationToken);

            return Ok(itens);
        }

        [HttpPatch("{id:int}/disponibilidade")]
        public async Task<IActionResult> AtualizarDisponibilidadeAsync(
            int id,
            AtualizarDisponibilidadeCardapioDto dto,
            CancellationToken cancellationToken = default)
        {
            var resultado = await _cardapioService.AtualizarDisponibilidadeAsync(
                id,
                dto,
                cancellationToken);

            return resultado.Situacao switch
            {
                SituacaoAlteracaoDisponibilidade.ItemNaoEncontrado => NotFound(),
                SituacaoAlteracaoDisponibilidade.ConflitoConcorrencia =>
                    Conflict(new { mensagem = resultado.Mensagem }),
                _ => NoContent()
            };
        }
    }
}
