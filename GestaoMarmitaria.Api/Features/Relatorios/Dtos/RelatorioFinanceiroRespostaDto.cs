using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public class RelatorioFinanceiroRespostaDto
{
    public DateOnly DataInicial { get; set; }
    public DateOnly DataFinal { get; set; }
    public int TotalPedidos { get; set; }
    public int PedidosPagos { get; set; }
    public int PedidosReembolsados { get; set; }
    public decimal TotalPago { get; set; }
    public decimal TotalReembolsado { get; set; }
    public decimal TotalReembolsoPendente { get; set; }
    public decimal TotalLiquido => TotalPago - TotalReembolsado;
    public List<ResumoFormaPagamentoDto> PorFormaPagamento { get; set; } = new();
}
