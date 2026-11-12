using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Features.Pagamentos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Models;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace GestaoMarmitaria.Api.Features.Pedidos
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PedidosController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly TimeProvider _timeProvider;

        private readonly decimal _taxaEntrega;

        public PedidosController(
            AppDbContext context,
            IOptions<EntregaSettings> entregaSettings,
            TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
            _taxaEntrega = entregaSettings.Value.Taxa!.Value;
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
        public async Task<IActionResult> CriarPedidoAsync(CriarPedidoDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
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
                await transaction.CommitAsync();

                var resposta = MapearPedidoParaResposta(pedido);

                return CreatedAtAction(
                    nameof(GetPedidoPorId),
                    new { id = pedido.Id },
                    resposta);
            }
            catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
            {
                return Conflict(new { mensagem = "Ocorreu um conflito de concorrência ao processar o pedido. Tente novamente." });
            }
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> AtualizarStatusPedido(int id, AtualizarStatusPedidoDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
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

                var statusAnterior = pedidoExistente.Status;
                pedidoExistente.Status = dto.Status;

                _context.HistoricosStatusPedido.Add(new HistoricoStatusPedido
                {
                    PedidoId = pedidoExistente.Id,
                    StatusAnterior = statusAnterior,
                    StatusNovo = dto.Status,
                    UsuarioId = ObterUsuarioId(),
                    DataAlteracao = _timeProvider.GetUtcNow().UtcDateTime
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
            {
                return Conflict(new { mensagem = "Ocorreu um conflito de concorrência ao atualizar o pedido. Tente novamente." });
            }
        }

        [HttpGet("{id:int}/historico")]
        [ActionName(nameof(GetHistoricoPorPedidoAsync))]
        public async Task<IActionResult> GetHistoricoPorPedidoAsync(int id)
        {
            var pedidoExiste = await _context.Pedidos
                .AsNoTracking()
                .AnyAsync(p => p.Id == id);

            if (!pedidoExiste) return NotFound();

            var historico = await _context.HistoricosStatusPedido
                .AsNoTracking()
                .Where(h => h.PedidoId == id)
                .OrderBy(h => h.DataAlteracao)
                .ThenBy(h => h.Id)
                .Select(h => new HistoricoStatusPedidoRespostaDto
                {
                    Id = h.Id,
                    StatusAnterior = h.StatusAnterior,
                    StatusNovo = h.StatusNovo,
                    UsuarioId = h.UsuarioId,
                    UsuarioNome = h.Usuario != null ? h.Usuario.Nome : null,
                    DataAlteracao = h.DataAlteracao
                })
                .ToListAsync();

            return Ok(historico);
        }

        [HttpGet("{id:int}/pagamento")]
        [ActionName(nameof(GetPagamentoPorPedidoAsync))]
        public async Task<IActionResult> GetPagamentoPorPedidoAsync(int id)
        {
            var pagamento = await _context.Pagamentos
                .AsNoTracking()
                .Include(p => p.UsuarioRegistro)
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pagamento is null) return NotFound();

            var resposta = MapearPagamentoParaResposta(pagamento, pagamento.UsuarioRegistro?.Nome);

            return Ok(resposta);
        }

        [HttpGet("{id:int}/reembolso")]
        [ActionName(nameof(GetReembolsoPorPedidoAsync))]
        public async Task<IActionResult> GetReembolsoPorPedidoAsync(int id)
        {
            var reembolso = await _context.Reembolsos
                .AsNoTracking()
                .Include(r => r.UsuarioRegistro)
                .FirstOrDefaultAsync(r => r.Pagamento.PedidoId == id);

            if (reembolso is null) return NotFound();

            var resposta = MapearReembolsoParaResposta(reembolso, reembolso.UsuarioRegistro?.Nome);

            return Ok(resposta);
        }

        [HttpPost("{id:int}/pagamento")]
        public async Task<IActionResult> RegistrarPagamentoAsync(int id, RegistrarPagamentoDto dto)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var pedido = await _context.Pedidos
                    .AsNoTracking()
                    .Include(p => p.Itens)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (pedido is null) return NotFound();

                var pagamentoExistente = await _context.Pagamentos
                    .AnyAsync(p => p.PedidoId == id);

                if (pagamentoExistente)
                {
                    return Conflict(new
                    {
                        mensagem = "O pedido já possui pagamento registrado."
                    });
                }

                if (pedido.Status == StatusPedido.Cancelado)
                {
                    return Conflict(new
                    {
                        mensagem = "Não é possível registrar pagamento para um pedido cancelado."
                    });
                }

                var usuarioId = ObterUsuarioId();
                var pagamento = MontarPagamento(
                    pedido,
                    dto.FormaPagamento,
                    usuarioId,
                    _timeProvider.GetUtcNow().UtcDateTime);
                _context.Pagamentos.Add(pagamento);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var resposta = MapearPagamentoParaResposta(
                    pagamento,
                    await ObterNomeUsuarioAsync(usuarioId));

                return CreatedAtAction(
                    nameof(GetPagamentoPorPedidoAsync),
                    new { id = pagamento.PedidoId },
                    resposta);
            }
            catch (DbUpdateException ex) when (
                FalhasBanco.EhViolacaoDeIndiceUnico(ex, "IX_Pagamentos_PedidoId"))
            {
                return Conflict(new
                {
                    mensagem = "O pedido já possui pagamento registrado."
                });
            }
            catch (Exception ex) when (FalhasBanco.EhFalhaDeSerializacao(ex))
            {
                return Conflict(new
                {
                    mensagem = "Ocorreu um conflito de concorrência ao registrar o pagamento. Tente novamente."
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id:int}/reembolso")]
        public async Task<IActionResult> RegistrarReembolsoAsync(int id)
        {
            var pedido = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido is null) return NotFound();

            if (pedido.Status != StatusPedido.Cancelado)
            {
                return Conflict(new
                {
                    mensagem = "O reembolso só pode ser registrado para um pedido cancelado."
                });
            }

            var pagamento = await _context.Pagamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PedidoId == id);

            if (pagamento is null)
            {
                return Conflict(new
                {
                    mensagem = "O pedido não possui pagamento registrado para reembolso."
                });
            }

            var reembolsoExistente = await _context.Reembolsos
                .AnyAsync(r => r.PagamentoId == pagamento.Id);

            if (reembolsoExistente)
            {
                return Conflict(new
                {
                    mensagem = "O pagamento já possui reembolso registrado."
                });
            }

            var usuarioId = ObterUsuarioId();
            var reembolso = MontarReembolso(
                pagamento,
                usuarioId,
                _timeProvider.GetUtcNow().UtcDateTime);
            _context.Reembolsos.Add(reembolso);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                FalhasBanco.EhViolacaoDeIndiceUnico(ex, "IX_Reembolsos_PagamentoId"))
            {
                return Conflict(new
                {
                    mensagem = "O pagamento já possui reembolso registrado."
                });
            }

            var resposta = MapearReembolsoParaResposta(
                reembolso,
                await ObterNomeUsuarioAsync(usuarioId));

            return CreatedAtAction(
                nameof(GetReembolsoPorPedidoAsync),
                new { id },
                resposta);
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
                var inicioUtc = FusoBrasilia.ParaUtc(filtros.DataInicial.Value);
                consulta = consulta.Where(p => p.DataCriacao >= inicioUtc);
            }

            if (filtros.DataFinal.HasValue)
            {
                var fimUtcExclusivo = FusoBrasilia.ParaUtc(filtros.DataFinal.Value.AddDays(1));
                consulta = consulta.Where(p => p.DataCriacao < fimUtcExclusivo);
            }

            if (filtros.Pago.HasValue)
            {
                consulta = filtros.Pago.Value
                    ? consulta.Where(p => p.Pagamento != null)
                    : consulta.Where(p => p.Pagamento == null);
            }

            if (filtros.ReembolsoPendente.HasValue)
            {
                if (filtros.ReembolsoPendente.Value)
                {
                    consulta = consulta.Where(p =>
                        p.Status == StatusPedido.Cancelado
                        && p.Pagamento != null
                        && p.Pagamento.Reembolso == null);
                }
                else
                {
                    consulta = consulta.Where(p =>
                        p.Status != StatusPedido.Cancelado
                        || p.Pagamento == null
                        || p.Pagamento.Reembolso != null);
                }
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

            var marmitasIds = produtos
                .Where(p => p.Tipo == TipoProduto.Marmita)
                .Select(p => p.Id)
                .ToList();

            if (marmitasIds.Count > 0)
            {
                var dataCardapio = FusoBrasilia.Hoje(_timeProvider);

                var quantidadeDisponivel = await _context.ItensCardapio
                    .CountAsync(i =>
                        i.Data == dataCardapio
                        && i.Disponivel
                        && marmitasIds.Contains(i.ProdutoId));

                if (quantidadeDisponivel != marmitasIds.Count)
                {
                    return (null, "O pedido contém marmitas indisponíveis no cardápio de hoje.");
                }
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
                Observacoes = dto.Observacoes?.Trim(),
                DataCriacao = _timeProvider.GetUtcNow().UtcDateTime
            };

            foreach (var itemDto in dto.Itens)
            {
                var produto = produtosPorId[itemDto.ProdutoId];
                pedido.Itens.Add(CriarItemPedido(produto, itemDto.Quantidade));
            }

            return pedido;
        }

        private static decimal CalcularTotalPedido(Pedido pedido)
        {
            return pedido.Itens.Sum(i => i.Quantidade * i.PrecoUnitario)
                + pedido.TaxaEntrega;
        }

        private static Pagamento MontarPagamento(
            Pedido pedido,
            FormaPagamento formaPagamento,
            int? usuarioId,
            DateTime agora)
        {
            return new Pagamento
            {
                PedidoId = pedido.Id,
                FormaPagamento = formaPagamento,
                Valor = CalcularTotalPedido(pedido),
                UsuarioRegistroId = usuarioId,
                DataPagamento = agora,
                DataRegistro = agora
            };
        }

        private static Reembolso MontarReembolso(
            Pagamento pagamento,
            int? usuarioId,
            DateTime agora)
        {
            return new Reembolso
            {
                PagamentoId = pagamento.Id,
                Valor = pagamento.Valor,
                UsuarioRegistroId = usuarioId,
                DataReembolso = agora,
                DataRegistro = agora
            };
        }

        private int? ObterUsuarioId()
        {
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(valor, out var usuarioId) ? usuarioId : null;
        }

        private async Task<string?> ObterNomeUsuarioAsync(int? usuarioId)
        {
            if (usuarioId is null) return null;

            return await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.Nome)
                .FirstOrDefaultAsync();
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

        private static PagamentoRespostaDto MapearPagamentoParaResposta(
            Pagamento pagamento,
            string? usuarioNome)
        {
            return new PagamentoRespostaDto
            {
                Id = pagamento.Id,
                PedidoId = pagamento.PedidoId,
                FormaPagamento = pagamento.FormaPagamento,
                Valor = pagamento.Valor,
                DataPagamento = pagamento.DataPagamento,
                UsuarioRegistroId = pagamento.UsuarioRegistroId,
                UsuarioRegistroNome = usuarioNome,
                DataRegistro = pagamento.DataRegistro
            };
        }

        private static ReembolsoRespostaDto MapearReembolsoParaResposta(
            Reembolso reembolso,
            string? usuarioNome)
        {
            return new ReembolsoRespostaDto
            {
                Id = reembolso.Id,
                PagamentoId = reembolso.PagamentoId,
                Valor = reembolso.Valor,
                DataReembolso = reembolso.DataReembolso,
                UsuarioRegistroId = reembolso.UsuarioRegistroId,
                UsuarioRegistroNome = usuarioNome,
                DataRegistro = reembolso.DataRegistro
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
