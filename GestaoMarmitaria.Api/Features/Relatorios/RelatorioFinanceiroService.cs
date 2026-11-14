using GestaoMarmitaria.Api.Enums;
using GestaoMarmitaria.Api.Infra.Data;
using GestaoMarmitaria.Api.Infra.Regras;
using GestaoMarmitaria.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public interface IRelatorioFinanceiroService
{
    Task<RelatorioFinanceiroResultado> ConsultarAsync(
        ConsultarRelatorioFinanceiroDto filtros,
        CancellationToken cancellationToken = default);
}

public sealed record RelatorioFinanceiroResultado(
    RelatorioFinanceiroRespostaDto? Relatorio,
    string? Erro);

public class RelatorioFinanceiroService : IRelatorioFinanceiroService
{
    private readonly AppDbContext _context;

    public RelatorioFinanceiroService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RelatorioFinanceiroResultado> ConsultarAsync(
        ConsultarRelatorioFinanceiroDto filtros,
        CancellationToken cancellationToken = default)
    {
        if (filtros.DataFinal == DateOnly.MaxValue)
        {
            return new RelatorioFinanceiroResultado(
                null,
                "A data final deve ser anterior a 31/12/9999.");
        }

        var inicioUtc = FusoBrasilia.ParaUtc(filtros.DataInicial!.Value);
        var fimUtcExclusivo = FusoBrasilia.ParaUtc(filtros.DataFinal!.Value.AddDays(1));

        // O período recorta pedidos pela data de criação (dias de Brasília), como na
        // listagem de pedidos; pagamentos e reembolsos são somados dentro desse recorte.
        var pedidos = _context.Pedidos
            .AsNoTracking()
            .Where(p => p.DataCriacao >= inicioUtc && p.DataCriacao < fimUtcExclusivo);

        var reembolsos = _context.Reembolsos
            .AsNoTracking()
            .Where(r => r.Pagamento.Pedido.DataCriacao >= inicioUtc
                && r.Pagamento.Pedido.DataCriacao < fimUtcExclusivo);

        var relatorio = new RelatorioFinanceiroRespostaDto
        {
            DataInicial = filtros.DataInicial!.Value,
            DataFinal = filtros.DataFinal!.Value,
            TotalPedidos = await ContarPedidosAsync(pedidos, cancellationToken),
            PedidosPagos = await ContarPedidosPagosAsync(pedidos, cancellationToken),
            PedidosReembolsados = await ContarReembolsosAsync(reembolsos, cancellationToken),
            TotalPago = await SomarPagamentosAsync(pedidos, cancellationToken),
            TotalReembolsado = await SomarReembolsosAsync(reembolsos, cancellationToken),
            TotalReembolsoPendente = await SomarReembolsoPendenteAsync(
                pedidos,
                cancellationToken),
            PorFormaPagamento = await AgruparPorFormaPagamentoAsync(
                pedidos,
                cancellationToken)
        };

        return new RelatorioFinanceiroResultado(relatorio, null);
    }

    private static Task<int> ContarPedidosAsync(
        IQueryable<Pedido> pedidos,
        CancellationToken cancellationToken)
    {
        return pedidos.CountAsync(cancellationToken);
    }

    private static Task<int> ContarPedidosPagosAsync(
        IQueryable<Pedido> pedidos,
        CancellationToken cancellationToken)
    {
        return pedidos.CountAsync(p => p.Pagamento != null, cancellationToken);
    }

    private static Task<int> ContarReembolsosAsync(
        IQueryable<Reembolso> reembolsos,
        CancellationToken cancellationToken)
    {
        return reembolsos.CountAsync(cancellationToken);
    }

    private static async Task<decimal> SomarPagamentosAsync(
        IQueryable<Pedido> pedidos,
        CancellationToken cancellationToken)
    {
        return await pedidos
            .SumAsync(p => (decimal?)p.Pagamento!.Valor, cancellationToken) ?? 0m;
    }

    private static async Task<decimal> SomarReembolsosAsync(
        IQueryable<Reembolso> reembolsos,
        CancellationToken cancellationToken)
    {
        return await reembolsos
            .SumAsync(r => (decimal?)r.Valor, cancellationToken) ?? 0m;
    }

    private static async Task<decimal> SomarReembolsoPendenteAsync(
        IQueryable<Pedido> pedidos,
        CancellationToken cancellationToken)
    {
        return await pedidos
            .Where(p => p.Status == StatusPedido.Cancelado
                && p.Pagamento != null
                && p.Pagamento.Reembolso == null)
            .SumAsync(p => (decimal?)p.Pagamento!.Valor, cancellationToken) ?? 0m;
    }

    private static async Task<List<ResumoFormaPagamentoDto>> AgruparPorFormaPagamentoAsync(
        IQueryable<Pedido> pedidos,
        CancellationToken cancellationToken)
    {
        var resumos = await pedidos
            .Where(p => p.Pagamento != null)
            .GroupBy(p => p.Pagamento!.FormaPagamento)
            .Select(grupo => new
            {
                FormaPagamento = grupo.Key,
                Quantidade = grupo.Count(),
                Total = grupo.Sum(p => p.Pagamento!.Valor)
            })
            .OrderBy(resumo => resumo.FormaPagamento)
            .ToListAsync(cancellationToken);

        return resumos
            .Select(resumo => new ResumoFormaPagamentoDto
            {
                FormaPagamento = resumo.FormaPagamento,
                Quantidade = resumo.Quantidade,
                Total = resumo.Total
            })
            .ToList();
    }
}
