using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestaoMarmitaria.Api.Features.Pedidos;

public interface IPedidoService
{
    Task<PedidoPaginadoResultado> ConsultarAsync(
        ConsultarPedidosDto filtros,
        CancellationToken cancellationToken = default);

    Task<PedidoRespostaDto?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoricoStatusPedidoRespostaDto>?> ObterHistoricoAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<CriacaoPedidoResultado> CriarAsync(
        CriarPedidoDto dto,
        CancellationToken cancellationToken = default);

    Task<AlteracaoStatusResultado> AlterarStatusAsync(
        int id,
        AtualizarStatusPedidoDto dto,
        int? usuarioId,
        CancellationToken cancellationToken = default);
}

public sealed record PedidoPaginadoResultado(
    PedidosPaginadosRespostaDto? Resposta,
    string? Erro);

public enum SituacaoCriacaoPedido
{
    Criado,
    Validacao,
    ConflitoConcorrencia
}

public sealed record CriacaoPedidoResultado(
    SituacaoCriacaoPedido Situacao,
    PedidoRespostaDto? Pedido = null,
    string? Mensagem = null);

public enum SituacaoAlteracaoStatus
{
    Alterado,
    PedidoNaoEncontrado,
    TransicaoInvalida,
    ConflitoConcorrencia
}

public sealed record AlteracaoStatusResultado(
    SituacaoAlteracaoStatus Situacao,
    string? Mensagem = null);

public class PedidoService : IPedidoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly decimal _taxaEntrega;

    public PedidoService(
        AppDbContext context,
        IOptions<EntregaSettings> entregaSettings,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
        _taxaEntrega = entregaSettings.Value.Taxa!.Value;
    }

    public async Task<PedidoPaginadoResultado> ConsultarAsync(
        ConsultarPedidosDto filtros,
        CancellationToken cancellationToken = default)
    {
        if (filtros.DataFinal == DateOnly.MaxValue)
        {
            return new PedidoPaginadoResultado(
                null,
                "A data final deve ser anterior a 31/12/9999.");
        }

        var consulta = AplicarFiltros(
            _context.Pedidos.AsNoTracking().Include(p => p.Itens),
            filtros);

        var totalRegistros = await consulta.CountAsync(cancellationToken);

        var pedidos = await BuscarPaginaAsync(
            consulta,
            filtros,
            totalRegistros,
            cancellationToken);

        return new PedidoPaginadoResultado(
            new PedidosPaginadosRespostaDto
            {
                Itens = pedidos.Select(MapearPedidoParaResposta).ToList(),
                Pagina = filtros.Pagina,
                TamanhoPagina = filtros.TamanhoPagina,
                TotalRegistros = totalRegistros
            },
            null);
    }

    public async Task<PedidoRespostaDto?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var pedido = await _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return pedido is null ? null : MapearPedidoParaResposta(pedido);
    }

    public async Task<IReadOnlyList<HistoricoStatusPedidoRespostaDto>?> ObterHistoricoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var pedidoExiste = await _context.Pedidos
            .AsNoTracking()
            .AnyAsync(p => p.Id == id, cancellationToken);

        if (!pedidoExiste) return null;

        return await _context.HistoricosStatusPedido
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
            .ToListAsync(cancellationToken);
    }

    public async Task<CriacaoPedidoResultado> CriarAsync(
        CriarPedidoDto dto,
        CancellationToken cancellationToken = default)
    {
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                if (!await ClienteExisteAsync(dto.ClienteId, cancellationToken))
                {
                    return new CriacaoPedidoResultado(
                        SituacaoCriacaoPedido.Validacao,
                        Mensagem: "O cliente informado não existe.");
                }

                var produtosIds = dto.Itens
                    .Select(i => i.ProdutoId)
                    .ToList();

                var (produtosPorId, erro) = await CarregarProdutosDoPedidoAsync(
                    produtosIds,
                    cancellationToken);

                if (erro is not null)
                {
                    return new CriacaoPedidoResultado(
                        SituacaoCriacaoPedido.Validacao,
                        Mensagem: erro);
                }

                var pedido = MontarPedido(dto, produtosPorId!);

                _context.Pedidos.Add(pedido);
                await _context.SaveChangesAsync(cancellationToken);
                await transacao.CommitAsync(cancellationToken);

                return new CriacaoPedidoResultado(
                    SituacaoCriacaoPedido.Criado,
                    MapearPedidoParaResposta(pedido));
            },
            () => new CriacaoPedidoResultado(
                SituacaoCriacaoPedido.ConflitoConcorrencia,
                Mensagem: "Ocorreu um conflito de concorrência ao processar o pedido. Tente novamente."),
            cancellationToken);
    }

    public async Task<AlteracaoStatusResultado> AlterarStatusAsync(
        int id,
        AtualizarStatusPedidoDto dto,
        int? usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var pedidoExistente = await _context.Pedidos
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (pedidoExistente is null)
                {
                    return new AlteracaoStatusResultado(
                        SituacaoAlteracaoStatus.PedidoNaoEncontrado);
                }

                var transicaoPermitida = RegrasStatusPedido.PodeAlterarStatus(
                    pedidoExistente.Status,
                    pedidoExistente.TipoEntrega,
                    dto.Status);

                if (!transicaoPermitida)
                {
                    return new AlteracaoStatusResultado(
                        SituacaoAlteracaoStatus.TransicaoInvalida,
                        "A transição de status solicitada não é permitida.");
                }

                var statusAnterior = pedidoExistente.Status;
                pedidoExistente.Status = dto.Status;

                _context.HistoricosStatusPedido.Add(new HistoricoStatusPedido
                {
                    PedidoId = pedidoExistente.Id,
                    StatusAnterior = statusAnterior,
                    StatusNovo = dto.Status,
                    UsuarioId = usuarioId,
                    DataAlteracao = _timeProvider.GetUtcNow().UtcDateTime
                });

                await _context.SaveChangesAsync(cancellationToken);
                await transacao.CommitAsync(cancellationToken);

                return new AlteracaoStatusResultado(SituacaoAlteracaoStatus.Alterado);
            },
            () => new AlteracaoStatusResultado(
                SituacaoAlteracaoStatus.ConflitoConcorrencia,
                "Ocorreu um conflito de concorrência ao atualizar o pedido. Tente novamente."),
            cancellationToken);
    }

    private static IQueryable<Pedido> AplicarFiltros(
        IQueryable<Pedido> consulta,
        ConsultarPedidosDto filtros)
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
        int totalRegistros,
        CancellationToken cancellationToken)
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
            .ToListAsync(cancellationToken);
    }

    private Task<bool> ClienteExisteAsync(
        int clienteId,
        CancellationToken cancellationToken)
    {
        return _context.Clientes.AnyAsync(c => c.Id == clienteId, cancellationToken);
    }

    private async Task<(Dictionary<int, Produto>? Produtos, string? Erro)>
        CarregarProdutosDoPedidoAsync(
            List<int> produtosIds,
            CancellationToken cancellationToken)
    {
        var produtos = await _context.Produtos
            .AsNoTracking()
            .Where(p => produtosIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

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
                    && marmitasIds.Contains(i.ProdutoId),
                    cancellationToken);

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
