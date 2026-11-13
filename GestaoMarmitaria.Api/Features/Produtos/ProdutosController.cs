using GestaoMarmitaria.Api.Features.Compartilhado;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Seguranca;
using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Produtos
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProdutosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProdutosAsync(
            [FromQuery] ConsultarProdutosDto filtros,
            CancellationToken cancellationToken = default)
        {
            var consulta = _context.Produtos.AsNoTracking();

            var totalRegistros = await consulta.CountAsync(cancellationToken);

            var produtos = await Paginacao.BuscarPaginaAsync(
                consulta.OrderBy(p => p.Id),
                filtros,
                totalRegistros,
                cancellationToken);

            var resposta = ItensPaginadosRespostaDto<ProdutoRespostaDto>.Criar(
                produtos.Select(MapearProdutoParaResposta).ToList(),
                filtros,
                totalRegistros);

            return Ok(resposta);
        }

        [HttpGet("{id:int}")]
        [ActionName(nameof(GetProdutoPorIdAsync))]
        public async Task<IActionResult> GetProdutoPorIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (produto is null)
            {
                return NotFound();
            }

            return Ok(MapearProdutoParaResposta(produto));
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpPost]
        public async Task<IActionResult> CriarProdutoAsync(
            [FromBody] CriarProdutoDto dto,
            CancellationToken cancellationToken = default)
        {
            var novoProduto = new Produto
            {
                Nome = dto.Nome,
                Descricao = dto.Descricao,
                Preco = dto.Preco,
                Tipo = dto.Tipo,
                Ativo = true
            };

            _context.Produtos.Add(novoProduto);
            await _context.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetProdutoPorIdAsync),
                new { id = novoProduto.Id },
                MapearProdutoParaResposta(novoProduto));
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarProdutoAsync(
            int id,
            [FromBody] AtualizarProdutoDto dto,
            CancellationToken cancellationToken = default)
        {
            var produtoExistente = await _context.Produtos
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (produtoExistente is null) return NotFound();

            if (produtoExistente.Tipo != dto.Tipo)
            {
                var temVinculoCardapio = await _context.ItensCardapio
                    .AnyAsync(i => i.ProdutoId == id, cancellationToken);

                if (temVinculoCardapio)
                {
                    return Conflict(new { mensagem = "Não é possível alterar o tipo de um produto vinculado ao cardápio." });
                }
            }

            produtoExistente.Nome = dto.Nome;
            produtoExistente.Descricao = dto.Descricao;
            produtoExistente.Preco = dto.Preco;
            produtoExistente.Tipo = dto.Tipo;
            produtoExistente.Ativo = dto.Ativo;

            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [Authorize(Policy = Politicas.SomenteAdmin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> ExcluirProdutoAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var produtoExistente = await _context.Produtos
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (produtoExistente is null) return NotFound();

            var possuiItensPedido = await _context.ItensPedido
                .AnyAsync(i => i.ProdutoId == id, cancellationToken);

            var possuiItensCardapio = await _context.ItensCardapio
                .AnyAsync(i => i.ProdutoId == id, cancellationToken);

            if (possuiItensPedido || possuiItensCardapio)
            {
                return Conflict(new { mensagem = "O produto possui itens em pedidos ou no cardápio e não pode ser excluído." });
            }

            _context.Produtos.Remove(produtoExistente);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private static ProdutoRespostaDto MapearProdutoParaResposta(Produto produto)
        {
            return new ProdutoRespostaDto
            {
                Id = produto.Id,
                Nome = produto.Nome,
                Descricao = produto.Descricao,
                Preco = produto.Preco,
                Tipo = produto.Tipo,
                Ativo = produto.Ativo
            };
        }
    }
}
