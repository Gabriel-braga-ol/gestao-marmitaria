using GestaoMarmitaria.Api.Enums;

namespace GestaoMarmitaria.Api.Dtos
{
    public class PagamentoRespostaDto
    {
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public FormaPagamento FormaPagamento { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataPagamento { get; set; }
        public int? UsuarioRegistroId { get; set; }
        public string? UsuarioRegistroNome { get; set; }
        public DateTime? DataRegistro { get; set; }
    }
}
