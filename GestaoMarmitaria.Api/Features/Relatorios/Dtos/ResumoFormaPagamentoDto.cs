using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public class ResumoFormaPagamentoDto
{
    public FormaPagamento FormaPagamento { get; set; }
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
}
