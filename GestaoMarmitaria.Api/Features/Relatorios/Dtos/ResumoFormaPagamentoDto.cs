using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Relatorios;

public class ResumoFormaPagamentoDto
{
    public FormaPagamento FormaPagamento { get; init; }
    public int Quantidade { get; init; }
    public decimal Total { get; init; }
}
