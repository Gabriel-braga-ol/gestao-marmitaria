using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Features.Pagamentos
{
    public class PagamentoRespostaDto
    {
        public int Id { get; init; }
        public int PedidoId { get; init; }
        public FormaPagamento FormaPagamento { get; init; }
        public decimal Valor { get; init; }
        public DateTime DataPagamento { get; init; }
        public int? UsuarioRegistroId { get; init; }
        public string? UsuarioRegistroNome { get; init; }
        public DateTime? DataRegistro { get; init; }
    }
}
