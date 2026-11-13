using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;

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
        return await ExecucaoTransacional.ExecutarSerializavelAsync(
            _context,
            async transacao =>
            {
                var pedido = await _context.Pedidos
                    .AsNoTracking()
                    .Include(p => p.Itens)
                    .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);

                if (pedido is null)
                {
                    return new RegistroPagamentoResultado(
                        SituacaoRegistroPagamento.PedidoNaoEncontrado);
                }

                var pagamentoExistente = await _context.Pagamentos
                    .AnyAsync(p => p.PedidoId == pedidoId, cancellationToken);

                if (pagamentoExistente)
                {
                    return new RegistroPagamentoResultado(
                        SituacaoRegistroPagamento.Conflito,
                        Mensagem: "O pedido já possui pagamento registrado.");
                }

                if (pedido.Status == StatusPedido.Cancelado)
                {
                    return new RegistroPagamentoResultado(
                        SituacaoRegistroPagamento.Conflito,
                        Mensagem: "Não é possível registrar pagamento para um pedido cancelado.");
                }

                var pagamento = MontarPagamento(
                    pedido,
                    dto.FormaPagamento,
                    usuarioId,
                    _timeProvider.GetUtcNow().UtcDateTime);

                _context.Pagamentos.Add(pagamento);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (
                    FalhasBanco.EhViolacaoDeIndiceUnico(
                        ex,
                        IndicesUnicos.PagamentoPorPedido))
                {
                    return new RegistroPagamentoResultado(
                        SituacaoRegistroPagamento.Conflito,
                        Mensagem: "O pedido já possui pagamento registrado.");
                }

                await transacao.CommitAsync(cancellationToken);

                var resposta = MapearPagamentoParaResposta(
                    pagamento,
                    await ObterNomeUsuarioAsync(usuarioId, cancellationToken));

                return new RegistroPagamentoResultado(
                    SituacaoRegistroPagamento.Criado,
                    resposta);
            },
            () => new RegistroPagamentoResultado(
                SituacaoRegistroPagamento.Conflito,
                Mensagem: "Ocorreu um conflito de concorrência ao registrar o pagamento. Tente novamente."),
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
        var pedido = await _context.Pedidos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);

        if (pedido is null)
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.PedidoNaoEncontrado);
        }

        if (pedido.Status != StatusPedido.Cancelado)
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.Conflito,
                Mensagem: "O reembolso só pode ser registrado para um pedido cancelado.");
        }

        var pagamento = await _context.Pagamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PedidoId == pedidoId, cancellationToken);

        if (pagamento is null)
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.Conflito,
                Mensagem: "O pedido não possui pagamento registrado para reembolso.");
        }

        var reembolsoExistente = await _context.Reembolsos
            .AnyAsync(r => r.PagamentoId == pagamento.Id, cancellationToken);

        if (reembolsoExistente)
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.Conflito,
                Mensagem: "O pagamento já possui reembolso registrado.");
        }

        var reembolso = MontarReembolso(
            pagamento,
            usuarioId,
            _timeProvider.GetUtcNow().UtcDateTime);

        _context.Reembolsos.Add(reembolso);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            FalhasBanco.EhViolacaoDeIndiceUnico(
                ex,
                IndicesUnicos.ReembolsoPorPagamento))
        {
            return new RegistroReembolsoResultado(
                SituacaoRegistroReembolso.Conflito,
                Mensagem: "O pagamento já possui reembolso registrado.");
        }

        var resposta = MapearReembolsoParaResposta(
            reembolso,
            await ObterNomeUsuarioAsync(usuarioId, cancellationToken));

        return new RegistroReembolsoResultado(
            SituacaoRegistroReembolso.Criado,
            resposta);
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
