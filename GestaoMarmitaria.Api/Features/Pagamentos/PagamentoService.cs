using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GestaoMarmitaria.Api.Features.Pagamentos;

public interface IPagamentoService
{
    Task<PagamentoRespostaDto?> ObterPagamentoAsync(
        int pedidoId,
        CancellationToken cancellationToken = default);

    Task<RegistroPagamentoResultado> RegistrarPagamentoAsync(
        int pedidoId,
        RegistrarPagamentoDto dto,
        int? usuarioId,
        CancellationToken cancellationToken = default);

    Task<ReembolsoRespostaDto?> ObterReembolsoAsync(
        int pedidoId,
        CancellationToken cancellationToken = default);

    Task<RegistroReembolsoResultado> RegistrarReembolsoAsync(
        int pedidoId,
        int? usuarioId,
        CancellationToken cancellationToken = default);
}

public enum SituacaoRegistroPagamento
{
    Criado,
    PedidoNaoEncontrado,
    Conflito
}

public sealed record RegistroPagamentoResultado(
    SituacaoRegistroPagamento Situacao,
    PagamentoRespostaDto? Pagamento = null,
    string? Mensagem = null);

public enum SituacaoRegistroReembolso
{
    Criado,
    PedidoNaoEncontrado,
    Conflito
}

public sealed record RegistroReembolsoResultado(
    SituacaoRegistroReembolso Situacao,
    ReembolsoRespostaDto? Reembolso = null,
    string? Mensagem = null);

public class PagamentoService : IPagamentoService
{
    private const string MensagemPagamentoJaRegistrado =
        "O pedido já possui pagamento registrado.";

    private const string MensagemPedidoCancelado =
        "Não é possível registrar pagamento para um pedido cancelado.";

    private const string MensagemPedidoNaoCancelado =
        "O reembolso só pode ser registrado para um pedido cancelado.";

    private const string MensagemPedidoSemPagamento =
        "O pedido não possui pagamento registrado para reembolso.";

    private const string MensagemReembolsoJaRegistrado =
        "O pagamento já possui reembolso registrado.";

    private const string MensagemConflitoRegistroPagamento =
        "Ocorreu um conflito de concorrência ao registrar o pagamento. Tente novamente.";

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PagamentoService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PagamentoRespostaDto?> ObterPagamentoAsync(
        int pedidoId,
        CancellationToken cancellationToken = default)
    {
        var pagamento = await _context.Pagamentos
            .AsNoTracking()
            .Include(p => p.UsuarioRegistro)
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId, cancellationToken);

        return pagamento is null
            ? null
            : MapearPagamentoParaResposta(pagamento, pagamento.UsuarioRegistro?.Nome);
    }

    public async Task<RegistroPagamentoResultado> RegistrarPagamentoAsync(
        int pedidoId,
        RegistrarPagamentoDto dto,
        int? usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Serializable cobre a corrida entre pagamento e cancelamento: o pedido
        // não pode ser pago e cancelado ao mesmo tempo.
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            transacao => RegistrarPagamentoComTransacaoAsync(
                pedidoId,
                dto,
                usuarioId,
                transacao,
                cancellationToken),
            () => new RegistroPagamentoResultado(
                SituacaoRegistroPagamento.Conflito,
                Mensagem: MensagemConflitoRegistroPagamento),
            cancellationToken);
    }

    public async Task<ReembolsoRespostaDto?> ObterReembolsoAsync(
        int pedidoId,
        CancellationToken cancellationToken = default)
    {
        var reembolso = await _context.Reembolsos
            .AsNoTracking()
            .Include(r => r.UsuarioRegistro)
            .FirstOrDefaultAsync(r => r.Pagamento.PedidoId == pedidoId, cancellationToken);

        return reembolso is null
            ? null
            : MapearReembolsoParaResposta(reembolso, reembolso.UsuarioRegistro?.Nome);
    }

    public async Task<RegistroReembolsoResultado> RegistrarReembolsoAsync(
        int pedidoId,
        int? usuarioId,
        CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarPedidoAsync(pedidoId, cancellationToken);

        if (pedido is null)
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.PedidoNaoEncontrado);
        }

        if (pedido.Status != StatusPedido.Cancelado)
        {
            return ConflitoReembolso(MensagemPedidoNaoCancelado);
        }

        var pagamento = await CarregarPagamentoDoPedidoAsync(pedidoId, cancellationToken);

        if (pagamento is null)
        {
            return ConflitoReembolso(MensagemPedidoSemPagamento);
        }

        var jaPossuiReembolso = await JaPossuiReembolsoAsync(
            pagamento.Id,
            cancellationToken);

        if (jaPossuiReembolso)
        {
            return ConflitoReembolso(MensagemReembolsoJaRegistrado);
        }

        var reembolso = MontarReembolso(
            pagamento,
            usuarioId,
            _timeProvider.GetUtcNow().UtcDateTime);

        var gravado = await GravarReembolsoAsync(reembolso, cancellationToken);

        if (!gravado)
        {
            return ConflitoReembolso(MensagemReembolsoJaRegistrado);
        }

        return new RegistroReembolsoResultado(
            SituacaoRegistroReembolso.Criado,
            await MapearReembolsoCriadoAsync(reembolso, usuarioId, cancellationToken));
    }

    private async Task<RegistroPagamentoResultado> RegistrarPagamentoComTransacaoAsync(
        int pedidoId,
        RegistrarPagamentoDto dto,
        int? usuarioId,
        IDbContextTransaction transacao,
        CancellationToken cancellationToken)
    {
        var pedido = await CarregarPedidoComItensAsync(pedidoId, cancellationToken);

        if (pedido is null)
        {
            return new RegistroPagamentoResultado(
                SituacaoRegistroPagamento.PedidoNaoEncontrado);
        }

        // Pré-checagem para responder rápido; o índice único cobre a corrida entre requests.
        var jaPossuiPagamento = await JaPossuiPagamentoAsync(pedidoId, cancellationToken);

        if (jaPossuiPagamento)
        {
            return ConflitoPagamento(MensagemPagamentoJaRegistrado);
        }

        if (pedido.Status == StatusPedido.Cancelado)
        {
            return ConflitoPagamento(MensagemPedidoCancelado);
        }

        var pagamento = MontarPagamento(
            pedido,
            dto.FormaPagamento,
            usuarioId,
            _timeProvider.GetUtcNow().UtcDateTime);

        var gravado = await GravarPagamentoAsync(pagamento, cancellationToken);

        if (!gravado)
        {
            return ConflitoPagamento(MensagemPagamentoJaRegistrado);
        }

        await transacao.CommitAsync(cancellationToken);

        return new RegistroPagamentoResultado(
            SituacaoRegistroPagamento.Criado,
            await MapearPagamentoCriadoAsync(pagamento, usuarioId, cancellationToken));
    }

    private Task<Pedido?> CarregarPedidoComItensAsync(
        int pedidoId,
        CancellationToken cancellationToken)
    {
        return _context.Pedidos
            .AsNoTracking()
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);
    }

    private Task<Pedido?> CarregarPedidoAsync(
        int pedidoId,
        CancellationToken cancellationToken)
    {
        return _context.Pedidos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);
    }

    private Task<Pagamento?> CarregarPagamentoDoPedidoAsync(
        int pedidoId,
        CancellationToken cancellationToken)
    {
        return _context.Pagamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId, cancellationToken);
    }

    private Task<bool> JaPossuiPagamentoAsync(
        int pedidoId,
        CancellationToken cancellationToken)
    {
        return _context.Pagamentos
            .AnyAsync(p => p.PedidoId == pedidoId, cancellationToken);
    }

    private Task<bool> JaPossuiReembolsoAsync(
        int pagamentoId,
        CancellationToken cancellationToken)
    {
        return _context.Reembolsos
            .AnyAsync(r => r.PagamentoId == pagamentoId, cancellationToken);
    }

    /// <summary>
    /// Grava e trata a violação do índice único: dois requests simultâneos podem
    /// passar pela pré-checagem e só o banco impede o pagamento duplicado.
    /// </summary>
    private async Task<bool> GravarPagamentoAsync(
        Pagamento pagamento,
        CancellationToken cancellationToken)
    {
        _context.Pagamentos.Add(pagamento);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(
                ex,
                IndicesUnicos.PagamentoPorPedido))
        {
            return false;
        }
    }

    /// <summary>
    /// Mesma proteção do pagamento: a checagem prévia responde rápido, mas a
    /// corrida entre requests é decidida pelo índice único.
    /// </summary>
    private async Task<bool> GravarReembolsoAsync(
        Reembolso reembolso,
        CancellationToken cancellationToken)
    {
        _context.Reembolsos.Add(reembolso);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(
                ex,
                IndicesUnicos.ReembolsoPorPagamento))
        {
            return false;
        }
    }

    private async Task<PagamentoRespostaDto> MapearPagamentoCriadoAsync(
        Pagamento pagamento,
        int? usuarioId,
        CancellationToken cancellationToken)
    {
        var usuarioNome = await ObterNomeUsuarioAsync(usuarioId, cancellationToken);

        return MapearPagamentoParaResposta(pagamento, usuarioNome);
    }

    private async Task<ReembolsoRespostaDto> MapearReembolsoCriadoAsync(
        Reembolso reembolso,
        int? usuarioId,
        CancellationToken cancellationToken)
    {
        var usuarioNome = await ObterNomeUsuarioAsync(usuarioId, cancellationToken);

        return MapearReembolsoParaResposta(reembolso, usuarioNome);
    }

    private static RegistroPagamentoResultado ConflitoPagamento(string mensagem)
    {
        return new RegistroPagamentoResultado(
            SituacaoRegistroPagamento.Conflito,
            Mensagem: mensagem);
    }

    private static RegistroReembolsoResultado ConflitoReembolso(string mensagem)
    {
        return new RegistroReembolsoResultado(
            SituacaoRegistroReembolso.Conflito,
            Mensagem: mensagem);
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
            Valor = pedido.CalcularTotal(),
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

    private async Task<string?> ObterNomeUsuarioAsync(
        int? usuarioId,
        CancellationToken cancellationToken)
    {
        if (usuarioId is null) return null;

        return await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync(cancellationToken);
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
}
