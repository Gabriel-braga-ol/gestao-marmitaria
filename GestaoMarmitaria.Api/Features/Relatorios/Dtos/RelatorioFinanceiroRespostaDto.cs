using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public class RelatorioFinanceiroRespostaDto
{
    public DateOnly DataInicial { get; init; }
    public DateOnly DataFinal { get; init; }
    public int TotalPedidos { get; init; }
    public int PedidosPagos { get; init; }
    public int PedidosReembolsados { get; init; }
    public decimal TotalPago { get; init; }
    public decimal TotalReembolsado { get; init; }
    public decimal TotalReembolsoPendente { get; init; }
    public decimal TotalLiquido => TotalPago - TotalReembolsado;
    public List<ResumoFormaPagamentoDto> PorFormaPagamento { get; init; } = new();
}
