using GestaoMarmitaria.Api.Data;
using GestaoMarmitaria.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using GestaoMarmitaria.Api.Regras;

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
            if (filtros.DataFinal == DateOnly.MaxValue)
            {
                return BadRequest(new { mensagem = "A data final deve ser anterior a 31/12/9999." });
            }

            var consulta = AplicarFiltros(
                _context.Pedidos.AsNoTracking().Include(p => p.Itens),
                filtros);

            var totalRegistros = await consulta.CountAsync();

            var pedidos = await BuscarPaginaAsync(consulta, filtros, totalRegistros);

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
            if (!await ClienteExisteAsync(dto.ClienteId))
            {
                return BadRequest(new { mensagem = "O cliente informado não existe." });
            }

            var produtosIds = dto.Itens
                .Select(i => i.ProdutoId)
                .ToList();

            var (produtosPorId, erro) = await CarregarProdutosDoPedidoAsync(produtosIds);

            if (erro is not null)
            {
                return BadRequest(new { mensagem = erro });
            }

            var pedido = MontarPedido(dto, produtosPorId!);

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

            var transicaoPermitida = RegrasStatusPedido.PodeAlterarStatus(
                pedidoExistente.Status,
                pedidoExistente.TipoEntrega,
                dto.Status);

            if (!transicaoPermitida)
            {
                return Conflict(new { mensagem = "A transição de status solicitada não é permitida." });
            }

            pedidoExistente.Status = dto.Status;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static IQueryable<Pedido> AplicarFiltros(IQueryable<Pedido> consulta, ConsultarPedidosDto filtros)
        {
            if (filtros.Status.HasValue)
            {
                consulta = consulta.Where(p => p.Status == filtros.Status.Value);
            }

            if (filtros.ClienteId.HasValue)
            {
                consulta = consulta.Where(p => p.ClienteId == filtros.ClienteId.Value);
            }

            if (filtros.DataInicial.HasValue)
            {
                var inicioUtc = ConverterInicioDoDiaParaUtc(filtros.DataInicial.Value);
                consulta = consulta.Where(p => p.DataCriacao >= inicioUtc);
            }

            if (filtros.DataFinal.HasValue)
            {
                var fimUtcExclusivo = ConverterInicioDoDiaParaUtc(filtros.DataFinal.Value.AddDays(1));
                consulta = consulta.Where(p => p.DataCriacao < fimUtcExclusivo);
            }

            return consulta;
        }

        private static async Task<List<Pedido>> BuscarPaginaAsync(
            IQueryable<Pedido> consulta,
            ConsultarPedidosDto filtros,
            int totalRegistros)
        {
            var quantidadeIgnorada = (long)(filtros.Pagina - 1) * filtros.TamanhoPagina;

            if (quantidadeIgnorada >= totalRegistros)
            {
                return new List<Pedido>();
            }

            return await consulta
                .OrderByDescending(p => p.DataCriacao)
                .ThenByDescending(p => p.Id)
                .Skip((int)quantidadeIgnorada)
                .Take(filtros.TamanhoPagina)
                .ToListAsync();
        }

        private static DateTime ConverterInicioDoDiaParaUtc(DateOnly data)
        {
            var fusoBrasilia = TimeZoneInfo.FindSystemTimeZoneById(
                "America/Sao_Paulo");

            var inicioLocal = data.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

            while (fusoBrasilia.IsInvalidTime(inicioLocal))
            {
                inicioLocal = inicioLocal.AddMinutes(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, fusoBrasilia);
        }

        private Task<bool> ClienteExisteAsync(int clienteId)
        {
            return _context.Clientes.AnyAsync(c => c.Id == clienteId);
        }

        private async Task<(Dictionary<int, Produto>? Produtos, string? Erro)> CarregarProdutosDoPedidoAsync(
            List<int> produtosIds)
        {
            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => produtosIds.Contains(p.Id))
                .ToListAsync();

            if (produtos.Count != produtosIds.Count)
            {
                return (null, "Um ou mais produtos informados não existem.");
            }

            if (produtos.Any(p => !p.Ativo))
            {
                return (null, "O pedido contém produtos inativos.");
            }

            return (produtos.ToDictionary(p => p.Id), null);
        }

        private Pedido MontarPedido(CriarPedidoDto dto, Dictionary<int, Produto> produtosPorId)
        {
            var ehEntrega = dto.TipoEntrega == TipoEntrega.Entrega;

            var pedido = new Pedido
            {
                ClienteId = dto.ClienteId,
                TipoEntrega = dto.TipoEntrega,
                EnderecoEntrega = ehEntrega ? dto.EnderecoEntrega!.Trim() : null,
                TaxaEntrega = ehEntrega ? _taxaEntrega : 0.00m,
                Observacoes = dto.Observacoes?.Trim()
            };

            foreach (var itemDto in dto.Itens)
            {
                var produto = produtosPorId[itemDto.ProdutoId];
                pedido.Itens.Add(CriarItemPedido(produto, itemDto.Quantidade));
            }

            return pedido;
        }

        private static ItemPedido CriarItemPedido(Produto produto, int quantidade)
        {
            return new ItemPedido
            {
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                Quantidade = quantidade,
                PrecoUnitario = produto.Preco
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
