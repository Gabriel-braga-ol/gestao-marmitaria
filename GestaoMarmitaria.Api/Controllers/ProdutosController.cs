using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Dtos;
using GestaoMarmitaria.Api.Data;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Controllers
{
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
        public async Task<IActionResult> GetProdutos()
        {
            var produtos = await _context.Produtos
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .ToListAsync();

            return Ok(produtos);
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
        [HttpPost]
        public async Task<IActionResult> CriarProduto([FromBody] CriarProdutoDto dto)
        {
            var novoProduto = new Produto
            {
                Nome = dto.Nome,
                Descricao = dto.Descricao,
                Preco = dto.Preco,
                Ativo = true
            };

            _context.Produtos.Add(novoProduto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProdutoPorId), new { id = novoProduto.Id }, novoProduto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> AtualizarProduto(int id, [FromBody] AtualizarProdutoDto dto)
        {
            var produtoExistente = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id);

            if (produtoExistente is null) return NotFound();

            produtoExistente.Nome = dto.Nome;
            produtoExistente.Descricao = dto.Descricao;
            produtoExistente.Preco = dto.Preco;
            produtoExistente.Ativo = dto.Ativo;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> ExcluirProduto(int id)
        {
            var produtoExistente = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id);

            if (produtoExistente is null) return NotFound();

            var possuiItensPedido = await _context.ItensPedido
                .AnyAsync(i => i.ProdutoId == id);

            if (possuiItensPedido)
            {
                return Conflict(new { mensagem = "O produto possui pedidos e não pode ser excluído." });            
            }

            _context.Produtos.Remove(produtoExistente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
