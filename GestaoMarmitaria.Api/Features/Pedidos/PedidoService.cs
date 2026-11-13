using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Features.Compartilhado;
using GestaoMarmitaria.Api.Infra.Configurations;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

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

        var pedidos = await Paginacao.BuscarPaginaAsync(
            consulta
                .OrderByDescending(p => p.DataCriacao)
                .ThenByDescending(p => p.Id),
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

                var validacaoProdutos = await ValidarProdutosDoPedidoAsync(
                    produtosIds,
                    cancellationToken);

                if (!validacaoProdutos.EhValida)
                {
                    return new CriacaoPedidoResultado(
                        SituacaoCriacaoPedido.Validacao,
                        Mensagem: validacaoProdutos.Mensagem);
                }

                var pedido = MontarPedido(dto, validacaoProdutos.ProdutosPorId!);

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
        // A transição é validada com o status lido: Serializable impede que dois
        // requests simultâneos validem o mesmo status e avancem o pedido duas vezes.
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var pedido = await _context.Pedidos
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

                if (pedido is null)
                {
                    return new AlteracaoStatusResultado(
                        SituacaoAlteracaoStatus.PedidoNaoEncontrado);
                }

                var transicaoPermitida = RegrasStatusPedido.PodeAlterarStatus(
                    pedido.Status,
                    pedido.TipoEntrega,
                    dto.Status);

                if (!transicaoPermitida)
                {
                    return new AlteracaoStatusResultado(
                        SituacaoAlteracaoStatus.TransicaoInvalida,
                        "A transição de status solicitada não é permitida.");
                }

                var statusAnterior = pedido.Status;
                pedido.Status = dto.Status;

                _context.HistoricosStatusPedido.Add(new HistoricoStatusPedido
                {
                    PedidoId = pedido.Id,
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
            // A data final é inclusiva para o cliente, mas o filtro SQL é exclusivo:
            // soma-se um dia inteiro para abranger tudo que foi criado nessa data.
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
            consulta = filtros.ReembolsoPendente.Value
                ? consulta.Where(TemReembolsoPendente())
                : consulta.Where(NaoTemReembolsoPendente());
        }

        return consulta;
    }

    private static Expression<Func<Pedido, bool>> TemReembolsoPendente()
    {
        return p => p.Status == StatusPedido.Cancelado
            && p.Pagamento != null
            && p.Pagamento.Reembolso == null;
    }

    private static Expression<Func<Pedido, bool>> NaoTemReembolsoPendente()
    {
        return p => p.Status != StatusPedido.Cancelado
            || p.Pagamento == null
            || p.Pagamento.Reembolso != null;
    }

    private Task<bool> ClienteExisteAsync(
        int clienteId,
        CancellationToken cancellationToken)
    {
        return _context.Clientes.AnyAsync(c => c.Id == clienteId, cancellationToken);
    }

    private sealed record ValidacaoProdutosPedido(
        Dictionary<int, Produto>? ProdutosPorId,
        string? Mensagem)
    {
        public bool EhValida => ProdutosPorId is not null;
    }

    private async Task<ValidacaoProdutosPedido> ValidarProdutosDoPedidoAsync(
        List<int> produtosIds,
        CancellationToken cancellationToken)
    {
        var produtos = await CarregarProdutosAsync(produtosIds, cancellationToken);

        if (produtos.Count != produtosIds.Count)
        {
            return new ValidacaoProdutosPedido(
                null,
                "Um ou mais produtos informados não existem.");
        }

        if (produtos.Any(p => !p.Ativo))
        {
            return new ValidacaoProdutosPedido(
                null,
                "O pedido contém produtos inativos.");
        }

        var marmitasIds = produtos
            .Where(p => p.Tipo == TipoProduto.Marmita)
            .Select(p => p.Id)
            .ToList();

        var marmitasDisponiveis = await TodasAsMarmitasDisponiveisAsync(
            marmitasIds,
            cancellationToken);

        if (!marmitasDisponiveis)
        {
            return new ValidacaoProdutosPedido(
                null,
                "O pedido contém marmitas indisponíveis no cardápio de hoje.");
        }

        return new ValidacaoProdutosPedido(
            produtos.ToDictionary(p => p.Id),
            null);
    }

    private Task<List<Produto>> CarregarProdutosAsync(
        List<int> produtosIds,
        CancellationToken cancellationToken)
    {
        return _context.Produtos
            .AsNoTracking()
            .Where(p => produtosIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> TodasAsMarmitasDisponiveisAsync(
        List<int> marmitasIds,
        CancellationToken cancellationToken)
    {
        if (marmitasIds.Count == 0)
        {
            return true;
        }

        var dataCardapio = FusoBrasilia.Hoje(_timeProvider);

        // Uma única consulta para todas as marmitas do pedido.
        var quantidadeDisponivel = await _context.ItensCardapio
            .CountAsync(i =>
                i.Data == dataCardapio
                && i.Disponivel
                && marmitasIds.Contains(i.ProdutoId),
                cancellationToken);

        return quantidadeDisponivel == marmitasIds.Count;
    }

    private Pedido MontarPedido(CriarPedidoDto dto, Dictionary<int, Produto> produtosPorId)
    {
        var ehEntrega = dto.TipoEntrega == TipoEntrega.Entrega;

        // Endereço e taxa entram no pedido como cópia histórica: mudanças futuras
        // no cadastro do cliente não podem reescrever o que foi combinado.
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
        // Nome e preço são copiados do catálogo no momento do pedido; alterações
        // posteriores no produto não recalculam o histórico.
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
