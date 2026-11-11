using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Data;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace GestaoMarmitaria.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase // fornece metodos necessários para gerenciar requisições e resposta Http
    {
        private readonly AppDbContext _context;

        public ProdutosController(AppDbContext context)
        {
            _context = context;
        }

        // GEt /api/produtos
        [HttpGet]
        public async Task<IActionResult> GetProdutos([FromQuery] ConsultarProdutosDto filtros)
        {
            var consulta = _context.Produtos.AsNoTracking();

            var totalRegistros = await consulta.CountAsync();
            var quantidadeIgnorada = (long)(filtros.Pagina - 1) * filtros.TamanhoPagina;

            var produtos = quantidadeIgnorada >= totalRegistros
                ? new List<Produto>()
                : await consulta
                    .OrderBy(p => p.Id)
                    .Skip((int)quantidadeIgnorada)
                    .Take(filtros.TamanhoPagina)
                    .ToListAsync();

            var resposta = new ItensPaginadosRespostaDto<Produto>
            {
                Itens = produtos,
                Pagina = filtros.Pagina,
                TamanhoPagina = filtros.TamanhoPagina,
                TotalRegistros = totalRegistros
            };

            return Ok(resposta);
        }

        // GET /api/produtos/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProdutoPorId(int id)
        {
            var produto = await _context.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto is null)
            {
                return NotFound();
            }

            return Ok(produto);
        }

        //POST /api/produtos
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CriarProduto([FromBody] CriarProdutoDto dto)
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
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProdutoPorId), new { id = novoProduto.Id }, novoProduto);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarProdutoAsync(int id, [FromBody] AtualizarProdutoDto dto)
        {
            var produtoExistente = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id);

            if (produtoExistente is null) return NotFound();

            if (produtoExistente.Tipo != dto.Tipo)
            {
                var temVinculoCardapio = await _context.ItensCardapio.AnyAsync(i => i.ProdutoId == id); 

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

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> ExcluirProduto(int id)
        {
            var produtoExistente = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id);

            if (produtoExistente is null) return NotFound();

            var possuiItensPedido = await _context.ItensPedido
                .AnyAsync(i => i.ProdutoId == id);

            var possuiItensCardapio = await _context.ItensCardapio
                .AnyAsync(i => i.ProdutoId == id);

            if (possuiItensPedido || possuiItensCardapio)
            {
                return Conflict(new { mensagem = "O produto possui itens em pedidos ou no cardápio e não pode ser excluído." });
            }

            _context.Produtos.Remove(produtoExistente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
