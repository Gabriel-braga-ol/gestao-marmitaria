using GestaoMarmitaria.Api.Data;
using GestaoMarmitaria.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;

namespace GestaoMarmitaria.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PedidosController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IConfiguration _configuration;

        private readonly decimal _taxaEntrega;

        public PedidosController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            var taxa = _configuration.GetValue<decimal?>("Entrega:Taxa");

            if (taxa is null || taxa < 0)
            {
                throw new InvalidOperationException("Configure Entrega:Taxa com um valor maior ou igual a zero.");
            }

            _taxaEntrega = taxa.Value;
        }

        [HttpGet]
        public async Task<IActionResult> GetPedidos([FromQuery] ConsultarPedidosDto filtros)
        {
            var consulta = _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Itens)
                .AsQueryable();

            if (filtros.Status.HasValue)
            {
                consulta = consulta.Where(p => p.Status == filtros.Status.Value);
            }

            if (filtros.ClienteId.HasValue)
            {
                consulta = consulta.Where(p => p.ClienteId == filtros.ClienteId.Value);
            }

            var totalRegistros = await consulta.CountAsync();

            var quantidadeIgnorada = (long)(filtros.Pagina - 1) * filtros.TamanhoPagina;

            var pedidos = new List<Pedido>();

            if (quantidadeIgnorada < totalRegistros)
            {
                pedidos = await consulta
                    .OrderByDescending(p => p.DataCriacao)
                    .ThenByDescending(p => p.Id)
                    .Skip((int)quantidadeIgnorada)
                    .Take(filtros.TamanhoPagina)
                    .ToListAsync();
            }

            var resposta = new PedidosPaginadosRespostaDto
            {
                Itens = pedidos.Select(p => MapearPedidoParaResposta(p)).ToList(),
                Pagina = filtros.Pagina,
                TamanhoPagina = filtros.TamanhoPagina,
                TotalRegistros = totalRegistros
            };

            return Ok(resposta);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPedidoPorId(int id)
        {
            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Itens)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido is null) return NotFound();

            var resposta = MapearPedidoParaResposta(pedido);

            return Ok(resposta);
        }

        [HttpPost]
        public async Task<IActionResult> CriarPedido(CriarPedidoDto dto)
        {
            var clienteExiste = await _context.Clientes
                .AnyAsync(c => c.Id == dto.ClienteId);

            if (!clienteExiste)
            {
                return BadRequest(new { mensagem = "O cliente informado não existe." });
            }

            var produtosIds = dto.Itens
                .Select(i => i.ProdutoId)
                .ToList();

            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => produtosIds.Contains(p.Id))
                .ToListAsync();

            if (produtos.Count != produtosIds.Count)
            {
                return BadRequest(new { mensagem = "Um ou mais produtos informados não existem." });
            }

            if (produtos.Any(p => !p.Ativo))
            {
                return BadRequest(new { mensagem = "O pedido contém produtos inativos." });
            }

            var produtosPorId = produtos.ToDictionary(p => p.Id);

            var pedido = new Pedido
            {
                ClienteId = dto.ClienteId,
                TipoEntrega = dto.TipoEntrega,
                EnderecoEntrega = dto.TipoEntrega == TipoEntrega.Entrega
                    ? dto.EnderecoEntrega!.Trim()
                    : null,
                TaxaEntrega = dto.TipoEntrega == TipoEntrega.Entrega ? _taxaEntrega : 0.00m,
                Observacoes = dto.Observacoes?.Trim()
            };

            foreach (var itemDto in dto.Itens)
            {
                var produto = produtosPorId[itemDto.ProdutoId];

                var itemPedido = new ItemPedido
                {
                    ProdutoId = produto.Id,
                    NomeProduto = produto.Nome,
                    Quantidade = itemDto.Quantidade,
                    PrecoUnitario = produto.Preco
                };

                pedido.Itens.Add(itemPedido);
            }

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            var resposta = MapearPedidoParaResposta(pedido);

            return CreatedAtAction(
                nameof(GetPedidoPorId),
                new { id = pedido.Id },
                resposta);
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> AtualizarStatusPedido(int id, AtualizarStatusPedidoDto dto)
        {
            var pedidoExistente = await _context.Pedidos.FirstOrDefaultAsync(p => p.Id == id);

            if (pedidoExistente is null) return NotFound();

            var transicaoPermitida = PodeAlterarStatus(pedidoExistente, dto.Status);

            if (!transicaoPermitida)
            {
                return Conflict(new { mensagem = "A transição de status solicitada não é permitida." });
            }

            pedidoExistente.Status = dto.Status;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static bool PodeAlterarStatus(Pedido pedido, StatusPedido novoStatus)
        {
            return pedido.Status switch
            {
                StatusPedido.Novo => novoStatus == StatusPedido.EmPreparo,

                StatusPedido.EmPreparo => novoStatus == StatusPedido.Pronto,

                StatusPedido.Pronto => pedido.TipoEntrega == TipoEntrega.Entrega
                    ? novoStatus == StatusPedido.SaiuParaEntrega
                    : novoStatus == StatusPedido.Entregue,
                StatusPedido.SaiuParaEntrega => novoStatus == StatusPedido.Entregue,

                _ => false
            };
        }

        private static PedidoRespostaDto MapearPedidoParaResposta(Pedido pedido)
        {
            return new PedidoRespostaDto
            {
                Id = pedido.Id,
                ClienteId = pedido.ClienteId,
                DataCriacao = pedido.DataCriacao,
                TipoEntrega = pedido.TipoEntrega,
                EnderecoEntrega = pedido.EnderecoEntrega,
                TaxaEntrega = pedido.TaxaEntrega,
                Observacoes = pedido.Observacoes,
                Status = pedido.Status,
                Itens = pedido.Itens.Select(i => new ItemPedidoRespostaDto
                {
                    Id = i.Id,
                    ProdutoId = i.ProdutoId,
                    NomeProduto = i.NomeProduto,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario
                }).ToList()
            };
        }
    }
}
