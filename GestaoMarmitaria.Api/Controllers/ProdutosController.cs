using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace GestaoMarmitaria.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase // fornece metodos necessários para gerenciar requisições e resposta Http
    {
        private readonly List<Produto> _produtos = new()
        {
            new Produto
            {
                Id = 1,
                Nome = "Marmita M - Frango à milanesa",
                Descricao = "Acompanha arroz branco, feijão carioca e purê de batata",
                Preco = 22.50m,
            },
            new Produto
            {
                Id = 2,
                Nome = "Suco natural de laranja 500ml",
                Preco = 8.00m,
            }
        };

        // GEt /api/produtos
        [HttpGet]
        public IActionResult GetProdutos()
        {
            return Ok(_produtos);
        }

        // GET /api/produtos/{id}
        [HttpGet("{id:int}")]
        public IActionResult GetProdutoPorId(int id)
        {
            var produto = _produtos.FirstOrDefault(p => p.Id == id);

            if (produto == null)
            {
                return NotFound();
            }

            return Ok(produto);
        }

        //POST /api/produtos
        [HttpPost]
        public IActionResult CriarProduto([FromBody] CriarProdutoDto dto)
        {
            int novoId = _produtos.Any() ? _produtos.Max(p => p.Id) + 1 : 1;

            var novoProduto = new Produto
            {
                Id = novoId,
                Nome = dto.Nome,
                Descricao = dto.Descricao,
                Preco = dto.Preco,
                Ativo = true
            };

            _produtos.Add(novoProduto);

            return CreatedAtAction(nameof(GetProdutoPorId), new { id = novoProduto.Id }, novoProduto);
        }
    }
}
