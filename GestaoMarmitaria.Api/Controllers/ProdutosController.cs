using GestaoMarmitaria.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace GestaoMarmitaria.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase // fornece metodos necessários para gerenciar requisições e resposta Http
    {
        [HttpGet]
        public IActionResult GetProdutos()
        {
            var produtos = new List<Produto>
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

            return Ok(produtos);

        }
    }
}
