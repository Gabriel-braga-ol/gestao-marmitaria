using GestaoMarmitaria.Api.Models;
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
            Produto? produtoEncontrado = null;

            foreach (var produto in _produtos)
            {
                if (produto.Id == id)
                {
                    produtoEncontrado = produto;
                    break;
                }
            }

            if (produtoEncontrado == null) return NotFound();

            return Ok(produtoEncontrado);
        }
    }
}
